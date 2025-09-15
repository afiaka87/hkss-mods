using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HKSS.DataExportBus
{
    public class WebSocketServer
    {
        private HttpListener listener;
        private readonly int port;
        private readonly string authToken;
        private readonly string[] allowedOrigins;
        private readonly List<WebSocketClient> connectedClients = new List<WebSocketClient>();
        private readonly object clientLock = new object();
        private bool isRunning = false;
        private Timer pingTimer;
        private const int PING_INTERVAL_MS = 30000; // Ping every 30 seconds
        private const int PING_TIMEOUT_MS = 10000; // 10 second timeout for pong

        // Simplified OBS-like scene management (not full OBS 5.0 protocol)
        private readonly Dictionary<string, object> obsSceneData = new Dictionary<string, object>();

        public WebSocketServer(int port, string authToken, string allowedOrigins)
        {
            this.port = port;
            this.authToken = authToken;
            this.allowedOrigins = string.IsNullOrEmpty(allowedOrigins) ? new[] { "*" } : allowedOrigins.Split(',');
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");
                listener.Start();
                isRunning = true;

                // Start ping timer
                pingTimer = new Timer(_ => SendPingsToAllClients(), null, PING_INTERVAL_MS, PING_INTERVAL_MS);

                DataExportBusPlugin.ModLogger?.LogInfo($"WebSocket server listening on ws://localhost:{port}/");

                while (!cancellationToken.IsCancellationRequested && isRunning)
                {
                    try
                    {
                        var contextTask = listener.GetContextAsync();
                        var completedTask = await Task.WhenAny(contextTask, Task.Delay(-1, cancellationToken));

                        if (completedTask == contextTask)
                        {
                            var context = await contextTask;
                            _ = Task.Run(() => HandleWebSocketConnection(context, cancellationToken), cancellationToken);
                        }
                    }
                    catch (HttpListenerException ex) when (ex.ErrorCode == 995)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        DataExportBusPlugin.ModLogger?.LogError($"WebSocket server error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"Failed to start WebSocket server: {ex}");
            }
        }

        private async Task HandleWebSocketConnection(HttpListenerContext context, CancellationToken cancellationToken)
        {
            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                return;
            }

            // Check origin
            var origin = context.Request.Headers["Origin"];
            if (!string.IsNullOrEmpty(origin) && !IsAllowedOrigin(origin))
            {
                context.Response.StatusCode = 403;
                context.Response.Close();
                return;
            }

            // Check authentication header if auth is enabled
            if (!string.IsNullOrEmpty(authToken))
            {
                var providedAuth = context.Request.Headers["Authorization"];
                if (providedAuth != $"Bearer {authToken}")
                {
                    context.Response.StatusCode = 401;
                    context.Response.Close();
                    return;
                }
            }

            WebSocketContext webSocketContext = null;
            WebSocketClient client = null;

            try
            {
                webSocketContext = await context.AcceptWebSocketAsync(null);
                client = new WebSocketClient
                {
                    Id = Guid.NewGuid().ToString(),
                    Socket = webSocketContext.WebSocket,
                    IsAuthenticated = string.IsNullOrEmpty(authToken) // Auto-auth if no token required
                };

                lock (clientLock)
                {
                    connectedClients.Add(client);
                }

                DataExportBusPlugin.ModLogger?.LogInfo($"WebSocket client connected: {client.Id}");

                // Send initial hello message
                await SendMessage(client, new
                {
                    type = "hello",
                    version = PluginInfo.PLUGIN_VERSION,
                    authentication = !string.IsNullOrEmpty(authToken)
                });

                // Handle messages
                await HandleClientMessages(client, cancellationToken);
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"WebSocket connection error: {ex.Message}");
            }
            finally
            {
                if (client != null)
                {
                    lock (clientLock)
                    {
                        connectedClients.Remove(client);
                    }
                }

                webSocketContext?.WebSocket?.Dispose();
            }
        }

        private async Task HandleClientMessages(WebSocketClient client, CancellationToken cancellationToken)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);

            while (client.Socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await client.Socket.ReceiveAsync(buffer, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                        await ProcessClientMessage(client, message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await client.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cancellationToken);
                        break;
                    }
                }
                catch (WebSocketException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    DataExportBusPlugin.ModLogger?.LogError($"Error handling WebSocket message: {ex.Message}");
                }
            }
        }

        private async Task ProcessClientMessage(WebSocketClient client, string message)
        {
            try
            {
                var json = JObject.Parse(message);
                string messageType = json["type"]?.ToString() ?? json["op"]?.ToString() ?? "";
                string requestId = json["id"]?.ToString() ?? json["requestId"]?.ToString() ?? "";

                DataExportBusPlugin.ModLogger?.LogDebug($"WebSocket message received: {messageType}");

                // Check authentication for non-auth messages
                if (!client.IsAuthenticated && messageType.ToLower() != "auth" && messageType.ToLower() != "authenticate")
                {
                    await SendMessage(client, new { type = "error", message = "Authentication required" });
                    return;
                }

                switch (messageType.ToLower())
                {
                    case "auth":
                    case "authenticate":
                        await HandleAuthentication(client, json);
                        break;

                    case "subscribe":
                        await HandleSubscribe(client, json);
                        break;

                    case "unsubscribe":
                        await HandleUnsubscribe(client, json);
                        break;

                    case "ping":
                        await SendMessage(client, new { type = "pong", timestamp = DateTime.UtcNow });
                        break;

                    case "pong":
                        client.LastPongReceived = DateTime.UtcNow;
                        client.PingPending = false;
                        break;

                    // Simplified OBS-like commands (not full OBS 5.0 protocol)
                    // For actual OBS integration, use the game state events instead
                    case "getversion":
                        await SendOBSResponse(client, requestId, new
                        {
                            obsWebSocketVersion = "simplified-1.0",
                            rpcVersion = 1,
                            supportedImageFormats = new[] { "png", "jpg", "jpeg", "gif" },
                            note = "This is a simplified OBS-like API, not full OBS 5.0 protocol"
                        });
                        break;

                    case "getscenelist":
                        await SendOBSResponse(client, requestId, new
                        {
                            currentProgramSceneName = GetCurrentSceneName(),
                            scenes = GetSceneList()
                        });
                        break;

                    case "setcurrentprogramscene":
                        string sceneName = json["sceneName"]?.ToString();
                        if (!string.IsNullOrEmpty(sceneName))
                        {
                            SetCurrentScene(sceneName);
                            await SendOBSResponse(client, requestId, new { });
                        }
                        break;

                    case "getsourcesettings":
                        string sourceName = json["sourceName"]?.ToString();
                        await SendOBSResponse(client, requestId, new
                        {
                            sourceName = sourceName,
                            sourceSettings = GetSourceSettings(sourceName)
                        });
                        break;

                    case "setsourcesettings":
                        await HandleSetSourceSettings(client, json, requestId);
                        break;

                    default:
                        if (!string.IsNullOrEmpty(requestId))
                        {
                            await SendOBSError(client, requestId, $"Unknown request type: {messageType}");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"Error processing WebSocket message: {ex}");
            }
        }

        private async Task HandleAuthentication(WebSocketClient client, JObject json)
        {
            string providedToken = json["token"]?.ToString() ?? json["authentication"]?.ToString() ?? "";

            if (providedToken == authToken)
            {
                client.IsAuthenticated = true;
                await SendMessage(client, new { type = "authenticated", success = true });
            }
            else
            {
                await SendMessage(client, new { type = "authenticated", success = false, error = "Invalid token" });
            }
        }

        private async Task HandleSubscribe(WebSocketClient client, JObject json)
        {
            var events = json["events"]?.ToObject<List<string>>() ?? new List<string>();

            foreach (var eventType in events)
            {
                if (!client.SubscribedEvents.Contains(eventType))
                {
                    client.SubscribedEvents.Add(eventType);
                }
            }

            await SendMessage(client, new { type = "subscribed", events = client.SubscribedEvents });
        }

        private async Task HandleUnsubscribe(WebSocketClient client, JObject json)
        {
            var events = json["events"]?.ToObject<List<string>>() ?? new List<string>();

            foreach (var eventType in events)
            {
                client.SubscribedEvents.Remove(eventType);
            }

            await SendMessage(client, new { type = "unsubscribed", events = client.SubscribedEvents });
        }

        private async Task HandleSetSourceSettings(WebSocketClient client, JObject json, string requestId)
        {
            string sourceName = json["sourceName"]?.ToString();
            var settings = json["sourceSettings"];

            if (!string.IsNullOrEmpty(sourceName) && settings != null)
            {
                // Store settings for the source
                obsSceneData[sourceName] = settings;

                // If it's a text source, we might want to update it with game data
                if (sourceName.ToLower().Contains("text"))
                {
                    UpdateTextSource(sourceName, settings);
                }

                await SendOBSResponse(client, requestId, new { });
            }
        }

        private void UpdateTextSource(string sourceName, object settings)
        {
            // This would be called to update OBS text sources with game data
            var collector = DataExportBusPlugin.Instance?.GetMetricsCollector();
            if (collector != null)
            {
                var state = collector.GetCurrentState();
                // Update the text source with current game state
            }
        }

        private string GetCurrentSceneName()
        {
            // Map game state to OBS scene names
            var collector = DataExportBusPlugin.Instance?.GetMetricsCollector();
            if (collector != null)
            {
                var state = collector.GetCurrentState();
                if (state.ContainsKey("current_scene"))
                {
                    string gameScene = state["current_scene"].ToString();
                    // Map game scenes to OBS scenes
                    if (gameScene.Contains("Boss"))
                        return "Boss Fight";
                    else if (gameScene.Contains("Menu"))
                        return "Main Menu";
                    else
                        return "Gameplay";
                }
            }
            return "Gameplay";
        }

        private object GetSceneList()
        {
            return new[]
            {
                new { sceneName = "Gameplay", sceneIndex = 0 },
                new { sceneName = "Boss Fight", sceneIndex = 1 },
                new { sceneName = "Main Menu", sceneIndex = 2 },
                new { sceneName = "Cutscene", sceneIndex = 3 },
                new { sceneName = "Death Screen", sceneIndex = 4 }
            };
        }

        private void SetCurrentScene(string sceneName)
        {
            // This would trigger scene changes in OBS
            obsSceneData["currentScene"] = sceneName;

            // Broadcast scene change event
            BroadcastToClients(new
            {
                type = "event",
                eventType = "CurrentProgramSceneChanged",
                eventData = new { sceneName = sceneName }
            });
        }

        private object GetSourceSettings(string sourceName)
        {
            if (obsSceneData.ContainsKey(sourceName))
                return obsSceneData[sourceName];

            // Return default settings
            return new
            {
                text = "",
                font = new { face = "Arial", size = 32 },
                color = 0xFFFFFF
            };
        }

        private async Task SendOBSResponse(WebSocketClient client, string requestId, object data)
        {
            await SendMessage(client, new
            {
                op = 7, // Response opcode for OBS WebSocket
                d = new
                {
                    requestType = "Response",
                    requestId = requestId,
                    requestStatus = new { result = true, code = 100 },
                    responseData = data
                }
            });
        }

        private async Task SendOBSError(WebSocketClient client, string requestId, string error)
        {
            await SendMessage(client, new
            {
                op = 7,
                d = new
                {
                    requestType = "Response",
                    requestId = requestId,
                    requestStatus = new { result = false, code = 204, comment = error }
                }
            });
        }

        public void BroadcastMetric(GameMetric metric)
        {
            // Convert to WebSocket event format
            var eventData = new
            {
                type = "metric",
                timestamp = metric.Timestamp,
                eventType = metric.EventType,
                data = metric.Data
            };

            BroadcastToClients(eventData);

            // Also send as NDJSON for streaming clients
            string ndjson = JsonConvert.SerializeObject(metric, Formatting.None) + "\n";
            BroadcastRawMessage(ndjson);

            // Trigger OBS scene changes based on game events
            HandleOBSAutomation(metric);
        }

        private void HandleOBSAutomation(GameMetric metric)
        {
            // Automatically switch OBS scenes based on game events
            if (metric.EventType == "boss_event")
            {
                string eventType = metric.Data.ContainsKey("event_type") ? metric.Data["event_type"].ToString() : "";
                if (eventType == "start")
                {
                    SetCurrentScene("Boss Fight");
                }
                else if (eventType == "defeat")
                {
                    SetCurrentScene("Gameplay");
                }
            }
            else if (metric.EventType == "player_damaged")
            {
                int health = metric.Data.ContainsKey("health_remaining") ? Convert.ToInt32(metric.Data["health_remaining"]) : 0;
                if (health == 0)
                {
                    SetCurrentScene("Death Screen");
                }
            }
        }

        private void BroadcastToClients(object message)
        {
            string json = JsonConvert.SerializeObject(message);
            BroadcastRawMessage(json);
        }

        private void BroadcastRawMessage(string message)
        {
            lock (clientLock)
            {
                var disconnectedClients = new List<WebSocketClient>();

                foreach (var client in connectedClients)
                {
                    if (!client.IsAuthenticated && !string.IsNullOrEmpty(authToken))
                        continue;

                    Task.Run(async () =>
                    {
                        try
                        {
                            if (client.Socket.State == WebSocketState.Open)
                            {
                                var buffer = Encoding.UTF8.GetBytes(message);
                                await client.Socket.SendAsync(
                                    new ArraySegment<byte>(buffer),
                                    WebSocketMessageType.Text,
                                    true,
                                    CancellationToken.None
                                );
                            }
                            else
                            {
                                lock (clientLock)
                                {
                                    disconnectedClients.Add(client);
                                }
                            }
                        }
                        catch
                        {
                            lock (clientLock)
                            {
                                disconnectedClients.Add(client);
                            }
                        }
                    });
                }

                foreach (var client in disconnectedClients)
                {
                    connectedClients.Remove(client);
                }
            }
        }

        private async Task SendMessage(WebSocketClient client, object message)
        {
            try
            {
                if (client.Socket.State == WebSocketState.Open)
                {
                    string json = JsonConvert.SerializeObject(message);
                    var buffer = Encoding.UTF8.GetBytes(json);
                    await client.Socket.SendAsync(
                        new ArraySegment<byte>(buffer),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None
                    );
                }
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"Error sending WebSocket message: {ex.Message}");
            }
        }

        private void SendPingsToAllClients()
        {
            try
            {
                List<WebSocketClient> clientsToRemove = new List<WebSocketClient>();

                lock (clientLock)
                {
                    foreach (var client in connectedClients.ToList())
                    {
                        // Check if client has timed out
                        if (client.PingPending && (DateTime.UtcNow - client.LastPongReceived).TotalMilliseconds > PING_TIMEOUT_MS)
                        {
                            clientsToRemove.Add(client);
                            DataExportBusPlugin.ModLogger?.LogInfo($"WebSocket client {client.Id} timed out");
                            continue;
                        }

                        // Send ping
                        if (client.Socket.State == WebSocketState.Open)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await SendMessage(client, new { type = "ping", timestamp = DateTime.UtcNow });
                                    client.PingPending = true;
                                }
                                catch { }
                            });
                        }
                    }

                    // Remove timed out clients
                    foreach (var client in clientsToRemove)
                    {
                        connectedClients.Remove(client);
                        client.Socket?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"Error in ping timer: {ex.Message}");
            }
        }

        public void Stop()
        {
            isRunning = false;
            pingTimer?.Dispose();
            listener?.Stop();
            listener?.Close();

            lock (clientLock)
            {
                foreach (var client in connectedClients)
                {
                    client.Socket?.Dispose();
                }
                connectedClients.Clear();
            }
        }

        private bool IsAllowedOrigin(string origin)
        {
            if (allowedOrigins == null || allowedOrigins.Length == 0)
                return true;

            foreach (var allowed in allowedOrigins)
            {
                if (allowed == "*" || string.Equals(allowed, origin, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private class WebSocketClient
        {
            public string Id { get; set; }
            public WebSocket Socket { get; set; }
            public bool IsAuthenticated { get; set; }
            public List<string> SubscribedEvents { get; set; } = new List<string>();
            public DateTime LastPongReceived { get; set; } = DateTime.UtcNow;
            public bool PingPending { get; set; } = false;
        }
    }
}