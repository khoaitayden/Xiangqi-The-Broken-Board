using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace GamePeek
{
    // ── Incoming message POCOs (deserialized from phone JSON) ─────────────────

    /// <summary>Discriminator wrapper — only the <c>type</c> field is read first.</summary>
    [Serializable]
    internal class BaseMessage { public string type; }

    /// <summary>Configuration update sent by the phone app.</summary>
    [Serializable]
    public class ConfigMessage
    {
        public string type;
        public string resolution;  // e.g. "520x1131" — always in portrait order (short × long)
        public int    quality;     // 0–100
        public int    fps;         // frames per second cap
        public bool   landscape;   // true when the device is in landscape orientation
    }

    /// <summary>Touch event from the phone.</summary>
    [Serializable]
    public class TouchMessage
    {
        public string type;
        public string phase;      // began | moved | ended | canceled
        public float  x;          // normalised [0, 1]
        public float  y;          // normalised [0, 1]
        public int    fingerId;
    }

    /// <summary>Gyroscope data from the phone (rad/s).</summary>
    [Serializable]
    public class GyroMessage
    {
        public string type;
        public float x, y, z;
    }

    /// <summary>Accelerometer data from the phone (g-force).</summary>
    [Serializable]
    public class AccelMessage
    {
        public string type;
        public float x, y, z;
    }

    /// <summary>Hello/handshake message sent by the phone on connect.</summary>
    [Serializable]
    public class HelloMessage
    {
        public string type;
        public string client;       // "flutter" | "flutter_webrtc"
        public string tier;         // "free" | "pro"
        public string deviceName;   // human-readable device name, e.g. "John's iPhone 15 Pro"
        public int    width;        // native screen width in pixels (0 if not provided)
        public int    height;       // native screen height in pixels (0 if not provided)
        public string orientation;  // "portrait" | "landscape" (empty if not provided)
    }

    /// <summary>WebRTC SDP answer from the phone (signaling).</summary>
    [Serializable]
    internal class AnswerMessage
    {
        public string type;
        public string sdp;
    }

    /// <summary>ICE candidate from the phone (signaling).</summary>
    [Serializable]
    internal class CandidateMessage
    {
        public string type;
        public string candidate;
        public string sdpMid;
        public int    sdpMLineIndex;
    }

    /// <summary>Ping/pong timestamp message.</summary>
    [Serializable]
    internal class PingPongMessage
    {
        public string type;
        public long   ts;   // Unix epoch milliseconds
    }

    /// <summary>Sent to clients when the Unity editor enters or exits Play Mode.</summary>
    [Serializable]
    internal class PlayModeMessage
    {
        public string type;    // always "playmode"
        public bool   playing; // true = in Play Mode, false = Edit Mode
    }

    // ── WebSocket behaviour (one instance per connected client) ───────────────

    /// <summary>
    /// Per-connection WebSocket behaviour class consumed by websocket-sharp.
    /// Each instance is handed a reference to its owning
    /// <see cref="GamePeekWebSocketServer"/> when the connection is created (via
    /// the <c>AddWebSocketService</c> initializer), so connect/disconnect/message
    /// delivery is instance-scoped — should two server instances ever coexist
    /// (failed stop, overlapping reload), each only receives its own traffic.
    /// </summary>
    internal class GamePeekBehavior : WebSocketBehavior
    {
        /// <summary>
        /// The server that owns this connection. Assigned by the
        /// <c>AddWebSocketService</c> initializer before the connection opens.
        /// </summary>
        internal GamePeekWebSocketServer Owner { get; set; }

        protected override void OnOpen()
        {
            string deviceName = Context.Headers["X-Device-Name"] ?? "Unknown";
            Owner?.HandleConnect(ID, deviceName);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Owner?.HandleDisconnect(ID);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if (e.IsText)
                Owner?.HandleTextMessage(ID, e.Data);
            // Binary frames from phone are not expected; ignore silently.
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            GamePeekConstants.LogWarning($"[WS] Client {ID} error: {e.Message}");
        }

        /// <summary>Sends a text message to THIS specific client session.</summary>
        internal void SendText(string json) => Send(json);
    }

    // ── Server wrapper ────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps a <c>websocket-sharp</c> <see cref="WebSocketSharp.Server.WebSocketServer"/>
    /// and exposes a clean API for the rest of GamePeek.
    /// <para>
    /// <b>Thread safety:</b> <see cref="BroadcastFrame"/> and
    /// <see cref="SendToSession"/> may be called from any thread.
    /// All events are raised on the websocket-sharp internal thread pool
    /// and must be marshalled to the Unity main thread by the subscriber
    /// (see <see cref="ConnectionManager"/>).
    /// </para>
    /// </summary>
    public sealed class GamePeekWebSocketServer : IDisposable
    {
        // ── Events (raised on websocket-sharp background threads) ─────────────

        /// <summary>Raised when a new client connects. Args: (sessionId, deviceName).</summary>
        public event Action<string, string> ClientConnected;

        /// <summary>Raised when a client disconnects. Args: (sessionId).</summary>
        public event Action<string>         ClientDisconnected;

        /// <summary>
        /// Raised for every incoming text message except RTT pings (which are
        /// answered directly on the socket thread). Args: (sessionId, rawJson).
        /// <para>
        /// Raised on a websocket-sharp background thread. <c>JsonUtility</c> is a
        /// main-thread-only API, so the subscriber must marshal the raw string to
        /// the Unity main thread before parsing (see <see cref="ConnectionManager"/>).
        /// </para>
        /// </summary>
        public event Action<string, string> TextMessageReceived;

        // ── Internal state ────────────────────────────────────────────────────
        private WebSocketSharp.Server.WebSocketServer _server;
        private readonly int  _port;
        private volatile bool _running;

        // ── Constructor ───────────────────────────────────────────────────────
        /// <summary>Creates the server wrapper. Call <see cref="Start"/> to bind the socket.</summary>
        /// <param name="port">TCP port to listen on (default 7777).</param>
        public GamePeekWebSocketServer(int port = GamePeekConstants.DefaultPort) => _port = port;

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Binds the socket and starts accepting connections.</summary>
        public void Start()
        {
            if (_running) return;

            try
            {
                _server = new WebSocketSharp.Server.WebSocketServer(_port);
                // The initializer runs for every new connection and scopes event
                // delivery to THIS server instance (see GamePeekBehavior.Owner).
                _server.AddWebSocketService<GamePeekBehavior>("/",
                    behavior => behavior.Owner = this);
                _server.Start();
                _running = true;
            }
            catch
            {
                Stop();
                throw;
            }

            GamePeekConstants.Log($"[WS] Server listening on port {_port}.");
        }

        /// <summary>Broadcasts a raw JPEG frame to all connected clients as a binary message.</summary>
        /// <param name="jpegBytes">Complete JPEG byte array representing one frame.</param>
        public void BroadcastFrame(byte[] jpegBytes)
        {
            if (!_running || jpegBytes == null || jpegBytes.Length == 0) return;
            _server?.WebSocketServices["/"]?.Sessions?.Broadcast(jpegBytes);
        }

        /// <summary>Broadcasts a UTF-8 text message to all connected clients.</summary>
        public void BroadcastText(string json)
        {
            if (!_running || string.IsNullOrEmpty(json)) return;
            _server?.WebSocketServices["/"]?.Sessions?.Broadcast(json);
        }

        /// <summary>
        /// Sends a UTF-8 text message to a single specific session.
        /// Safe to call from any thread.
        /// </summary>
        /// <param name="sessionId">websocket-sharp session ID.</param>
        /// <param name="json">JSON payload to send.</param>
        public void SendToSession(string sessionId, string json)
        {
            if (!_running || string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(json)) return;
            try
            {
                _server?.WebSocketServices["/"]?.Sessions?.SendTo(json, sessionId);
            }
            catch (Exception ex)
            {
                GamePeekConstants.LogWarning($"[WS] SendToSession failed: {ex.Message}");
            }
        }

        /// <summary>Forcibly closes a specific client session.</summary>
        public void CloseSession(string sessionId)
        {
            if (!_running || string.IsNullOrEmpty(sessionId)) return;
            try
            {
                _server?.WebSocketServices["/"]?.Sessions
                    ?.CloseSession(sessionId, WebSocketSharp.CloseStatusCode.Normal, "use_jpeg");
            }
            catch { }
        }

        /// <summary>Returns the number of currently connected clients.</summary>
        public int ConnectedCount =>
            _server?.WebSocketServices["/"]?.Sessions?.Count ?? 0;

        /// <summary>Stops the server and releases resources.</summary>
        public void Stop()
        {
            if (!_running && _server == null) return;
            _running = false;

            try
            {
                _server?.Stop();
            }
            catch (Exception ex)
            {
                GamePeekConstants.LogWarning($"[WS] Server stop failed: {ex.Message}");
            }

            _server = null;
            GamePeekConstants.Log("[WS] Server stopped.");
        }

        /// <inheritdoc/>
        public void Dispose() => Stop();

        // ── Behaviour callbacks (invoked per connection by GamePeekBehavior) ──
        // The !_running guards suppress late callbacks while (or after) the
        // server shuts down — the same effect the old static-event unhook had.

        internal void HandleConnect(string sessionId, string deviceName)
        {
            if (!_running) return;
            ClientConnected?.Invoke(sessionId, deviceName);
        }

        internal void HandleDisconnect(string sessionId)
        {
            if (!_running) return;
            ClientDisconnected?.Invoke(sessionId);
        }

        internal void HandleTextMessage(string sessionId, string json)
        {
            if (!_running) return;
            if (string.IsNullOrWhiteSpace(json)) return;

            GamePeekConstants.Log($"[WS] Received: {json}");

            // This runs on websocket-sharp's background thread pool where
            // JsonUtility (a main-thread-only API) must not be touched.
            // RTT pings are answered right here so the phone's latency
            // measurement excludes the main-thread queue; everything else is
            // forwarded raw and parsed in ConnectionManager's update drain.
            if (TryHandlePing(sessionId, json)) return;

            TextMessageReceived?.Invoke(sessionId, json);
        }

        /// <summary>
        /// Detects <c>{"type":"ping","ts":...}</c> with plain string scanning
        /// (no JsonUtility — this runs on a background thread) and echoes the
        /// pong immediately. Returns <c>true</c> when the message was a ping.
        /// </summary>
        private bool TryHandlePing(string sessionId, string json)
        {
            const string pingLiteral = "\"ping\"";

            int typeKey = json.IndexOf("\"type\"", StringComparison.Ordinal);
            if (typeKey < 0) return false;

            // Expect ':' then optional whitespace then "ping".
            int i = json.IndexOf(':', typeKey + 6);
            if (i < 0) return false;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i + pingLiteral.Length > json.Length ||
                string.CompareOrdinal(json, i, pingLiteral, 0, pingLiteral.Length) != 0)
                return false;

            // Extract the ts value so the phone can compute RTT from the echo.
            long ts = 0;
            int tsKey = json.IndexOf("\"ts\"", StringComparison.Ordinal);
            if (tsKey >= 0)
            {
                int colon = json.IndexOf(':', tsKey + 4);
                if (colon >= 0)
                {
                    int start = colon + 1;
                    while (start < json.Length && char.IsWhiteSpace(json[start])) start++;
                    int end = start;
                    if (end < json.Length && json[end] == '-') end++;
                    while (end < json.Length && char.IsDigit(json[end])) end++;
                    long.TryParse(json.Substring(start, end - start), out ts);
                }
            }

            // Pong JSON built by hand — JsonUtility.ToJson is main-thread-only.
            SendToSession(sessionId, "{\"type\":\"pong\",\"ts\":" + ts + "}");
            return true;
        }
    }
}
