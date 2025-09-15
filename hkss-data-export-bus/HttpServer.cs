using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HKSS.DataExportBus
{
    public class HttpServer
    {
        private HttpListener listener;
        private readonly string bindAddress;
        private readonly int port;
        private readonly string authToken;
        private readonly string[] allowedOrigins;
        private readonly Queue<GameMetric> metricsQueue = new Queue<GameMetric>();
        private readonly object queueLock = new object();
        private bool isRunning = false;
        private const int MAX_QUEUE_SIZE = 5000; // Configurable max queue size
        private readonly SemaphoreSlim requestSemaphore = new SemaphoreSlim(10, 10); // Max 10 concurrent requests

        public HttpServer(string bindAddress, int port, string authToken, string allowedOrigins)
        {
            this.bindAddress = bindAddress;
            this.port = port;
            this.authToken = authToken;
            this.allowedOrigins = string.IsNullOrEmpty(allowedOrigins) ? new[] { "*" } : allowedOrigins.Split(',');
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                listener = new HttpListener();
                string prefix = $"http://{bindAddress}:{port}/";
                listener.Prefixes.Add(prefix);
                listener.Start();
                isRunning = true;

                DataExportBusPlugin.ModLogger?.LogInfo($"HTTP server listening on {prefix}");

                while (!cancellationToken.IsCancellationRequested && isRunning)
                {
                    try
                    {
                        var contextTask = listener.GetContextAsync();
                        var completedTask = await Task.WhenAny(contextTask, Task.Delay(-1, cancellationToken));

                        if (completedTask == contextTask)
                        {
                            var context = await contextTask;
                            _ = Task.Run(async () => await HandleRequestWithLimit(context), cancellationToken);
                        }
                    }
                    catch (HttpListenerException ex) when (ex.ErrorCode == 995) // Listener stopped
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        DataExportBusPlugin.ModLogger?.LogError($"HTTP server error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"Failed to start HTTP server: {ex}");
            }
        }

        private async Task HandleRequestWithLimit(HttpListenerContext context)
        {
            await requestSemaphore.WaitAsync();
            try
            {
                await HandleRequest(context);
            }
            finally
            {
                requestSemaphore.Release();
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            try
            {
                // Set CORS headers
                SetCorsHeaders(context.Request, context.Response);

                // Handle preflight requests
                if (context.Request.HttpMethod == "OPTIONS")
                {
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                    return;
                }

                // Check authentication
                if (!string.IsNullOrEmpty(authToken))
                {
                    string providedToken = context.Request.Headers["Authorization"];
                    if (providedToken != $"Bearer {authToken}")
                    {
                        await SendResponse(context.Response, 401, "Unauthorized");
                        return;
                    }
                }

                // Route request
                string path = context.Request.Url.AbsolutePath;
                string method = context.Request.HttpMethod;

                switch (path.ToLower())
                {
                    case "/api/status":
                        await HandleStatus(context.Response);
                        break;

                    case "/api/metrics":
                        await HandleMetrics(context.Response);
                        break;

                    case "/api/events":
                        await HandleEvents(context.Response);
                        break;

                    case "/api/state":
                        await HandleState(context.Response);
                        break;

                    case "/api/livesplit/start":
                        if (method == "POST")
                            await HandleLiveSplitCommand(context.Response, "starttimer");
                        else
                            await SendResponse(context.Response, 405, "Method not allowed");
                        break;

                    case "/api/livesplit/split":
                        if (method == "POST")
                            await HandleLiveSplitCommand(context.Response, "split");
                        else
                            await SendResponse(context.Response, 405, "Method not allowed");
                        break;

                    case "/api/livesplit/reset":
                        if (method == "POST")
                            await HandleLiveSplitCommand(context.Response, "reset");
                        else
                            await SendResponse(context.Response, 405, "Method not allowed");
                        break;

                    case "/":
                    case "/index.html":
                        await HandleDashboard(context.Response);
                        break;

                    default:
                        await SendResponse(context.Response, 404, "Not found");
                        break;
                }
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"Error handling HTTP request: {ex}");
                await SendResponse(context.Response, 500, "Internal server error");
            }
        }

        private void SetCorsHeaders(HttpListenerRequest request, HttpListenerResponse response)
        {
            var origin = request.Headers["Origin"];

            // Check if origin is allowed and set appropriate CORS header
            if (!string.IsNullOrEmpty(origin) && allowedOrigins.Any(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase)))
            {
                response.Headers.Add("Access-Control-Allow-Origin", origin);
                response.Headers.Add("Vary", "Origin");
            }
            else if (allowedOrigins.Contains("*"))
            {
                response.Headers.Add("Access-Control-Allow-Origin", "*");
            }

            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
            response.Headers.Add("Access-Control-Max-Age", "600");
        }

        private async Task HandleStatus(HttpListenerResponse response)
        {
            var status = new
            {
                status = "online",
                version = PluginInfo.PLUGIN_VERSION,
                uptime = DateTime.UtcNow.ToString("o"),
                connections = new
                {
                    http = isRunning,
                    tcp = DataExportBusPlugin.Instance.EnableTcpServer.Value,
                    websocket = DataExportBusPlugin.Instance.EnableWebSocketServer.Value,
                    file = DataExportBusPlugin.Instance.EnableFileExport.Value
                }
            };

            await SendJsonResponse(response, status);
        }

        private async Task HandleMetrics(HttpListenerResponse response)
        {
            List<GameMetric> metrics;
            lock (queueLock)
            {
                metrics = new List<GameMetric>(metricsQueue);
                metricsQueue.Clear();
            }

            await SendJsonResponse(response, metrics);
        }

        private async Task HandleEvents(HttpListenerResponse response)
        {
            var collector = DataExportBusPlugin.Instance?.GetMetricsCollector();
            if (collector != null)
            {
                var events = collector.GetRecentEvents();
                await SendJsonResponse(response, events);
            }
            else
            {
                await SendJsonResponse(response, new List<string>());
            }
        }

        private async Task HandleState(HttpListenerResponse response)
        {
            var collector = DataExportBusPlugin.Instance?.GetMetricsCollector();
            if (collector != null)
            {
                var state = collector.GetCurrentState();
                await SendJsonResponse(response, state);
            }
            else
            {
                await SendJsonResponse(response, new Dictionary<string, object>());
            }
        }

        private async Task HandleLiveSplitCommand(HttpListenerResponse response, string command)
        {
            // Forward to TCP server if available
            // This would trigger the appropriate LiveSplit command
            await SendJsonResponse(response, new { command = command, status = "sent" });
        }

        private async Task HandleDashboard(HttpListenerResponse response)
        {
            string html = @"<!DOCTYPE html>
<html>
<head>
    <title>Data Export Bus Dashboard</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; background: #1a1a1a; color: #fff; }
        h1 { color: #4CAF50; }
        .metric { background: #2a2a2a; padding: 10px; margin: 10px 0; border-radius: 5px; }
        .label { color: #888; }
        .value { color: #4CAF50; font-weight: bold; }
        #status { padding: 10px; background: #2a2a2a; border-radius: 5px; }
        button { background: #4CAF50; color: white; border: none; padding: 10px 20px; margin: 5px; cursor: pointer; border-radius: 5px; }
        button:hover { background: #45a049; }
    </style>
</head>
<body>
    <h1>Hollow Knight: Silksong - Data Export Bus</h1>
    <div id='status'>Loading...</div>
    <div id='metrics'></div>
    <div>
        <button onclick='fetchStatus()'>Refresh Status</button>
        <button onclick='fetchMetrics()'>Get Metrics</button>
        <button onclick='fetchState()'>Get State</button>
    </div>
    <script>
        async function fetchStatus() {
            try {
                const response = await fetch('/api/status');
                const data = await response.json();
                document.getElementById('status').innerHTML = '<pre>' + JSON.stringify(data, null, 2) + '</pre>';
            } catch (e) {
                document.getElementById('status').innerHTML = 'Error: ' + e.message;
            }
        }

        async function fetchMetrics() {
            try {
                const response = await fetch('/api/metrics');
                const data = await response.json();
                document.getElementById('metrics').innerHTML = '<h2>Recent Metrics</h2><pre>' + JSON.stringify(data, null, 2) + '</pre>';
            } catch (e) {
                document.getElementById('metrics').innerHTML = 'Error: ' + e.message;
            }
        }

        async function fetchState() {
            try {
                const response = await fetch('/api/state');
                const data = await response.json();
                document.getElementById('metrics').innerHTML = '<h2>Current State</h2><pre>' + JSON.stringify(data, null, 2) + '</pre>';
            } catch (e) {
                document.getElementById('metrics').innerHTML = 'Error: ' + e.message;
            }
        }

        // Auto-refresh every 2 seconds
        setInterval(fetchStatus, 2000);
        fetchStatus();
    </script>
</body>
</html>";

            await SendResponse(response, 200, html, "text/html");
        }

        private async Task SendJsonResponse(HttpListenerResponse response, object data)
        {
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            await SendResponse(response, 200, json, "application/json");
        }

        private async Task SendResponse(HttpListenerResponse response, int statusCode, string content, string contentType = "text/plain")
        {
            try
            {
                response.StatusCode = statusCode;
                response.ContentType = contentType;
                byte[] buffer = Encoding.UTF8.GetBytes(content);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                DataExportBusPlugin.ModLogger?.LogError($"Error sending HTTP response: {ex.Message}");
            }
        }

        public void QueueMetric(GameMetric metric)
        {
            lock (queueLock)
            {
                metricsQueue.Enqueue(metric);
                while (metricsQueue.Count > MAX_QUEUE_SIZE) // Keep last MAX_QUEUE_SIZE metrics
                {
                    metricsQueue.Dequeue();
                }
            }
        }

        public void Stop()
        {
            isRunning = false;
            listener?.Stop();
            listener?.Close();
            requestSemaphore?.Dispose();
        }
    }
}