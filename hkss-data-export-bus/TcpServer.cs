using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HKSS.DataExportBus
{
    public class TcpServer : IDisposable
    {
        private TcpListener tcpListener;
        private NamedPipeServerStream namedPipe;
        private readonly int port;
        private readonly bool enableNamedPipe;
        private readonly string authToken;
        private readonly List<TcpClient> connectedClients = new List<TcpClient>();
        private readonly object clientLock = new object();
        private bool isRunning = false;
        private const int MAX_CONNECTED_CLIENTS = 10;
        private const int MAX_SPLIT_TIMES = 200;

        // LiveSplit specific state
        private DateTime timerStartTime;
        private bool timerRunning = false;
        private int currentSplit = 0;
        private List<TimeSpan> splitTimes = new List<TimeSpan>();

        public TcpServer(int port, bool enableNamedPipe, string authToken)
        {
            this.port = port;
            this.enableNamedPipe = enableNamedPipe;
            this.authToken = authToken;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Start TCP listener
                tcpListener = new TcpListener(IPAddress.Any, port);
                tcpListener.Start();
                isRunning = true;

                DataExportBusPlugin.ModLogger?.LogInfo($"TCP server listening on port {port}");

                // Start named pipe if enabled
                if (enableNamedPipe)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await StartNamedPipeAsync(cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            DataExportBusPlugin.ModLogger?.LogError($"Named pipe error: {ex}");
                        }
                    }, cancellationToken);
                }

                // Accept TCP connections
                while (!cancellationToken.IsCancellationRequested && isRunning)
                {
                    try
                    {
                        var tcpClientTask = tcpListener.AcceptTcpClientAsync();
                        var completedTask = await Task.WhenAny(tcpClientTask, Task.Delay(-1, cancellationToken));

                        if (completedTask == tcpClientTask)
                        {
                            var client = await tcpClientTask;
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await HandleClient(client, cancellationToken);
                                }
                                catch (Exception ex)
                                {
                                    DataExportBusPlugin.ModLogger?.LogError($"TCP client handler error: {ex}");
                                }
                                finally
                                {
                                    client?.Dispose();
                                }
                            }, cancellationToken);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        DataExportBusPlugin.ModLogger?.LogError($"TCP server error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"Failed to start TCP server: {ex}");
            }
        }

        private async Task StartNamedPipeAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && isRunning)
                {
                    try
                    {
                        namedPipe = new NamedPipeServerStream(
                            "LiveSplitDataExport",
                            PipeDirection.InOut,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous
                        );

                        await namedPipe.WaitForConnectionAsync(cancellationToken);
                        DataExportBusPlugin.ModLogger?.LogInfo("Named pipe client connected");

                        using (var reader = new StreamReader(namedPipe))
                        using (var writer = new StreamWriter(namedPipe) { AutoFlush = true })
                        {
                            // Handle authentication if required (named pipes are local, so this is optional)
                            bool isAuthenticated = string.IsNullOrEmpty(authToken);
                            if (!isAuthenticated)
                            {
                                await writer.WriteLineAsync("AUTH_REQUIRED");

                                string authCommand = await reader.ReadLineAsync();
                                if (authCommand != null && authCommand.StartsWith("AUTH "))
                                {
                                    string providedToken = authCommand.Substring(5).Trim();
                                    if (providedToken == authToken)
                                    {
                                        isAuthenticated = true;
                                        await writer.WriteLineAsync("AUTH_SUCCESS");
                                        DataExportBusPlugin.ModLogger?.LogInfo("Named pipe client authenticated");
                                    }
                                    else
                                    {
                                        await writer.WriteLineAsync("AUTH_FAILED");
                                        DataExportBusPlugin.ModLogger?.LogWarning("Named pipe client authentication failed");
                                        continue;
                                    }
                                }
                                else
                                {
                                    await writer.WriteLineAsync("AUTH_FAILED");
                                    continue;
                                }
                            }

                            while (namedPipe.IsConnected && !cancellationToken.IsCancellationRequested)
                            {
                                string command = await reader.ReadLineAsync();
                                if (command != null)
                                {
                                    string response = ProcessLiveSplitCommand(command);
                                    await writer.WriteLineAsync(response);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DataExportBusPlugin.ModLogger?.LogError($"Named pipe error: {ex.Message}");
                    }
                    finally
                    {
                        namedPipe?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"Named pipe server error: {ex}");
            }
        }

        private async Task HandleClient(TcpClient client, CancellationToken cancellationToken)
        {
            lock (clientLock)
            {
                // Limit the number of connected clients
                if (connectedClients.Count >= MAX_CONNECTED_CLIENTS)
                {
                    DataExportBusPlugin.ModLogger?.LogWarning($"Maximum client limit ({MAX_CONNECTED_CLIENTS}) reached. Closing oldest client.");
                    var oldestClient = connectedClients[0];
                    connectedClients.RemoveAt(0);
                    try
                    {
                        oldestClient?.Close();
                        oldestClient?.Dispose();
                    }
                    catch { }
                }

                connectedClients.Add(client);
            }

            try
            {
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream))
                using (var writer = new StreamWriter(stream) { AutoFlush = true })
                {
                    DataExportBusPlugin.ModLogger?.LogInfo($"TCP client connected from {client.Client.RemoteEndPoint}");

                    // Handle authentication if required
                    bool isAuthenticated = string.IsNullOrEmpty(authToken);
                    if (!isAuthenticated)
                    {
                        await writer.WriteLineAsync("AUTH_REQUIRED");

                        // Wait for authentication
                        var authTask = reader.ReadLineAsync();
                        var authResult = await Task.WhenAny(authTask, Task.Delay(5000, cancellationToken));

                        if (authResult == authTask)
                        {
                            string authCommand = await authTask;
                            if (authCommand != null && authCommand.StartsWith("AUTH "))
                            {
                                string providedToken = authCommand.Substring(5).Trim();
                                if (providedToken == authToken)
                                {
                                    isAuthenticated = true;
                                    await writer.WriteLineAsync("AUTH_SUCCESS");
                                    DataExportBusPlugin.ModLogger?.LogInfo($"TCP client authenticated from {client.Client.RemoteEndPoint}");
                                }
                                else
                                {
                                    await writer.WriteLineAsync("AUTH_FAILED");
                                    DataExportBusPlugin.ModLogger?.LogWarning($"TCP client authentication failed from {client.Client.RemoteEndPoint}");
                                    return;
                                }
                            }
                            else
                            {
                                await writer.WriteLineAsync("AUTH_FAILED");
                                return;
                            }
                        }
                        else
                        {
                            await writer.WriteLineAsync("AUTH_TIMEOUT");
                            return;
                        }
                    }

                    // Send initial connection message
                    await writer.WriteLineAsync($"Connected to Data Export Bus v{PluginInfo.PLUGIN_VERSION}");

                    while (client.Connected && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var readTask = reader.ReadLineAsync();
                            var completedTask = await Task.WhenAny(readTask, Task.Delay(100, cancellationToken));

                            if (completedTask == readTask)
                            {
                                string command = await readTask;
                                if (command == null)
                                    break;

                                string response = ProcessLiveSplitCommand(command.Trim());
                                await writer.WriteLineAsync(response);
                            }
                        }
                        catch (IOException)
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"Client handler error: {ex.Message}");
            }
            finally
            {
                lock (clientLock)
                {
                    connectedClients.Remove(client);
                }
                client.Close();
            }
        }

        private void AddSplitTime(TimeSpan time)
        {
            // Enforce maximum split times to prevent unbounded growth
            if (splitTimes.Count >= MAX_SPLIT_TIMES)
            {
                DataExportBusPlugin.ModLogger?.LogWarning($"Maximum split times ({MAX_SPLIT_TIMES}) reached. Removing oldest.");
                splitTimes.RemoveAt(0);
            }
            splitTimes.Add(time);
        }

        public string ProcessLiveSplitCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return "";

            // Parse command and parameters
            string[] parts = command.Split(' ');
            string cmd = parts[0].ToLower();
            string param = parts.Length > 1 ? string.Join(" ", parts, 1, parts.Length - 1) : "";

            DataExportBusPlugin.ModLogger?.LogDebug($"Processing LiveSplit command: {cmd}");

            switch (cmd)
            {
                case "starttimer":
                    timerStartTime = DateTime.UtcNow;
                    timerRunning = true;
                    currentSplit = 0;
                    splitTimes.Clear();
                    BroadcastToClients("Event: Timer Started");
                    return "OK";

                case "startorsplit":
                    if (!timerRunning)
                    {
                        timerStartTime = DateTime.UtcNow;
                        timerRunning = true;
                        currentSplit = 0;
                        splitTimes.Clear();
                        BroadcastToClients("Event: Timer Started");
                    }
                    else
                    {
                        var splitTime = DateTime.UtcNow - timerStartTime;
                        AddSplitTime(splitTime);
                        currentSplit++;
                        BroadcastToClients($"Event: Split {currentSplit} - {splitTime:hh\\:mm\\:ss\\.fff}");
                    }
                    return "OK";

                case "split":
                    if (timerRunning)
                    {
                        var splitTime = DateTime.UtcNow - timerStartTime;
                        AddSplitTime(splitTime);
                        currentSplit++;
                        BroadcastToClients($"Event: Split {currentSplit} - {splitTime:hh\\:mm\\:ss\\.fff}");
                        return "OK";
                    }
                    return "Error: Timer not running";

                case "unsplit":
                    if (currentSplit > 0)
                    {
                        currentSplit--;
                        if (splitTimes.Count > 0)
                            splitTimes.RemoveAt(splitTimes.Count - 1);
                        BroadcastToClients($"Event: Unsplit to {currentSplit}");
                        return "OK";
                    }
                    return "Error: No splits to undo";

                case "skipsplit":
                    if (timerRunning)
                    {
                        currentSplit++;
                        AddSplitTime(TimeSpan.Zero); // Add zero time for skipped split
                        BroadcastToClients($"Event: Split {currentSplit} skipped");
                        return "OK";
                    }
                    return "Error: Timer not running";

                case "pause":
                case "pausegametime":
                    timerRunning = false;
                    BroadcastToClients("Event: Timer Paused");
                    return "OK";

                case "resume":
                case "unpausegametime":
                    timerRunning = true;
                    BroadcastToClients("Event: Timer Resumed");
                    return "OK";

                case "reset":
                    timerRunning = false;
                    currentSplit = 0;
                    splitTimes.Clear();
                    BroadcastToClients("Event: Timer Reset");
                    return "OK";

                case "initgametime":
                    // Initialize game time (for auto-splitters)
                    return "OK";

                case "setgametime":
                    // Set game time to specific value
                    if (!string.IsNullOrEmpty(param))
                    {
                        BroadcastToClients($"Event: Game Time Set to {param}");
                        return "OK";
                    }
                    return "Error: Time parameter required";

                case "setloadingtimes":
                    // Set loading times for removal
                    return "OK";

                case "pauseloadingtimes":
                    // Pause loading time tracking
                    return "OK";

                case "unpauseloadingtimes":
                    // Resume loading time tracking
                    return "OK";

                case "getcurrenttime":
                    if (timerRunning)
                    {
                        var currentTime = DateTime.UtcNow - timerStartTime;
                        return currentTime.ToString(@"hh\:mm\:ss\.fff");
                    }
                    return "00:00:00.000";

                case "getcurrenttimerphase":
                    if (timerRunning)
                        return "Running";
                    else if (splitTimes.Count > 0)
                        return "Ended";
                    else
                        return "NotRunning";

                case "getsplitindex":
                    return currentSplit.ToString();

                case "getlastruntime":
                    if (splitTimes.Count > 0)
                        return splitTimes[splitTimes.Count - 1].ToString(@"hh\:mm\:ss\.fff");
                    return "00:00:00.000";

                case "getcomparison":
                    return param; // Return the comparison name

                case "ping":
                    return "pong";

                case "help":
                    return "Available commands: starttimer, split, unsplit, skipsplit, pause, resume, reset, getcurrenttime, getsplitindex";

                default:
                    return $"Unknown command: {cmd}";
            }
        }

        public void SendMetric(GameMetric metric)
        {
            // Convert metric to LiveSplit-compatible format if needed
            if (metric.EventType == "scene_transition")
            {
                // Auto-split on scene transitions if configured
                string sceneName = metric.Data.ContainsKey("scene_name") ? metric.Data["scene_name"].ToString() : "";
                if (ShouldAutoSplit(sceneName))
                {
                    ProcessLiveSplitCommand("split");
                }
            }
            else if (metric.EventType == "boss_event")
            {
                string eventType = metric.Data.ContainsKey("event_type") ? metric.Data["event_type"].ToString() : "";
                if (eventType == "defeat")
                {
                    ProcessLiveSplitCommand("split");
                }
            }

            // Send metric data to all connected clients
            string json = JsonConvert.SerializeObject(metric);
            BroadcastToClients($"DATA: {json}");
        }

        private bool ShouldAutoSplit(string sceneName)
        {
            // Get auto-split scenes from configuration
            var configScenes = DataExportBusPlugin.Instance?.AutoSplitScenes?.Value;
            if (string.IsNullOrEmpty(configScenes))
                return false;

            // Parse comma-separated list
            var autoSplitScenes = new HashSet<string>(
                configScenes.Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s)),
                StringComparer.OrdinalIgnoreCase
            );

            return autoSplitScenes.Contains(sceneName);
        }

        private void BroadcastToClients(string message)
        {
            lock (clientLock)
            {
                var disconnectedClients = new List<TcpClient>();

                foreach (var client in connectedClients)
                {
                    try
                    {
                        if (client.Connected)
                        {
                            var stream = client.GetStream();
                            var writer = new StreamWriter(stream) { AutoFlush = true };
                            writer.WriteLine(message);
                        }
                        else
                        {
                            disconnectedClients.Add(client);
                        }
                    }
                    catch
                    {
                        disconnectedClients.Add(client);
                    }
                }

                // Remove disconnected clients
                foreach (var client in disconnectedClients)
                {
                    connectedClients.Remove(client);
                    client.Close();
                }
            }
        }

        public void Stop()
        {
            isRunning = false;

            try
            {
                tcpListener?.Stop();
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"Error stopping TCP listener: {ex}");
            }

            try
            {
                namedPipe?.Dispose();
                namedPipe = null;
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"Error disposing named pipe: {ex}");
            }

            lock (clientLock)
            {
                foreach (var client in connectedClients)
                {
                    try
                    {
                        client?.Close();
                        client?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        DataExportBusPlugin.ModLogger?.LogWarning($"Error closing TCP client during shutdown: {ex.Message}");
                    }
                }
                connectedClients.Clear();
            }
        }

        private bool _disposed = false;

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
                Stop();
            }

            _disposed = true;
        }
    }
}