using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Logging;

namespace HKSS.DataExportBus.Security
{
    public static class SecurityValidator
    {
        private static readonly ManualLogSource Logger = DataExportBusPlugin.ModLogger;
        private static readonly HashSet<string> ReservedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
        private static readonly Regex PathTraversalPattern = new Regex(@"\.\.[\\/]", RegexOptions.Compiled);

        // Network validation
        public static bool ValidateBindAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return false;

            // Allow localhost, loopback, or any valid IP
            if (string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(address, "loopback", StringComparison.OrdinalIgnoreCase))
                return true;

            // Validate IP address
            return IPAddress.TryParse(address, out var ip);
        }

        public static bool ValidatePort(int port)
        {
            return port >= 1024 && port <= 65535;
        }

        // Path validation
        public static string ValidateAndSanitizePath(string path, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty");

            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("Base directory cannot be null or empty");

            // Remove any path traversal attempts
            if (PathTraversalPattern.IsMatch(path))
            {
                Logger?.LogWarning($"Path traversal attempt detected: {path}");
                throw new SecurityException("Path traversal is not allowed");
            }

            // Remove invalid characters
            foreach (char c in InvalidPathChars)
            {
                path = path.Replace(c.ToString(), "");
            }

            // Ensure path is within base directory
            string fullPath;
            if (Path.IsPathRooted(path))
            {
                // If absolute path provided, validate it's within allowed directory
                fullPath = Path.GetFullPath(path);
                string baseFullPath = Path.GetFullPath(baseDirectory);

                if (!fullPath.StartsWith(baseFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    Logger?.LogWarning($"Path outside base directory: {fullPath} not in {baseFullPath}");
                    throw new SecurityException("Path must be within the base directory");
                }
            }
            else
            {
                // Relative path - combine with base
                fullPath = Path.GetFullPath(Path.Combine(baseDirectory, path));
                string baseFullPath = Path.GetFullPath(baseDirectory);

                // Verify the resolved path is still within base directory
                if (!fullPath.StartsWith(baseFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    Logger?.LogWarning($"Path escape attempt: {fullPath} not in {baseFullPath}");
                    throw new SecurityException("Path must be within the base directory");
                }
            }

            return fullPath;
        }

        public static string ValidateFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or empty");

            // Check for reserved names
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            if (ReservedFileNames.Contains(nameWithoutExtension))
            {
                throw new ArgumentException($"Reserved file name: {fileName}");
            }

            // Remove invalid characters
            foreach (char c in InvalidFileNameChars)
            {
                fileName = fileName.Replace(c.ToString(), "");
            }

            // Limit file name length
            if (fileName.Length > 255)
            {
                fileName = fileName.Substring(0, 255);
            }

            return fileName;
        }

        // HTML sanitization
        public static string SanitizeHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Basic HTML encoding
            return System.Web.HttpUtility.HtmlEncode(input);
        }

        public static string SanitizeJson(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Escape JSON special characters
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\b", "\\b")
                .Replace("\f", "\\f")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        // Authentication
        public static bool SecureCompareStrings(string a, string b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;
            if (a.Length != b.Length)
                return false;

            // Constant-time comparison to prevent timing attacks
            uint diff = (uint)a.Length ^ (uint)b.Length;
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                diff |= (uint)(a[i] ^ b[i]);
            }
            return diff == 0;
        }

        public static string HashToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return string.Empty;

            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(token);
                byte[] hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        // Input size validation
        public static bool ValidateRequestSize(long size, long maxSize)
        {
            return size >= 0 && size <= maxSize;
        }

        // CORS validation
        public static bool ValidateOrigin(string origin, string[] allowedOrigins)
        {
            if (allowedOrigins == null || allowedOrigins.Length == 0)
                return false;

            // Check for wildcard
            if (allowedOrigins.Contains("*"))
                return true;

            // Check exact match (case-insensitive for protocol/host)
            foreach (var allowed in allowedOrigins)
            {
                if (string.Equals(allowed, origin, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Support wildcard subdomains
                if (allowed.StartsWith("*.") && origin != null)
                {
                    string domain = allowed.Substring(2);
                    Uri originUri;
                    if (Uri.TryCreate(origin, UriKind.Absolute, out originUri))
                    {
                        if (originUri.Host.EndsWith(domain, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }

            return false;
        }

        // Rate limiting
        private static readonly Dictionary<string, RateLimitInfo> RateLimitCache = new Dictionary<string, RateLimitInfo>();
        private static readonly object RateLimitLock = new object();

        private class RateLimitInfo
        {
            public DateTime WindowStart { get; set; }
            public int RequestCount { get; set; }
        }

        public static bool CheckRateLimit(string identifier, int maxRequests, TimeSpan window)
        {
            lock (RateLimitLock)
            {
                var now = DateTime.UtcNow;

                // Clean old entries
                var expiredKeys = RateLimitCache
                    .Where(kvp => now - kvp.Value.WindowStart > window)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    RateLimitCache.Remove(key);
                }

                // Check current rate
                if (!RateLimitCache.TryGetValue(identifier, out var info))
                {
                    info = new RateLimitInfo
                    {
                        WindowStart = now,
                        RequestCount = 0
                    };
                    RateLimitCache[identifier] = info;
                }

                // Reset window if expired
                if (now - info.WindowStart > window)
                {
                    info.WindowStart = now;
                    info.RequestCount = 0;
                }

                // Check limit
                info.RequestCount++;
                return info.RequestCount <= maxRequests;
            }
        }

        // Command injection prevention
        public static string SanitizeCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return string.Empty;

            // Remove potentially dangerous characters
            var dangerous = new[] { ';', '|', '&', '$', '`', '>', '<', '(', ')', '{', '}', '[', ']', '\n', '\r' };
            foreach (char c in dangerous)
            {
                command = command.Replace(c.ToString(), "");
            }

            return command.Trim();
        }

        // Validate configuration values
        public static T ValidateConfigValue<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0)
                return min;
            if (value.CompareTo(max) > 0)
                return max;
            return value;
        }

        public static void ValidateConfiguration(DataExportBusPlugin plugin)
        {
            // Validate ports
            if (!ValidatePort(plugin.HttpPort.Value))
            {
                Logger?.LogWarning($"Invalid HTTP port {plugin.HttpPort.Value}, using default 8080");
                plugin.HttpPort.Value = 8080;
            }

            if (!ValidatePort(plugin.TcpPort.Value))
            {
                Logger?.LogWarning($"Invalid TCP port {plugin.TcpPort.Value}, using default 9090");
                plugin.TcpPort.Value = 9090;
            }

            if (!ValidatePort(plugin.WebSocketPort.Value))
            {
                Logger?.LogWarning($"Invalid WebSocket port {plugin.WebSocketPort.Value}, using default 9091");
                plugin.WebSocketPort.Value = 9091;
            }

            // Validate bind address
            if (!ValidateBindAddress(plugin.HttpBindAddress.Value))
            {
                Logger?.LogWarning($"Invalid bind address {plugin.HttpBindAddress.Value}, using localhost");
                plugin.HttpBindAddress.Value = "localhost";
            }

            // Validate numeric ranges
            plugin.UpdateFrequencyHz.Value = ValidateConfigValue(plugin.UpdateFrequencyHz.Value, 1f, 60f);
            plugin.FileRotationSizeMB.Value = ValidateConfigValue(plugin.FileRotationSizeMB.Value, 1, 100);
            plugin.FileRotationMinutes.Value = ValidateConfigValue(plugin.FileRotationMinutes.Value, 1, 1440);
            plugin.MaxConnectionsPerIP.Value = ValidateConfigValue(plugin.MaxConnectionsPerIP.Value, 1, 20);
            plugin.AdvancedMetricsIntervalSec.Value = ValidateConfigValue(plugin.AdvancedMetricsIntervalSec.Value, 0.1f, 10f);
        }

        public class SecurityException : Exception
        {
            public SecurityException(string message) : base(message) { }
        }
    }
}