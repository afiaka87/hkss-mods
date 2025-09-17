using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using HKSS.DataExportBus.Security;
using BepInEx.Logging;

namespace HKSS.DataExportBus
{
    public class FileExporter : IDisposable
    {
        private readonly ManualLogSource _logger;
        private readonly string _exportDirectory;
        private readonly string _baseDirectory;
        private readonly ExportFormat _format;
        private readonly long _maxFileSizeBytes;
        private readonly TimeSpan _rotationInterval;

        private StreamWriter _currentCsvWriter;
        private StreamWriter _currentNdjsonWriter;
        private string _currentCsvPath;
        private string _currentNdjsonPath;
        private DateTime _lastRotationTime;
        private long _currentCsvSize;
        private long _currentNdjsonSize;
        private readonly ReaderWriterLockSlim _fileLock;
        private bool _csvHeaderWritten;
        private Timer _rotationTimer;
        private bool _disposed;
        private readonly SemaphoreSlim _writeSemaphore;

        public FileExporter(string directory, ExportFormat format, int maxSizeMB, int rotationMinutes)
        {
            _logger = DataExportBusPlugin.ModLogger;
            _format = format;
            _fileLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _writeSemaphore = new SemaphoreSlim(1, 1);
            _disposed = false;

            // Validate and sanitize inputs
            maxSizeMB = SecurityValidator.ValidateConfigValue(maxSizeMB,
                HKSS.DataExportBus.Configuration.Constants.FileExport.MIN_ROTATION_SIZE_MB,
                HKSS.DataExportBus.Configuration.Constants.FileExport.MAX_ROTATION_SIZE_MB);
            rotationMinutes = SecurityValidator.ValidateConfigValue(rotationMinutes,
                HKSS.DataExportBus.Configuration.Constants.FileExport.MIN_ROTATION_MINUTES,
                HKSS.DataExportBus.Configuration.Constants.FileExport.MAX_ROTATION_MINUTES);

            _maxFileSizeBytes = maxSizeMB * 1024 * 1024;
            _rotationInterval = TimeSpan.FromMinutes(rotationMinutes);

            // Validate and create export directory with security checks
            try
            {
                _baseDirectory = Directory.GetCurrentDirectory();
                _exportDirectory = SecurityValidator.ValidateAndSanitizePath(directory, _baseDirectory);

                if (!Directory.Exists(_exportDirectory))
                {
                    Directory.CreateDirectory(_exportDirectory);
                    _logger?.LogInfo($"Created export directory: {_exportDirectory}");
                }

                InitializeFiles();

                // Setup rotation timer with error handling
                _rotationTimer = new Timer(
                    callback: SafeCheckRotation,
                    state: null,
                    dueTime: TimeSpan.FromMinutes(HKSS.DataExportBus.Configuration.Constants.Timers.FILE_ROTATION_CHECK_INTERVAL_MINUTES),
                    period: TimeSpan.FromMinutes(HKSS.DataExportBus.Configuration.Constants.Timers.FILE_ROTATION_CHECK_INTERVAL_MINUTES)
                );
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to initialize FileExporter: {ex}");
                throw;
            }
        }

        private void InitializeFiles()
        {
            _fileLock.EnterWriteLock();
            try
            {
                // Close existing writers safely
                CloseWriters();

                _lastRotationTime = DateTime.UtcNow;
                string timestamp = DateTime.UtcNow.ToString(HKSS.DataExportBus.Configuration.Constants.FileExport.TIMESTAMP_FORMAT);

                // Initialize CSV file
                if (_format == ExportFormat.CSV || _format == ExportFormat.Both)
                {
                    try
                    {
                        string csvFileName = SecurityValidator.ValidateFileName($"metrics_{timestamp}{HKSS.DataExportBus.Configuration.Constants.FileExport.CSV_EXTENSION}");
                        _currentCsvPath = Path.Combine(_exportDirectory, csvFileName);
                        _currentCsvWriter = new StreamWriter(_currentCsvPath, false, Encoding.UTF8)
                        {
                            AutoFlush = true
                        };
                        _csvHeaderWritten = false;
                        _currentCsvSize = 0;
                        _logger?.LogInfo($"Created CSV file: {_currentCsvPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Failed to create CSV file: {ex}");
                    }
                }

                // Initialize NDJSON file
                if (_format == ExportFormat.NDJSON || _format == ExportFormat.Both)
                {
                    try
                    {
                        string ndjsonFileName = SecurityValidator.ValidateFileName($"metrics_{timestamp}{HKSS.DataExportBus.Configuration.Constants.FileExport.NDJSON_EXTENSION}");
                        _currentNdjsonPath = Path.Combine(_exportDirectory, ndjsonFileName);
                        _currentNdjsonWriter = new StreamWriter(_currentNdjsonPath, false, Encoding.UTF8)
                        {
                            AutoFlush = true
                        };
                        _currentNdjsonSize = 0;
                        _logger?.LogInfo($"Created NDJSON file: {_currentNdjsonPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Failed to create NDJSON file: {ex}");
                    }
                }
            }
            finally
            {
                _fileLock.ExitWriteLock();
            }
        }

        private void CloseWriters()
        {
            try
            {
                _currentCsvWriter?.Flush();
                _currentCsvWriter?.Close();
                _currentCsvWriter?.Dispose();
                _currentCsvWriter = null;

                _currentNdjsonWriter?.Flush();
                _currentNdjsonWriter?.Close();
                _currentNdjsonWriter?.Dispose();
                _currentNdjsonWriter = null;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error closing writers: {ex}");
            }
        }

        public async Task WriteMetricAsync(GameMetric metric)
        {
            if (_disposed)
                return;

            if (metric == null)
            {
                _logger?.LogWarning("Attempted to write null metric");
                return;
            }

            await _writeSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                _fileLock.EnterWriteLock();
                try
                {
                    // Write to CSV
                    if (_currentCsvWriter != null)
                    {
                        await WriteCsvAsync(metric).ConfigureAwait(false);
                    }

                    // Write to NDJSON
                    if (_currentNdjsonWriter != null)
                    {
                        await WriteNdjsonAsync(metric).ConfigureAwait(false);
                    }

                    // Check if rotation is needed
                    CheckFileSize();
                }
                finally
                {
                    _fileLock.ExitWriteLock();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error writing metric to file: {ex}");
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        public void WriteMetric(GameMetric metric)
        {
            // Synchronous wrapper for backward compatibility
            Task.Run(async () =>
            {
                try
                {
                    await WriteMetricAsync(metric).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error writing metric: {ex}");
                }
            });
        }

        private async Task WriteCsvAsync(GameMetric metric)
        {
            // Flatten the metric data for CSV
            var row = new List<string>
            {
                metric.Timestamp.ToString("o"),
                EscapeCsvValue(metric.EventType)
            };

            // Write header if needed
            if (!_csvHeaderWritten)
            {
                var headers = new List<string> { "Timestamp", "EventType" };

                // Add headers for all possible data fields using constants
                var commonFields = new[] {
                    HKSS.DataExportBus.Configuration.Constants.MetricFields.POSITION_X, HKSS.DataExportBus.Configuration.Constants.MetricFields.POSITION_Y,
                    HKSS.DataExportBus.Configuration.Constants.MetricFields.HEALTH_CURRENT, HKSS.DataExportBus.Configuration.Constants.MetricFields.HEALTH_MAX,
                    HKSS.DataExportBus.Configuration.Constants.MetricFields.SOUL_CURRENT, HKSS.DataExportBus.Configuration.Constants.MetricFields.SOUL_MAX,
                    HKSS.DataExportBus.Configuration.Constants.MetricFields.DAMAGE, HKSS.DataExportBus.Configuration.Constants.MetricFields.ENEMY_NAME,
                    HKSS.DataExportBus.Configuration.Constants.MetricFields.SCENE_NAME, HKSS.DataExportBus.Configuration.Constants.MetricFields.ITEM_NAME,
                    HKSS.DataExportBus.Configuration.Constants.MetricFields.ABILITY_NAME, HKSS.DataExportBus.Configuration.Constants.MetricFields.BOSS_NAME,
                    HKSS.DataExportBus.Configuration.Constants.MetricFields.SESSION_TIME
                };

                headers.AddRange(commonFields);
                await _currentCsvWriter.WriteLineAsync(string.Join(",", headers)).ConfigureAwait(false);
                _csvHeaderWritten = true;
            }

            // Add data values using constants
            var dataFields = new[] {
                HKSS.DataExportBus.Configuration.Constants.MetricFields.POSITION_X, HKSS.DataExportBus.Configuration.Constants.MetricFields.POSITION_Y,
                HKSS.DataExportBus.Configuration.Constants.MetricFields.HEALTH_CURRENT, HKSS.DataExportBus.Configuration.Constants.MetricFields.HEALTH_MAX,
                HKSS.DataExportBus.Configuration.Constants.MetricFields.SOUL_CURRENT, HKSS.DataExportBus.Configuration.Constants.MetricFields.SOUL_MAX,
                HKSS.DataExportBus.Configuration.Constants.MetricFields.DAMAGE, HKSS.DataExportBus.Configuration.Constants.MetricFields.ENEMY_NAME,
                HKSS.DataExportBus.Configuration.Constants.MetricFields.SCENE_NAME, HKSS.DataExportBus.Configuration.Constants.MetricFields.ITEM_NAME,
                HKSS.DataExportBus.Configuration.Constants.MetricFields.ABILITY_NAME, HKSS.DataExportBus.Configuration.Constants.MetricFields.BOSS_NAME,
                HKSS.DataExportBus.Configuration.Constants.MetricFields.SESSION_TIME
            };

            foreach (var field in dataFields)
            {
                if (metric.Data.ContainsKey(field))
                {
                    string value = metric.Data[field]?.ToString() ?? "";
                    row.Add(EscapeCsvValue(value));
                }
                else
                {
                    row.Add("");
                }
            }

            string csvLine = string.Join(",", row);
            await _currentCsvWriter.WriteLineAsync(csvLine).ConfigureAwait(false);
            _currentCsvSize += Encoding.UTF8.GetByteCount(csvLine) + Environment.NewLine.Length;
        }

        private async Task WriteNdjsonAsync(GameMetric metric)
        {
            // Create a clean object for NDJSON
            var jsonObject = new
            {
                timestamp = metric.Timestamp.ToString("o"),
                event_type = metric.EventType,
                data = metric.Data
            };

            string json = JsonConvert.SerializeObject(jsonObject, Formatting.None);
            await _currentNdjsonWriter.WriteLineAsync(json).ConfigureAwait(false);
            _currentNdjsonSize += Encoding.UTF8.GetByteCount(json) + 1;
        }

        private void CheckFileSize()
        {
            bool shouldRotate = false;

            // Check CSV size
            if (_currentCsvWriter != null && _currentCsvSize >= _maxFileSizeBytes)
            {
                _logger?.LogInfo($"CSV file size exceeded {_maxFileSizeBytes} bytes, rotating...");
                shouldRotate = true;
            }

            // Check NDJSON size
            if (_currentNdjsonWriter != null && _currentNdjsonSize >= _maxFileSizeBytes)
            {
                _logger?.LogInfo($"NDJSON file size exceeded {_maxFileSizeBytes} bytes, rotating...");
                shouldRotate = true;
            }

            if (shouldRotate)
            {
                RotateFiles();
            }
        }

        private void SafeCheckRotation(object state)
        {
            try
            {
                if (_disposed)
                    return;

                _fileLock.EnterUpgradeableReadLock();
                try
                {
                    if (DateTime.UtcNow - _lastRotationTime >= _rotationInterval)
                    {
                        _logger?.LogInfo($"Rotation interval reached ({_rotationInterval}), rotating files...");
                        RotateFiles();
                    }
                }
                finally
                {
                    _fileLock.ExitUpgradeableReadLock();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error in rotation timer: {ex}");
            }
        }

        private void RotateFiles()
        {
            if (_disposed)
                return;

            _fileLock.EnterWriteLock();
            try
            {
                // Archive old files
                ArchiveFiles();

                // Initialize new files
                InitializeFiles();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error rotating files: {ex}");
            }
            finally
            {
                _fileLock.ExitWriteLock();
            }
        }

        private void ArchiveFiles()
        {
            try
            {
                // Create archive directory if needed with validation
                string archiveDir = SecurityValidator.ValidateAndSanitizePath(
                    HKSS.DataExportBus.Configuration.Constants.FileExport.ARCHIVE_DIRECTORY_NAME,
                    _exportDirectory);

                if (!Directory.Exists(archiveDir))
                {
                    Directory.CreateDirectory(archiveDir);
                }

                // Move completed files to archive
                if (!string.IsNullOrEmpty(_currentCsvPath) && File.Exists(_currentCsvPath))
                {
                    try
                    {
                        string fileName = Path.GetFileName(_currentCsvPath);
                        string archivePath = Path.Combine(archiveDir, fileName);

                        // Handle existing file
                        if (File.Exists(archivePath))
                        {
                            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                            archivePath = Path.Combine(archiveDir, $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}{Path.GetExtension(fileName)}");
                        }

                        File.Move(_currentCsvPath, archivePath);
                        _logger?.LogInfo($"Archived CSV: {archivePath}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Failed to archive CSV file: {ex}");
                    }
                }

                if (!string.IsNullOrEmpty(_currentNdjsonPath) && File.Exists(_currentNdjsonPath))
                {
                    try
                    {
                        string fileName = Path.GetFileName(_currentNdjsonPath);
                        string archivePath = Path.Combine(archiveDir, fileName);

                        // Handle existing file
                        if (File.Exists(archivePath))
                        {
                            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                            archivePath = Path.Combine(archiveDir, $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}{Path.GetExtension(fileName)}");
                        }

                        File.Move(_currentNdjsonPath, archivePath);
                        _logger?.LogInfo($"Archived NDJSON: {archivePath}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError($"Failed to archive NDJSON file: {ex}");
                    }
                }

                // Clean up old archives
                CleanupOldArchives(archiveDir);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error archiving files: {ex}");
            }
        }


        private void CleanupOldArchives(string archiveDir)
        {
            try
            {
                var files = Directory.GetFiles(archiveDir)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .Skip(HKSS.DataExportBus.Configuration.Constants.FileExport.MAX_ARCHIVE_FILES)
                    .ToList();

                foreach (var file in files)
                {
                    file.Delete();
                    _logger?.LogInfo($"Deleted old archive: {file.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error cleaning up archives: {ex}");
            }
        }

        public string GetCurrentCsvPath()
        {
            _fileLock.EnterReadLock();
            try
            {
                return _currentCsvPath;
            }
            finally
            {
                _fileLock.ExitReadLock();
            }
        }

        public string GetCurrentNdjsonPath()
        {
            _fileLock.EnterReadLock();
            try
            {
                return _currentNdjsonPath;
            }
            finally
            {
                _fileLock.ExitReadLock();
            }
        }

        public Dictionary<string, object> GetStatistics()
        {
            _fileLock.EnterReadLock();
            try
            {
                return new Dictionary<string, object>
                {
                    ["export_directory"] = _exportDirectory,
                    ["format"] = _format.ToString(),
                    ["current_csv_file"] = _currentCsvPath,
                    ["current_csv_size"] = _currentCsvSize,
                    ["current_ndjson_file"] = _currentNdjsonPath,
                    ["current_ndjson_size"] = _currentNdjsonSize,
                    ["last_rotation"] = _lastRotationTime.ToString(HKSS.DataExportBus.Configuration.Constants.FileExport.ISO_DATETIME_FORMAT),
                    ["next_rotation"] = (_lastRotationTime + _rotationInterval).ToString(HKSS.DataExportBus.Configuration.Constants.FileExport.ISO_DATETIME_FORMAT),
                    ["max_file_size_mb"] = _maxFileSizeBytes / (1024 * 1024),
                    ["rotation_interval_minutes"] = _rotationInterval.TotalMinutes
                };
            }
            finally
            {
                _fileLock.ExitReadLock();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _rotationTimer?.Dispose();

                _fileLock?.EnterWriteLock();
                try
                {
                    CloseWriters();
                }
                finally
                {
                    _fileLock?.ExitWriteLock();
                    _fileLock?.Dispose();
                }

                _writeSemaphore?.Dispose();
            }

            _disposed = true;
        }

        private string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            // Escape CSV values that contain commas, quotes, or newlines
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                // Replace quotes with double quotes and wrap in quotes
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}