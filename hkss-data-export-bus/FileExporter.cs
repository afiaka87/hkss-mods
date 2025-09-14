using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HKSS.DataExportBus
{
    public class FileExporter : IDisposable
    {
        private readonly string exportDirectory;
        private readonly ExportFormat format;
        private readonly long maxFileSizeBytes;
        private readonly TimeSpan rotationInterval;

        private StreamWriter currentCsvWriter;
        private StreamWriter currentNdjsonWriter;
        private string currentCsvPath;
        private string currentNdjsonPath;
        private DateTime lastRotationTime;
        private long currentCsvSize;
        private long currentNdjsonSize;
        private readonly object fileLock = new object();
        private bool csvHeaderWritten = false;
        private Timer rotationTimer;

        public FileExporter(string directory, ExportFormat format, int maxSizeMB, int rotationMinutes)
        {
            this.format = format;
            this.maxFileSizeBytes = maxSizeMB * 1024 * 1024;
            this.rotationInterval = TimeSpan.FromMinutes(rotationMinutes);

            // Create export directory
            if (!Path.IsPathRooted(directory))
            {
                directory = Path.Combine(Directory.GetCurrentDirectory(), directory);
            }
            this.exportDirectory = directory;

            if (!Directory.Exists(exportDirectory))
            {
                Directory.CreateDirectory(exportDirectory);
                DataExportBusPlugin.ModLogger?.LogInfo($"Created export directory: {exportDirectory}");
            }

            InitializeFiles();

            // Setup rotation timer
            rotationTimer = new Timer(CheckRotation, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        private void InitializeFiles()
        {
            lock (fileLock)
            {
                lastRotationTime = DateTime.UtcNow;
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

                // Initialize CSV file
                if (format == ExportFormat.CSV || format == ExportFormat.Both)
                {
                    currentCsvPath = Path.Combine(exportDirectory, $"metrics_{timestamp}.csv");
                    currentCsvWriter = new StreamWriter(currentCsvPath, false, Encoding.UTF8) { AutoFlush = true };
                    csvHeaderWritten = false;
                    currentCsvSize = 0;
                    DataExportBusPlugin.ModLogger?.LogInfo($"Created CSV file: {currentCsvPath}");
                }

                // Initialize NDJSON file
                if (format == ExportFormat.NDJSON || format == ExportFormat.Both)
                {
                    currentNdjsonPath = Path.Combine(exportDirectory, $"metrics_{timestamp}.ndjson");
                    currentNdjsonWriter = new StreamWriter(currentNdjsonPath, false, Encoding.UTF8) { AutoFlush = true };
                    currentNdjsonSize = 0;
                    DataExportBusPlugin.ModLogger?.LogInfo($"Created NDJSON file: {currentNdjsonPath}");
                }
            }
        }

        public void WriteMetric(GameMetric metric)
        {
            lock (fileLock)
            {
                try
                {
                    // Write to CSV
                    if (currentCsvWriter != null)
                    {
                        WriteCsv(metric);
                    }

                    // Write to NDJSON
                    if (currentNdjsonWriter != null)
                    {
                        WriteNdjson(metric);
                    }

                    // Check if rotation is needed
                    CheckFileSize();
                }
                catch (Exception ex)
                {
                    DataExportBusPlugin.ModLogger?.LogError($"Error writing metric to file: {ex}");
                }
            }
        }

        private void WriteCsv(GameMetric metric)
        {
            // Flatten the metric data for CSV
            var row = new List<string>
            {
                metric.Timestamp.ToString("o"),
                metric.EventType
            };

            // Write header if needed
            if (!csvHeaderWritten)
            {
                var headers = new List<string> { "Timestamp", "EventType" };

                // Add headers for all possible data fields
                var commonFields = new[] { "position_x", "position_y", "health_current", "health_max",
                                          "soul_current", "soul_max", "damage", "enemy_name", "scene_name",
                                          "item_name", "ability_name", "boss_name", "session_time" };

                headers.AddRange(commonFields);
                currentCsvWriter.WriteLine(string.Join(",", headers));
                csvHeaderWritten = true;
            }

            // Add data values
            var dataFields = new[] { "position_x", "position_y", "health_current", "health_max",
                                      "soul_current", "soul_max", "damage", "enemy_name", "scene_name",
                                      "item_name", "ability_name", "boss_name", "session_time" };

            foreach (var field in dataFields)
            {
                if (metric.Data.ContainsKey(field))
                {
                    string value = metric.Data[field]?.ToString() ?? "";
                    // Escape CSV values
                    if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                    {
                        value = $"\"{value.Replace("\"", "\"\"")}\"";
                    }
                    row.Add(value);
                }
                else
                {
                    row.Add("");
                }
            }

            string csvLine = string.Join(",", row);
            currentCsvWriter.WriteLine(csvLine);
            currentCsvSize += Encoding.UTF8.GetByteCount(csvLine) + 2; // +2 for \r\n
        }

        private void WriteNdjson(GameMetric metric)
        {
            // Create a clean object for NDJSON
            var jsonObject = new
            {
                timestamp = metric.Timestamp.ToString("o"),
                event_type = metric.EventType,
                data = metric.Data
            };

            string json = JsonConvert.SerializeObject(jsonObject, Formatting.None);
            currentNdjsonWriter.WriteLine(json);
            currentNdjsonSize += Encoding.UTF8.GetByteCount(json) + 1; // +1 for \n
        }

        private void CheckFileSize()
        {
            bool shouldRotate = false;

            // Check CSV size
            if (currentCsvWriter != null && currentCsvSize >= maxFileSizeBytes)
            {
                DataExportBusPlugin.ModLogger?.LogInfo($"CSV file size exceeded {maxFileSizeBytes} bytes, rotating...");
                shouldRotate = true;
            }

            // Check NDJSON size
            if (currentNdjsonWriter != null && currentNdjsonSize >= maxFileSizeBytes)
            {
                DataExportBusPlugin.ModLogger?.LogInfo($"NDJSON file size exceeded {maxFileSizeBytes} bytes, rotating...");
                shouldRotate = true;
            }

            if (shouldRotate)
            {
                RotateFiles();
            }
        }

        private void CheckRotation(object state)
        {
            lock (fileLock)
            {
                if (DateTime.UtcNow - lastRotationTime >= rotationInterval)
                {
                    DataExportBusPlugin.ModLogger?.LogInfo($"Rotation interval reached ({rotationInterval}), rotating files...");
                    RotateFiles();
                }
            }
        }

        private void RotateFiles()
        {
            lock (fileLock)
            {
                try
                {
                    // Close current files
                    currentCsvWriter?.Close();
                    currentNdjsonWriter?.Close();

                    // Archive old files
                    ArchiveFiles();

                    // Initialize new files
                    InitializeFiles();
                }
                catch (Exception ex)
                {
                    DataExportBusPlugin.ModLogger?.LogError($"Error rotating files: {ex}");
                }
            }
        }

        private void ArchiveFiles()
        {
            try
            {
                // Create archive directory if needed
                string archiveDir = Path.Combine(exportDirectory, "archive");
                if (!Directory.Exists(archiveDir))
                {
                    Directory.CreateDirectory(archiveDir);
                }

                // Move completed files to archive
                if (!string.IsNullOrEmpty(currentCsvPath) && File.Exists(currentCsvPath))
                {
                    string archivePath = Path.Combine(archiveDir, Path.GetFileName(currentCsvPath));
                    File.Move(currentCsvPath, archivePath);
                    DataExportBusPlugin.ModLogger?.LogInfo($"Archived CSV: {archivePath}");

                    // Optionally compress archived files
                    CompressFile(archivePath);
                }

                if (!string.IsNullOrEmpty(currentNdjsonPath) && File.Exists(currentNdjsonPath))
                {
                    string archivePath = Path.Combine(archiveDir, Path.GetFileName(currentNdjsonPath));
                    File.Move(currentNdjsonPath, archivePath);
                    DataExportBusPlugin.ModLogger?.LogInfo($"Archived NDJSON: {archivePath}");

                    // Optionally compress archived files
                    CompressFile(archivePath);
                }

                // Clean up old archives (keep last 10 files)
                CleanupOldArchives(archiveDir);
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"Error archiving files: {ex}");
            }
        }

        private void CompressFile(string filePath)
        {
            // Note: System.IO.Compression might not be available in Unity
            // This is a placeholder for compression logic
            // In a real implementation, you might use a Unity-compatible compression library
        }

        private void CleanupOldArchives(string archiveDir)
        {
            try
            {
                var files = Directory.GetFiles(archiveDir)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .Skip(10)
                    .ToList();

                foreach (var file in files)
                {
                    file.Delete();
                    DataExportBusPlugin.ModLogger?.LogInfo($"Deleted old archive: {file.Name}");
                }
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"Error cleaning up archives: {ex}");
            }
        }

        public string GetCurrentCsvPath()
        {
            lock (fileLock)
            {
                return currentCsvPath;
            }
        }

        public string GetCurrentNdjsonPath()
        {
            lock (fileLock)
            {
                return currentNdjsonPath;
            }
        }

        public Dictionary<string, object> GetStatistics()
        {
            lock (fileLock)
            {
                return new Dictionary<string, object>
                {
                    ["export_directory"] = exportDirectory,
                    ["format"] = format.ToString(),
                    ["current_csv_file"] = currentCsvPath,
                    ["current_csv_size"] = currentCsvSize,
                    ["current_ndjson_file"] = currentNdjsonPath,
                    ["current_ndjson_size"] = currentNdjsonSize,
                    ["last_rotation"] = lastRotationTime.ToString("o"),
                    ["next_rotation"] = (lastRotationTime + rotationInterval).ToString("o"),
                    ["max_file_size_mb"] = maxFileSizeBytes / (1024 * 1024),
                    ["rotation_interval_minutes"] = rotationInterval.TotalMinutes
                };
            }
        }

        public void Dispose()
        {
            lock (fileLock)
            {
                rotationTimer?.Dispose();
                currentCsvWriter?.Close();
                currentNdjsonWriter?.Close();
                currentCsvWriter?.Dispose();
                currentNdjsonWriter?.Dispose();
            }
        }
    }
}