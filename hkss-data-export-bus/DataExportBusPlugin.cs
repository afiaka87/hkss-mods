using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HKSS.DataExportBus
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.hkss.dataexportbus";
        public const string PLUGIN_NAME = "Data Export Bus";
        public const string PLUGIN_VERSION = "1.0.0";
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Hollow Knight Silksong.exe")]
    public class DataExportBusPlugin : BaseUnityPlugin
    {
        public static ManualLogSource ModLogger { get; private set; }
        public static DataExportBusPlugin Instance { get; private set; }

        private Harmony harmony;
        private GameObject exportBusObject;
        private CancellationTokenSource cancellationTokenSource;

        // Server instances
        private HttpServer httpServer;
        private TcpServer tcpServer;
        private WebSocketServer webSocketServer;
        private FileExporter fileExporter;
        private MetricsCollector metricsCollector;

        // Configuration
        public ConfigEntry<bool> Enabled { get; private set; }

        // HTTP Server Config
        public ConfigEntry<bool> EnableHttpServer { get; private set; }
        public ConfigEntry<int> HttpPort { get; private set; }
        public ConfigEntry<string> HttpBindAddress { get; private set; }

        // TCP Server Config
        public ConfigEntry<bool> EnableTcpServer { get; private set; }
        public ConfigEntry<int> TcpPort { get; private set; }
        public ConfigEntry<bool> EnableNamedPipe { get; private set; }

        // WebSocket Server Config
        public ConfigEntry<bool> EnableWebSocketServer { get; private set; }
        public ConfigEntry<int> WebSocketPort { get; private set; }

        // File Export Config
        public ConfigEntry<bool> EnableFileExport { get; private set; }
        public ConfigEntry<string> ExportDirectory { get; private set; }
        public ConfigEntry<ExportFormat> FileFormat { get; private set; }
        public ConfigEntry<int> FileRotationSizeMB { get; private set; }
        public ConfigEntry<int> FileRotationMinutes { get; private set; }

        // Data Collection Config
        public ConfigEntry<float> UpdateFrequencyHz { get; private set; }
        public ConfigEntry<bool> ExportPlayerData { get; private set; }
        public ConfigEntry<bool> ExportCombatData { get; private set; }
        public ConfigEntry<bool> ExportSceneData { get; private set; }
        public ConfigEntry<bool> ExportInventoryData { get; private set; }
        public ConfigEntry<bool> ExportTimingData { get; private set; }

        // Security Config
        public ConfigEntry<string> AuthToken { get; private set; }
        public ConfigEntry<string> AllowedOrigins { get; private set; }
        public ConfigEntry<bool> EnableRateLimiting { get; private set; }
        public ConfigEntry<int> MaxConnectionsPerIP { get; private set; }

        void Awake()
        {
            Instance = this;
            ModLogger = Logger;

            InitializeConfig();

            if (!Enabled.Value)
            {
                Logger.LogInfo("Data Export Bus is disabled in config");
                return;
            }

            cancellationTokenSource = new CancellationTokenSource();

            harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            CreateExportBusObject();
            InitializeServers();

            Logger.LogInfo($"Data Export Bus v{PluginInfo.PLUGIN_VERSION} loaded!");
        }

        void OnDestroy()
        {
            StopServers();

            harmony?.UnpatchSelf();

            if (exportBusObject != null)
            {
                Destroy(exportBusObject);
            }

            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }

        private void InitializeConfig()
        {
            // General
            Enabled = Config.Bind("General", "Enabled", true,
                "Enable or disable the data export bus");

            // HTTP Server
            EnableHttpServer = Config.Bind("HTTP", "EnableHttpServer", true,
                "Enable HTTP REST API server");

            HttpPort = Config.Bind("HTTP", "Port", 8080,
                new ConfigDescription("HTTP server port",
                new AcceptableValueRange<int>(1024, 65535)));

            HttpBindAddress = Config.Bind("HTTP", "BindAddress", "localhost",
                "HTTP bind address (localhost, 0.0.0.0, or specific IP)");

            // TCP Server (LiveSplit)
            EnableTcpServer = Config.Bind("TCP", "EnableTcpServer", true,
                "Enable TCP server for LiveSplit integration");

            TcpPort = Config.Bind("TCP", "Port", 9090,
                new ConfigDescription("TCP server port",
                new AcceptableValueRange<int>(1024, 65535)));

            EnableNamedPipe = Config.Bind("TCP", "EnableNamedPipe", true,
                "Enable named pipe for local LiveSplit connection");

            // WebSocket Server
            EnableWebSocketServer = Config.Bind("WebSocket", "EnableWebSocketServer", true,
                "Enable WebSocket server for real-time streaming");

            WebSocketPort = Config.Bind("WebSocket", "Port", 9091,
                new ConfigDescription("WebSocket server port",
                new AcceptableValueRange<int>(1024, 65535)));

            // File Export
            EnableFileExport = Config.Bind("FileExport", "EnableFileExport", true,
                "Enable file export (CSV/NDJSON)");

            ExportDirectory = Config.Bind("FileExport", "Directory", "DataExport",
                "Directory for exported files (relative to game directory)");

            FileFormat = Config.Bind("FileExport", "Format", ExportFormat.NDJSON,
                "File export format");

            FileRotationSizeMB = Config.Bind("FileExport", "RotationSizeMB", 10,
                new ConfigDescription("Rotate files when size exceeds (MB)",
                new AcceptableValueRange<int>(1, 100)));

            FileRotationMinutes = Config.Bind("FileExport", "RotationMinutes", 30,
                new ConfigDescription("Rotate files after time (minutes)",
                new AcceptableValueRange<int>(1, 1440)));

            // Data Collection
            UpdateFrequencyHz = Config.Bind("DataCollection", "UpdateFrequencyHz", 10f,
                new ConfigDescription("Data update frequency (Hz)",
                new AcceptableValueRange<float>(1f, 60f)));

            ExportPlayerData = Config.Bind("DataCollection", "ExportPlayerData", true,
                "Export player position, health, and state");

            ExportCombatData = Config.Bind("DataCollection", "ExportCombatData", true,
                "Export combat events (damage, kills, etc)");

            ExportSceneData = Config.Bind("DataCollection", "ExportSceneData", true,
                "Export scene transitions and room data");

            ExportInventoryData = Config.Bind("DataCollection", "ExportInventoryData", true,
                "Export inventory and ability changes");

            ExportTimingData = Config.Bind("DataCollection", "ExportTimingData", true,
                "Export speedrun timing data");

            // Security
            AuthToken = Config.Bind("Security", "AuthToken", "",
                "Authentication token (empty = no auth)");

            AllowedOrigins = Config.Bind("Security", "AllowedOrigins", "*",
                "CORS allowed origins (comma-separated)");

            EnableRateLimiting = Config.Bind("Security", "EnableRateLimiting", false,
                "Enable rate limiting for connections");

            MaxConnectionsPerIP = Config.Bind("Security", "MaxConnectionsPerIP", 5,
                new ConfigDescription("Max connections per IP address",
                new AcceptableValueRange<int>(1, 20)));
        }

        private void CreateExportBusObject()
        {
            exportBusObject = new GameObject("DataExportBus");

            // Add metrics collector
            metricsCollector = exportBusObject.AddComponent<MetricsCollector>();

            DontDestroyOnLoad(exportBusObject);
        }

        private void InitializeServers()
        {
            try
            {
                // Initialize file exporter first (others may depend on it)
                if (EnableFileExport.Value)
                {
                    fileExporter = new FileExporter(
                        ExportDirectory.Value,
                        FileFormat.Value,
                        FileRotationSizeMB.Value,
                        FileRotationMinutes.Value
                    );
                    Logger.LogInfo($"File exporter initialized: {ExportDirectory.Value}");
                }

                // Initialize HTTP server
                if (EnableHttpServer.Value)
                {
                    httpServer = new HttpServer(
                        HttpBindAddress.Value,
                        HttpPort.Value,
                        AuthToken.Value,
                        AllowedOrigins.Value
                    );
                    Task.Run(() => httpServer.StartAsync(cancellationTokenSource.Token));
                    Logger.LogInfo($"HTTP server started on {HttpBindAddress.Value}:{HttpPort.Value}");
                }

                // Initialize TCP server
                if (EnableTcpServer.Value)
                {
                    tcpServer = new TcpServer(
                        TcpPort.Value,
                        EnableNamedPipe.Value,
                        AuthToken.Value
                    );
                    Task.Run(() => tcpServer.StartAsync(cancellationTokenSource.Token));
                    Logger.LogInfo($"TCP server started on port {TcpPort.Value}");
                }

                // Initialize WebSocket server
                if (EnableWebSocketServer.Value)
                {
                    webSocketServer = new WebSocketServer(
                        WebSocketPort.Value,
                        AuthToken.Value,
                        AllowedOrigins.Value
                    );
                    Task.Run(() => webSocketServer.StartAsync(cancellationTokenSource.Token));
                    Logger.LogInfo($"WebSocket server started on port {WebSocketPort.Value}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize servers: {ex}");
            }
        }

        private void StopServers()
        {
            cancellationTokenSource?.Cancel();

            httpServer?.Stop();
            tcpServer?.Stop();
            webSocketServer?.Stop();
            fileExporter?.Dispose();

            Logger.LogInfo("All servers stopped");
        }

        public void BroadcastMetric(GameMetric metric)
        {
            // Send to all active exporters
            httpServer?.QueueMetric(metric);
            tcpServer?.SendMetric(metric);
            webSocketServer?.BroadcastMetric(metric);
            fileExporter?.WriteMetric(metric);
        }

        public MetricsCollector GetMetricsCollector()
        {
            return metricsCollector;
        }
    }

    public enum ExportFormat
    {
        CSV,
        NDJSON,
        Both
    }
}