using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using Newtonsoft.Json;
using HKSS.DataExportBus.Security;

namespace HKSS.DataExportBus
{
    public class HttpServer : IDisposable
    {
        private readonly ManualLogSource _logger;
        private HttpListener _listener;
        private readonly string _bindAddress;
        private readonly int _port;
        private readonly string _authToken;
        private readonly string[] _allowedOrigins;
        private readonly ConcurrentQueue<GameMetric> _metricsQueue;
        private readonly SemaphoreSlim _requestSemaphore;
        private readonly CancellationTokenSource _shutdownTokenSource;
        private readonly ReaderWriterLockSlim _stateLock;
        private volatile bool _isRunning;
        private bool _disposed;
        private string _dashboardHtml;
        private DateTime _startTime;

        public HttpServer(string bindAddress, int port, string authToken, string allowedOrigins)
        {
            _logger = DataExportBusPlugin.ModLogger;

            // Validate and sanitize inputs
            if (!SecurityValidator.ValidateBindAddress(bindAddress))
            {
                _logger?.LogWarning($"Invalid bind address {bindAddress}, using localhost");
                bindAddress = "localhost";
            }

            if (!SecurityValidator.ValidatePort(port))
            {
                _logger?.LogWarning($"Invalid port {port}, using default");
                port = HKSS.DataExportBus.Configuration.Constants.Network.DEFAULT_HTTP_PORT;
            }

            _bindAddress = bindAddress;
            _port = port;
            _authToken = authToken;
            _allowedOrigins = string.IsNullOrWhiteSpace(allowedOrigins)
                ? new[] { "*" }
                : allowedOrigins.Split(',').Select(o => o.Trim()).ToArray();

            _metricsQueue = new ConcurrentQueue<GameMetric>();
            _requestSemaphore = new SemaphoreSlim(
                HKSS.DataExportBus.Configuration.Constants.Network.MAX_CONCURRENT_HTTP_REQUESTS,
                HKSS.DataExportBus.Configuration.Constants.Network.MAX_CONCURRENT_HTTP_REQUESTS
            );
            _shutdownTokenSource = new CancellationTokenSource();
            _stateLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _startTime = DateTime.UtcNow;

            LoadDashboardHtml();
        }

        private void LoadDashboardHtml()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourcePath = "HKSS.DataExportBus.Resources.dashboard.html";

                // Try embedded resource first
                using (var stream = assembly.GetManifestResourceStream(resourcePath))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            _dashboardHtml = reader.ReadToEnd();
                            _logger?.LogInfo("Loaded dashboard HTML from embedded resource");
                            return;
                        }
                    }
                }

                // Fallback to file system
                var filePath = Path.Combine(Path.GetDirectoryName(assembly.Location), "Resources", "dashboard.html");
                if (File.Exists(filePath))
                {
                    _dashboardHtml = File.ReadAllText(filePath, Encoding.UTF8);
                    _logger?.LogInfo("Loaded dashboard HTML from file");
                    return;
                }

                // Use minimal fallback
                _dashboardHtml = GetFallbackDashboard();
                _logger?.LogWarning("Using fallback dashboard HTML");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to load dashboard HTML: {ex}");
                _dashboardHtml = GetFallbackDashboard();
            }
        }

        private string GetFallbackDashboard()
        {
            return @"<!DOCTYPE html>
<html>
<head><title>Data Export Bus</title></head>
<body>
<h1>Data Export Bus</h1>
<p>Dashboard resources not found. API endpoints are still available:</p>
<ul>
<li>/api/status</li>
<li>/api/metrics</li>
<li>/api/state</li>
<li>/api/events</li>
</ul>
</body>
</html>";
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HttpServer));

            try
            {
                _listener = new HttpListener();

                // Security: Only bind to localhost unless explicitly configured
                string safeBindAddress = _bindAddress;
                if (safeBindAddress == "0.0.0.0" || safeBindAddress == "*")
                {
                    _logger?.LogWarning("Binding to all interfaces is a security risk. Consider using 'localhost' instead.");
                }

                string prefix = $"http://{safeBindAddress}:{_port}/";
                _listener.Prefixes.Add(prefix);
                _listener.Start();
                _isRunning = true;

                _logger?.LogInfo($"HTTP server listening on {prefix}");

                using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _shutdownTokenSource.Token))
                {
                    while (!linkedTokenSource.Token.IsCancellationRequested && _isRunning)
                    {
                        try
                        {
                            var contextTask = _listener.GetContextAsync();
                            var completedTask = await Task.WhenAny(
                                contextTask,
                                Task.Delay(-1, linkedTokenSource.Token)
                            ).ConfigureAwait(false);

                            if (completedTask == contextTask)
                            {
                                var context = await contextTask.ConfigureAwait(false);

                                // Handle request with proper async pattern
                                var requestTask = HandleRequestWithLimitAsync(context, linkedTokenSource.Token);

                                // Fire and forget with error logging
                                _ = requestTask.ContinueWith(t =>
                                {
                                    if (t.IsFaulted)
                                    {
                                        _logger?.LogError($"Request handling failed: {t.Exception?.GetBaseException()}");
                                    }
                                }, TaskScheduler.Default);
                            }
                        }
                        catch (HttpListenerException ex) when (ex.ErrorCode == 995) // Listener stopped
                        {
                            _logger?.LogInfo("HTTP listener stopped gracefully");
                            break;
                        }
                        catch (OperationCanceledException)
                        {
                            _logger?.LogInfo("HTTP server shutdown requested");
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError($"HTTP server error: {ex}");
                            await Task.Delay(1000, linkedTokenSource.Token).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to start HTTP server: {ex}");
                throw;
            }
            finally
            {
                _isRunning = false;
            }
        }

        private async Task HandleRequestWithLimitAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            await _requestSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await HandleRequestAsync(context, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _requestSemaphore.Release();
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            try
            {
                // Check request size limit
                if (context.Request.ContentLength64 > HKSS.DataExportBus.Configuration.Constants.Network.MAX_REQUEST_SIZE_BYTES)
                {
                    _logger?.LogWarning($"Request too large: {context.Request.ContentLength64} bytes");
                    await SendResponseAsync(context.Response, 413, HKSS.DataExportBus.Configuration.Constants.ErrorMessages.INTERNAL_SERVER_ERROR).ConfigureAwait(false);
                    return;
                }

                // Rate limiting by IP
                string clientIp = context.Request.RemoteEndPoint?.Address?.ToString() ?? "unknown";
                if (!SecurityValidator.CheckRateLimit(clientIp,
                    HKSS.DataExportBus.Configuration.Constants.RateLimit.MAX_REQUESTS_PER_WINDOW,
                    TimeSpan.FromSeconds(HKSS.DataExportBus.Configuration.Constants.RateLimit.RATE_LIMIT_WINDOW_SECONDS)))
                {
                    _logger?.LogWarning($"Rate limit exceeded for {clientIp}");
                    await SendResponseAsync(context.Response, 429, "Too many requests").ConfigureAwait(false);
                    return;
                }

                // Set CORS headers
                SetCorsHeaders(context.Request, context.Response);

                // Handle preflight requests
                if (context.Request.HttpMethod == HKSS.DataExportBus.Configuration.Constants.HttpMethods.OPTIONS)
                {
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                    return;
                }

                // Check authentication
                if (!string.IsNullOrWhiteSpace(_authToken))
                {
                    string providedToken = context.Request.Headers["Authorization"];
                    string expectedToken = $"{HKSS.DataExportBus.Configuration.Constants.Http.AUTH_HEADER_PREFIX}{_authToken}";

                    if (!SecurityValidator.SecureCompareStrings(providedToken, expectedToken))
                    {
                        _logger?.LogWarning($"Authentication failed from {clientIp}");
                        await SendResponseAsync(context.Response, 401, HKSS.DataExportBus.Configuration.Constants.ErrorMessages.UNAUTHORIZED).ConfigureAwait(false);
                        return;
                    }
                }

                // Route request with sanitized path
                string path = SecurityValidator.SanitizeCommand(context.Request.Url.AbsolutePath);
                string method = context.Request.HttpMethod;

                // Log request
                _logger?.LogInfo($"HTTP {method} {path} from {clientIp}");

                switch (path.ToLowerInvariant())
                {
                    case "/api/status":
                        await HandleStatusAsync(context.Response, cancellationToken).ConfigureAwait(false);
                        break;

                    case "/api/metrics":
                        await HandleMetricsAsync(context.Response, cancellationToken).ConfigureAwait(false);
                        break;

                    case "/api/events":
                        await HandleEventsAsync(context.Response, cancellationToken).ConfigureAwait(false);
                        break;

                    case "/api/state":
                        await HandleStateAsync(context.Response, cancellationToken).ConfigureAwait(false);
                        break;

                    case "/api/livesplit/start":
                        if (method == HKSS.DataExportBus.Configuration.Constants.HttpMethods.POST)
                            await HandleLiveSplitCommandAsync(context.Response, "starttimer", cancellationToken).ConfigureAwait(false);
                        else
                            await SendResponseAsync(context.Response, 405, HKSS.DataExportBus.Configuration.Constants.ErrorMessages.METHOD_NOT_ALLOWED).ConfigureAwait(false);
                        break;

                    case "/api/livesplit/split":
                        if (method == HKSS.DataExportBus.Configuration.Constants.HttpMethods.POST)
                            await HandleLiveSplitCommandAsync(context.Response, "split", cancellationToken).ConfigureAwait(false);
                        else
                            await SendResponseAsync(context.Response, 405, HKSS.DataExportBus.Configuration.Constants.ErrorMessages.METHOD_NOT_ALLOWED).ConfigureAwait(false);
                        break;

                    case "/api/livesplit/reset":
                        if (method == HKSS.DataExportBus.Configuration.Constants.HttpMethods.POST)
                            await HandleLiveSplitCommandAsync(context.Response, "reset", cancellationToken).ConfigureAwait(false);
                        else
                            await SendResponseAsync(context.Response, 405, HKSS.DataExportBus.Configuration.Constants.ErrorMessages.METHOD_NOT_ALLOWED).ConfigureAwait(false);
                        break;

                    case "/":
                    case "/index.html":
                        await HandleDashboard(context.Response).ConfigureAwait(false);
                        break;

                    default:
                        await SendResponseAsync(context.Response, 404, HKSS.DataExportBus.Configuration.Constants.ErrorMessages.NOT_FOUND).ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error handling HTTP request: {ex}");
                await SendResponseAsync(context.Response, 500, HKSS.DataExportBus.Configuration.Constants.ErrorMessages.INTERNAL_SERVER_ERROR).ConfigureAwait(false);
            }
        }

        private void SetCorsHeaders(HttpListenerRequest request, HttpListenerResponse response)
        {
            var origin = request.Headers["Origin"];

            // Validate origin using SecurityValidator
            if (SecurityValidator.ValidateOrigin(origin, _allowedOrigins))
            {
                if (_allowedOrigins.Contains("*"))
                {
                    response.Headers.Add("Access-Control-Allow-Origin", "*");
                }
                else
                {
                    response.Headers.Add("Access-Control-Allow-Origin", origin);
                    response.Headers.Add("Vary", "Origin");
                }
            }

            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
            response.Headers.Add("Access-Control-Max-Age", HKSS.DataExportBus.Configuration.Constants.Http.CORS_MAX_AGE);
        }

        private async Task HandleStatusAsync(HttpListenerResponse response, CancellationToken cancellationToken)
        {
            var uptime = DateTime.UtcNow - _startTime;
            var status = new
            {
                status = "online",
                version = PluginInfo.PLUGIN_VERSION,
                uptime = uptime.ToString(@"dd\.hh\:mm\:ss"),
                startTime = _startTime.ToString(HKSS.DataExportBus.Configuration.Constants.FileExport.ISO_DATETIME_FORMAT),
                connections = new
                {
                    http = _isRunning,
                    tcp = DataExportBusPlugin.Instance?.EnableTcpServer?.Value ?? false,
                    websocket = DataExportBusPlugin.Instance?.EnableWebSocketServer?.Value ?? false,
                    file = DataExportBusPlugin.Instance?.EnableFileExport?.Value ?? false
                },
                metrics = new
                {
                    queueSize = _metricsQueue.Count,
                    maxQueueSize = HKSS.DataExportBus.Configuration.Constants.Http.MAX_QUEUE_SIZE
                }
            };

            await SendJsonResponseAsync(response, status, cancellationToken).ConfigureAwait(false);
        }

        private async Task HandleMetricsAsync(HttpListenerResponse response, CancellationToken cancellationToken)
        {
            var metrics = new List<GameMetric>();

            // Drain queue without blocking
            while (_metricsQueue.TryDequeue(out var metric) && metrics.Count < HKSS.DataExportBus.Configuration.Constants.Http.MAX_QUEUE_SIZE)
            {
                metrics.Add(metric);
            }

            await SendJsonResponseAsync(response, metrics, cancellationToken).ConfigureAwait(false);
        }

        private async Task HandleEventsAsync(HttpListenerResponse response, CancellationToken cancellationToken)
        {
            var collector = DataExportBusPlugin.Instance?.GetMetricsCollector();
            var events = collector?.GetRecentEvents() ?? new List<string>();
            await SendJsonResponseAsync(response, events, cancellationToken).ConfigureAwait(false);
        }

        private async Task HandleStateAsync(HttpListenerResponse response, CancellationToken cancellationToken)
        {
            var collector = DataExportBusPlugin.Instance?.GetMetricsCollector();
            var state = collector?.GetCurrentState() ?? new Dictionary<string, object>();
            await SendJsonResponseAsync(response, state, cancellationToken).ConfigureAwait(false);
        }

        private async Task HandleLiveSplitCommandAsync(HttpListenerResponse response, string command, CancellationToken cancellationToken)
        {
            // Forward to TCP server if available
            var tcpServer = DataExportBusPlugin.Instance?.GetTcpServer();
            string tcpResponse = "";
            string status = "failed";

            if (tcpServer != null)
            {
                try
                {
                    tcpResponse = tcpServer.ProcessLiveSplitCommand(command);
                    status = "success";
                    _logger?.LogInfo($"Forwarded LiveSplit command via HTTP: {command}");
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error forwarding LiveSplit command: {ex.Message}");
                    tcpResponse = "Error processing command";
                }
            }
            else
            {
                tcpResponse = "TCP server not available";
            }

            var result = new
            {
                command = command,
                status = status,
                response = tcpResponse,
                timestamp = DateTime.UtcNow.ToString(HKSS.DataExportBus.Configuration.Constants.FileExport.ISO_DATETIME_FORMAT)
            };

            await SendJsonResponseAsync(response, result, cancellationToken).ConfigureAwait(false);
        }

        private async Task HandleDashboard(HttpListenerResponse response)
        {
            await SendResponseAsync(response, 200, _dashboardHtml, HKSS.DataExportBus.Configuration.Constants.ContentTypes.TEXT_HTML).ConfigureAwait(false);
        }

        private async Task SendJsonResponseAsync(HttpListenerResponse response, object data, CancellationToken cancellationToken)
        {
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            await SendResponseAsync(response, 200, json, HKSS.DataExportBus.Configuration.Constants.ContentTypes.APPLICATION_JSON, cancellationToken).ConfigureAwait(false);
        }

        private async Task SendResponseAsync(HttpListenerResponse response, int statusCode, string content, string contentType = null, CancellationToken cancellationToken = default)
        {
            try
            {
                response.StatusCode = statusCode;
                response.ContentType = contentType ?? HKSS.DataExportBus.Configuration.Constants.ContentTypes.TEXT_PLAIN;
                byte[] buffer = Encoding.UTF8.GetBytes(content ?? string.Empty);

                // Validate response size
                if (buffer.Length > HKSS.DataExportBus.Configuration.Constants.Http.MAX_RESPONSE_SIZE)
                {
                    _logger?.LogWarning($"Response too large: {buffer.Length} bytes, truncating");
                    buffer = Encoding.UTF8.GetBytes("Response too large");
                }

                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                response.OutputStream.Close();
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInfo("Response cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error sending HTTP response: {ex}");
            }
        }

        public void QueueMetric(GameMetric metric)
        {
            if (metric == null || _disposed)
                return;

            _metricsQueue.Enqueue(metric);

            // Trim queue if too large
            while (_metricsQueue.Count > HKSS.DataExportBus.Configuration.Constants.Http.MAX_QUEUE_SIZE)
            {
                _metricsQueue.TryDequeue(out _);
            }
        }

        public void Stop()
        {
            if (_disposed)
                return;

            _logger?.LogInfo("Stopping HTTP server");
            _isRunning = false;
            _shutdownTokenSource?.Cancel();

            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error stopping HTTP server: {ex}");
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
                Stop();
                _shutdownTokenSource?.Dispose();
                _requestSemaphore?.Dispose();
                _stateLock?.Dispose();
            }

            _disposed = true;
        }
    }
}