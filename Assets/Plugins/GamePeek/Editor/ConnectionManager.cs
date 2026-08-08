using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;

namespace GamePeek
{
    // ── State & data model ────────────────────────────────────────────────────

    /// <summary>Connection state of the GamePeek plugin.</summary>
    public enum ConnectionState
    {
        /// <summary>Server is stopped; no clients connected.</summary>
        Disconnected,
        /// <summary>Server is running and advertising via mDNS; waiting for a device.</summary>
        Advertising,
        /// <summary>At least one device is connected and streaming is active.</summary>
        Connected,
    }

    /// <summary>Describes a connected device.</summary>
    public sealed class DeviceInfo
    {
        /// <summary>websocket-sharp session identifier.</summary>
        public string SessionId  { get; init; }
        /// <summary>Device name reported by the phone via the <c>X-Device-Name</c> header.</summary>
        public string DeviceName { get; init; }
        /// <summary>IP address of the remote device (empty string if unknown).</summary>
        public string IPAddress  { get; init; }
        /// <summary>UTC time when the device connected.</summary>
        public DateTime ConnectedAt { get; init; }
        /// <summary>Whether the device has a Pro tier subscription.</summary>
        public bool IsPro { get; init; }
        /// <summary>Whether the session has completed the hello handshake.</summary>
        public bool HelloReceived { get; init; }
    }

    // ── Streaming configuration ───────────────────────────────────────────────

    /// <summary>All runtime-adjustable streaming parameters.</summary>
    public sealed class StreamConfig
    {
        public int Width          { get; set; } = 1280;
        public int Height         { get; set; } = 720;
        public int Quality        { get; set; } = 75;
        public int FpsCap         { get; set; } = 30;
        public int MaxBitrateKbps { get; set; } = GamePeekConstants.DefaultWebRtcMaxBitrateKbps;
        public string WebRtcStunUrl { get; set; } = string.Empty;
    }

    // ── Manager ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Central orchestrator for GamePeek.  Manages the full lifecycle of the
    /// WebSocket server, mDNS advertiser, frame capture, frame encoder, and
    /// (when the <c>com.unity.webrtc</c> package is present) the WebRTC streamer.
    /// <para>
    /// All state-change events are guaranteed to fire on the Unity <em>main
    /// thread</em> via a <see cref="ConcurrentQueue{T}"/> drained on each
    /// <see cref="EditorApplication.update"/> tick.
    /// </para>
    /// </summary>
    public sealed class ConnectionManager : IDisposable
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        private static ConnectionManager _instance;
        /// <summary>Returns the single shared instance, creating it if necessary.</summary>
        public static ConnectionManager Instance
            => _instance ??= new ConnectionManager();

        // ── Events (main-thread) ──────────────────────────────────────────────
        /// <summary>Fired whenever the connection state changes.</summary>
        public event Action<ConnectionState>   StateChanged;
        /// <summary>Fired when a device successfully connects.</summary>
        public event Action<DeviceInfo>        DeviceConnected;
        /// <summary>Fired when a device disconnects.</summary>
        public event Action<DeviceInfo>        DeviceDisconnected;
        /// <summary>Fired on each FPS stats update (≈ once per second).</summary>
        public event Action<float, float>      StatsUpdated;  // (captureFps, encodeMs)
        /// <summary>Fired when the smoothed RTT changes. Arg is RTT in milliseconds.</summary>
        public event Action<float>             RttUpdated;

        // ── Observed state (main-thread readable) ─────────────────────────────
        /// <summary>Current connection state.</summary>
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        /// <summary>The port the WebSocket server is (or will be) listening on.</summary>
        public int Port { get; private set; } = GamePeekConstants.DefaultPort;

        /// <summary>Read-only view of all currently connected devices.</summary>
        public IReadOnlyList<DeviceInfo> ConnectedDevices => _devices;

        /// <summary>Active streaming configuration.</summary>
        public StreamConfig Config { get; } = new StreamConfig();

        /// <summary>Smoothed round-trip time in milliseconds (0 until first measurement).</summary>
        public float SmoothedRtt { get; private set; }

        /// <summary>Whether a WebRTC connection is currently active.</summary>
        public bool WebRtcActive { get; private set; }

        /// <summary>Active frame capture strategy.</summary>
        public CaptureMethod ActiveCaptureMethod { get; private set; } = CaptureMethod.CameraRender;

        // ── Internal components ───────────────────────────────────────────────
        private GamePeekWebSocketServer _wsServer;
        private MdnsAdvertiser         _mdns;
        private FrameCapture           _capture;
        private FrameEncoder           _encoder;

        private readonly List<DeviceInfo>       _devices         = new();
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new();

        // First session to send a valid hello becomes host; only it can send config/input.
        private string _hostSessionId;

        // Suppress repeated "not in Play Mode" warnings for touch input.
        private bool _gameViewFocusWarningLogged;

        private bool   _editorHooked;
        // Time.unscaledDeltaTime is unreliable in edit mode — timers use
        // EditorApplication.timeSinceStartup deltas (same pattern as FrameCapture).
        private double _lastStatsTime;

        // ── RTT ───────────────────────────────────────────────────────────────
        private double _lastPingTime;
        private readonly Queue<float> _rttSamples = new(5);

        // ── Domain-reload survival (SessionState keys) ────────────────────────
        // SessionState survives domain reloads but not editor restarts. The live
        // streaming state is snapshotted here in OnBeforeAssemblyReload and claimed
        // by GamePeekSessionRestore after the reload, so the server comes back up
        // on the same port and phones can auto-reconnect.
        internal const string SessionKeyResumeStreaming     = "GamePeek_Resume_Streaming";
        internal const string SessionKeyResumePort          = "GamePeek_Resume_Port";
        internal const string SessionKeyResumeCaptureMethod = "GamePeek_Resume_CaptureMethod";
        internal const string SessionKeyResumeWidth         = "GamePeek_Resume_Width";
        internal const string SessionKeyResumeHeight        = "GamePeek_Resume_Height";
        internal const string SessionKeyResumeQuality       = "GamePeek_Resume_Quality";
        internal const string SessionKeyResumeFps           = "GamePeek_Resume_Fps";

        // Game View size the user had selected before GamePeek switched to the
        // "GamePeekCapture" slot (-1 / unset = nothing to restore). Unlike the
        // resume keys above this is NOT written in OnBeforeAssemblyReload — it is
        // captured on the first resize of a streaming session and only cleared
        // when an intentional stop restores the selection, so it survives
        // domain reloads mid-stream.
        internal const string SessionKeyPrevGameViewSize    = "GamePeek_Prev_GameViewSizeIndex";

        // ── WebRTC (compiled only when package is installed) ──────────────────
#if UNITY_WEBRTC
        private WebRTCStreamer _webRtcStreamer;
        private string         _webRtcSessionId;

        // Outbound signaling POCOs — kept here to avoid dependency on WebRTCStreamer.cs
        [Serializable] private class WsOfferMsg    { public string type; public string sdp; }
        [Serializable] private class WsCandidateMsg
        {
            public string type;
            public string candidate;
            public string sdpMid;
            public int    sdpMLineIndex;
        }
#endif

        // ── Constructor / dispose ─────────────────────────────────────────────

        private ConnectionManager()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private void OnBeforeAssemblyReload()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;

            bool wasStreaming = State != ConnectionState.Disconnected;

            // Close WITHOUT the shutdown broadcast — "shutdown" tells the phone the
            // stream ended intentionally (clean exit, no reconnect). An abrupt close
            // makes it treat the reload as a drop and auto-reconnect once the server
            // is back up (restarted by GamePeekSessionRestore after the reload).
            StopStreaming(notifyClients: false);

            if (!wasStreaming) return;

            // Snapshot the live state AFTER StopStreaming (which erases the resume
            // flag) so GamePeekSessionRestore can bring streaming back up.
            SessionState.SetBool(SessionKeyResumeStreaming, true);
            SessionState.SetInt(SessionKeyResumePort,          Port);
            SessionState.SetInt(SessionKeyResumeCaptureMethod, (int)ActiveCaptureMethod);
            SessionState.SetInt(SessionKeyResumeWidth,         Config.Width);
            SessionState.SetInt(SessionKeyResumeHeight,        Config.Height);
            SessionState.SetInt(SessionKeyResumeQuality,       Config.Quality);
            SessionState.SetInt(SessionKeyResumeFps,           Config.FpsCap);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            StopStreaming();
            _instance = null;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Starts the WebSocket server and mDNS advertiser.
        /// Also auto-configures the Windows firewall on first run.
        /// <para>
        /// When <paramref name="port"/> is already taken, binding is retried on
        /// <c>port+1 … port+9</c> before giving up. On success <see cref="Port"/>
        /// holds the port actually bound — the mDNS advertisement, the connection
        /// QR, the window UI, and the domain-reload resume snapshot all read it.
        /// </para>
        /// </summary>
        public bool StartStreaming(int port = GamePeekConstants.DefaultPort)
        {
            if (State != ConnectionState.Disconnected) return true;

            try
            {
                // Boot WebSocket server — retry on the next few ports when the
                // requested one is taken (e.g. another editor instance).
                Exception lastBindError = null;
                for (int candidate = port;
                     candidate < port + GamePeekConstants.PortBindAttempts && candidate <= 65535;
                     candidate++)
                {
                    var server = new GamePeekWebSocketServer(candidate);
                    server.ClientConnected     += OnClientConnected;
                    server.ClientDisconnected  += OnClientDisconnected;
                    server.TextMessageReceived += OnTextMessageReceived;
                    try
                    {
                        server.Start();
                    }
                    catch (Exception ex)
                    {
                        lastBindError = ex;
                        continue; // server cleaned itself up in Start(); try the next port
                    }
                    _wsServer = server;
                    Port      = candidate;
                    break;
                }

                if (_wsServer == null)
                    throw lastBindError ?? new InvalidOperationException("No bindable port found.");

                if (Port != port)
                {
                    // The QR texture cache only tracks IP changes — force a
                    // regeneration so the payload carries the fallback port.
                    QRCodeGenerator.Invalidate();
                    Debug.LogWarning(
                        $"[GamePeek] [WS] TCP port {port} is in use — streaming on fallback port {Port} instead. " +
                        "The QR code and mDNS advertisement use the new port; devices connecting by IP must use it too.");
                }

                // One-time Windows firewall setup. Pass the REQUESTED base port:
                // the rule covers the whole retry range [port, port+attempts-1],
                // so any fallback port bound above is already allowed.
                FirewallHelper.EnsureFirewallRule(port);

                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

                // Boot mDNS
                string localIp    = QRCodeGenerator.GetLocalIPv4();
                string editorName = EditorPrefs.GetString(GamePeekConstants.PrefEditorName, string.Empty);
                _mdns = new MdnsAdvertiser(Port, localIp, editorName);
                _mdns.Start();

                // Boot encoder + capture
                _encoder = new FrameEncoder(_wsServer, Config.Quality);
                _capture = new FrameCapture(_encoder, Config.Width, Config.Height, Config.FpsCap);
                _capture.SetCaptureMethod(ActiveCaptureMethod);
                _capture.Start();

                HookEditorUpdate();
                SetState(ConnectionState.Advertising);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GamePeek] [WS] Failed to start GamePeek on TCP ports {port}–{port + GamePeekConstants.PortBindAttempts - 1}. Other processes may already be using them. You can change the port in the GamePeek editor window.");
                Debug.LogException(ex);
                StopStreaming();
                return false;
            }
        }

        /// <summary>
        /// Sends a shutdown message to a single client and closes its connection.
        /// All other clients remain connected and streaming continues.
        /// </summary>
        public void DisconnectDevice(string sessionId)
        {
            if (_wsServer == null || string.IsNullOrEmpty(sessionId)) return;
            _wsServer.SendToSession(sessionId, "{\"type\":\"shutdown\"}");
            _wsServer.CloseSession(sessionId);
        }

        /// <summary>Stops all streaming, severs all connections, and releases resources.</summary>
        /// <param name="notifyClients">
        /// When <c>true</c> (default — an intentional stop) clients receive a
        /// <c>shutdown</c> message so they exit cleanly without reconnecting.
        /// Pass <c>false</c> on domain-reload teardown so the phone treats the
        /// close as a drop and auto-reconnects once the server is back up.
        /// </param>
        public void StopStreaming(bool notifyClients = true)
        {
            if (State == ConnectionState.Disconnected &&
                _wsServer == null &&
                _mdns == null &&
                _capture == null)
                return;

            // An intentional stop cancels any pending after-reload auto-resume.
            // (OnBeforeAssemblyReload re-sets the flag after calling this method.)
            SessionState.EraseBool(SessionKeyResumeStreaming);

#if UNITY_WEBRTC
            // Send shutdown to the WebRTC client BEFORE closing the peer connection,
            // so Flutter transitions to ConnectionState.shutdown (clean exit, no reconnect
            // prompt) rather than ConnectionState.disconnected (reconnect countdown).
            if (notifyClients && _webRtcSessionId != null)
                _wsServer?.SendToSession(_webRtcSessionId, "{\"type\":\"shutdown\"}");
            TearDownWebRTC();
#endif

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            _capture?.Stop();
            _capture = null;

            _encoder = null;

            // Tell all connected clients the stream is ending before closing the socket.
            if (notifyClients)
                _wsServer?.BroadcastText("{\"type\":\"shutdown\"}");
            _wsServer?.Stop();
            _wsServer = null;

            _mdns?.Stop();
            _mdns = null;

            // Restore the Game View size the user had selected before GamePeek
            // forced the "GamePeekCapture" slot. Only on an intentional stop —
            // on domain-reload teardown streaming resumes right after, and the
            // SessionState key survives the reload so a later manual stop still
            // restores the original selection. The custom slot itself is kept;
            // recreating it every session would churn the GameViewSizes asset.
            if (notifyClients) RestorePreviousGameViewSize();

            _devices.Clear();
            _hostSessionId = null;
            SmoothedRtt  = 0f;
            WebRtcActive = false;
            _rttSamples.Clear();

            UnhookEditorUpdate();
            QRCodeGenerator.Invalidate();
            SetState(ConnectionState.Disconnected);

#if ENABLE_INPUT_SYSTEM
            InputInjector.RemoveVirtualDevices();
#endif
        }

        /// <summary>Updates the WebRTC maximum video bitrate at runtime.</summary>
        public void SetWebRtcMaxBitrate(int kbps)
        {
            Config.MaxBitrateKbps = kbps;
#if UNITY_WEBRTC
            _webRtcStreamer?.SetMaxBitrate(kbps);
#endif
        }

        /// <summary>Switches the frame capture strategy at runtime.</summary>
        public void SetCaptureMethod(CaptureMethod method)
        {
            ActiveCaptureMethod = method;
            _capture?.SetCaptureMethod(method);
        }

        /// <summary>
        /// Applies a <see cref="StreamConfig"/> received from the phone app and
        /// updates the capture + encoder components accordingly.
        /// </summary>
        public void ApplyConfig(int width, int height, int quality, int fpsCap)
        {
            bool resolutionChanged = width != Config.Width || height != Config.Height;

            Config.Width   = width;
            Config.Height  = height;
            Config.Quality = quality;
            Config.FpsCap  = fpsCap;

            _capture?.SetResolution(width, height);
            _capture?.SetFpsCap(fpsCap);
            _encoder?.SetQuality(quality);
#if UNITY_WEBRTC
            _webRtcStreamer?.SetFpsCap(fpsCap);
#endif

            // Resize the Game View so ScreenCapture captures at the phone's exact
            // resolution — avoids stretching when aspect ratios differ.
            TrySetGameViewResolution(width, height);

#if UNITY_WEBRTC
            // The WebRTC RenderTexture is fixed-size — restart the session with the
            // new dimensions so the video track matches the phone's resolution.
            if (resolutionChanged && _webRtcStreamer != null && _webRtcSessionId != null)
            {
                var sessionId = _webRtcSessionId;
                TearDownWebRTC();
                StartWebRTCNegotiation(sessionId);
            }
#endif
        }

        // Warn only once per mismatch streak when the Game View refuses the requested size.
        private static bool _resizeMismatchWarned;

        /// <summary>
        /// Sets the Unity Game View to a custom fixed resolution using
        /// <see cref="UnityEditor.TestTools.Graphics.GameViewSize"/>.
        /// </summary>
        private static void TrySetGameViewResolution(int width, int height)
        {
            try
            {
                // Remember what the user had selected before the first switch to
                // the "GamePeekCapture" slot so StopStreaming can restore it.
                // Captured once per streaming session (repeated config messages
                // must not overwrite it with the capture slot itself) and kept in
                // SessionState so it survives domain reloads mid-stream.
                if (SessionState.GetInt(SessionKeyPrevGameViewSize, -1) < 0)
                {
                    int current = GameViewSize.GetSelectedIndex();
                    if (current >= 0)
                        SessionState.SetInt(SessionKeyPrevGameViewSize, current);
                }

                var sizeObj = GameViewSize.SetCustomSize(width, height);
                GameViewSize.SelectSize(sizeObj);
            }
            catch (Exception ex)
            {
                GamePeekConstants.LogWarning($"[Config] Could not resize Game View: {ex.Message}");
            }

            // The reflection above can also fail silently (Unity internals change
            // between versions) — read back the actual rendering resolution on the
            // next tick, after the Game View has applied the new size.
            EditorApplication.delayCall += () =>
            {
                try
                {
                    PlayModeWindow.GetRenderingResolution(out uint actualW, out uint actualH);
                    if (actualW == (uint)width && actualH == (uint)height)
                    {
                        _resizeMismatchWarned = false;
                    }
                    else if (!_resizeMismatchWarned)
                    {
                        _resizeMismatchWarned = true;
                        Debug.LogWarning(
                            $"[GamePeek] [Config] Game View resize to {width}x{height} did not apply " +
                            $"(actual rendering resolution: {actualW}x{actualH}). Touch mapping uses the " +
                            "actual rendering size so taps still align, but the stream aspect may differ " +
                            "from the phone.");
                    }
                }
                catch (Exception ex)
                {
                    GamePeekConstants.LogWarning($"[Config] Could not verify Game View resolution: {ex.Message}");
                }
            };
        }

        /// <summary>
        /// Re-selects the Game View size that was active before
        /// <see cref="TrySetGameViewResolution"/> switched to the
        /// "GamePeekCapture" slot, then clears the stored index.
        /// </summary>
        private static void RestorePreviousGameViewSize()
        {
            int prevIndex = SessionState.GetInt(SessionKeyPrevGameViewSize, -1);
            if (prevIndex < 0) return;

            SessionState.EraseInt(SessionKeyPrevGameViewSize);
            try
            {
                GameViewSize.SelectIndex(prevIndex);
            }
            catch (Exception ex)
            {
                GamePeekConstants.LogWarning($"[Config] Could not restore Game View size: {ex.Message}");
            }
        }

        // ── Editor update hook ────────────────────────────────────────────────

        private void HookEditorUpdate()
        {
            if (_editorHooked) return;
            _lastStatsTime = EditorApplication.timeSinceStartup;
            _lastPingTime  = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            _editorHooked = true;
        }

        private void UnhookEditorUpdate()
        {
            if (!_editorHooked) return;
            EditorApplication.update -= OnEditorUpdate;
            _editorHooked = false;
        }

        private void OnEditorUpdate()
        {
            // Drain cross-thread callbacks
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { GamePeekConstants.LogError(ex.ToString()); }
            }

#if UNITY_WEBRTC
            // Drive the WebRTC engine every editor tick
            _webRtcStreamer?.Tick();
#endif

            double now = EditorApplication.timeSinceStartup;

            // Periodic stats update
            if (now - _lastStatsTime >= 1.0)
            {
                _lastStatsTime = now;
                if (_capture != null && _encoder != null)
                {
#if UNITY_WEBRTC
                    float captureFps = WebRtcActive && _webRtcStreamer != null
                        ? _webRtcStreamer.SmoothedCaptureFps
                        : _capture.SmoothedFps;
                    StatsUpdated?.Invoke(captureFps, _encoder.LastEncodeMs);
#else
                    StatsUpdated?.Invoke(_capture.SmoothedFps, _encoder.LastEncodeMs);
#endif
                }
            }

            // RTT ping every 30 s (only while connected)
            if (State == ConnectionState.Connected && _devices.Count > 0)
            {
                if (now - _lastPingTime >= GamePeekConstants.PingIntervalSeconds)
                {
                    _lastPingTime = now;
                    SendPing();
                }
            }
            else
            {
                // Keep the baseline fresh while disconnected so the first ping
                // fires one full interval after a device connects.
                _lastPingTime = now;
            }
        }

        // ── Ping / pong ───────────────────────────────────────────────────────

        private void SendPing()
        {
            long ts  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var  msg = new PingPongMessage { type = "ping", ts = ts };
            _wsServer?.BroadcastText(UnityEngine.JsonUtility.ToJson(msg));
        }

        /// <summary>
        /// Runs on the main thread via the dispatch drain. <paramref name="receivedAt"/>
        /// was captured on the socket thread so queue latency doesn't inflate the RTT.
        /// </summary>
        private void OnPongReceived(long ts, long receivedAt)
        {
            float rtt = (float)(receivedAt - ts);

            if (_rttSamples.Count >= 5) _rttSamples.Dequeue();
            _rttSamples.Enqueue(rtt);

            float sum = 0f;
            foreach (float s in _rttSamples) sum += s;
            SmoothedRtt = sum / _rttSamples.Count;

            RttUpdated?.Invoke(SmoothedRtt);
        }

        // ── Event handlers (may arrive on background thread) ─────────────────

        private void OnClientConnected(string sessionId, string deviceName)
            => Enqueue(() =>
            {
                var info = new DeviceInfo
                {
                    SessionId   = sessionId,
                    DeviceName  = deviceName,
                    IPAddress   = string.Empty,
                    ConnectedAt = DateTime.UtcNow,
                };
                _devices.Add(info);
                GamePeekConstants.Log($"[WS] Device connected: {deviceName} (session {sessionId})");

#if ENABLE_INPUT_SYSTEM
                InputInjector.EnsureVirtualDevices();
#endif
                SetState(ConnectionState.Connected);
                DeviceConnected?.Invoke(info);
            });

        private void OnClientDisconnected(string sessionId)
            => Enqueue(() =>
            {
                int idx = _devices.FindIndex(d => d.SessionId == sessionId);
                if (idx < 0) return;

                var info = _devices[idx];
                _devices.RemoveAt(idx);
                GamePeekConstants.Log($"[WS] Device disconnected: {info.DeviceName}");

                if (_hostSessionId == sessionId)
                {
                    _hostSessionId = _devices.Count > 0 ? _devices[0].SessionId : null;
                    if (_hostSessionId != null)
                        GamePeekConstants.Log($"[Auth] Host transferred to {_devices[0].DeviceName}");
                }

#if UNITY_WEBRTC
                if (_webRtcSessionId == sessionId)
                    TearDownWebRTC();
#endif

                SetState(_devices.Count > 0 ? ConnectionState.Connected : ConnectionState.Advertising);
                DeviceDisconnected?.Invoke(info);
            });

        /// <summary>
        /// Raised on websocket-sharp's background thread for every raw text
        /// message. JsonUtility is main-thread-only, so hop to the main thread
        /// before any parsing — the queue preserves per-session message ordering.
        /// (RTT pings never arrive here; the server answers them on the socket thread.)
        /// </summary>
        private void OnTextMessageReceived(string sessionId, string json)
        {
            // Timestamp captured on the socket thread so the main-thread queue
            // latency doesn't inflate the RTT measured from pong messages.
            long receivedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Enqueue(() => DispatchTextMessage(sessionId, json, receivedAt));
        }

        /// <summary>
        /// Parses a raw JSON message and routes it to the typed handler.
        /// Runs on the main thread via the update-queue drain.
        /// </summary>
        private void DispatchTextMessage(string sessionId, string json, long receivedAt)
        {
            try
            {
                var baseMsg = UnityEngine.JsonUtility.FromJson<BaseMessage>(json);
                if (baseMsg == null) return;

                switch (baseMsg.type)
                {
                    case "hello":
                        OnHelloReceived(sessionId, UnityEngine.JsonUtility.FromJson<HelloMessage>(json));
                        break;

                    case "config":
                        OnConfigReceived(sessionId, UnityEngine.JsonUtility.FromJson<ConfigMessage>(json));
                        break;

                    // Only the host session may inject input.
                    case "touch":
                        if (sessionId != _hostSessionId) break;
                        HandleTouch(UnityEngine.JsonUtility.FromJson<TouchMessage>(json));
                        break;

                    case "gyro":
                        if (sessionId != _hostSessionId) break;
                        var gyro = UnityEngine.JsonUtility.FromJson<GyroMessage>(json);
                        GamePeekConstants.Log($"[Input] Gyro  x={gyro.x:F3} y={gyro.y:F3} z={gyro.z:F3}");
                        InputInjector.InjectGyro(gyro.x, gyro.y, gyro.z);
                        break;

                    case "accel":
                        if (sessionId != _hostSessionId) break;
                        var accel = UnityEngine.JsonUtility.FromJson<AccelMessage>(json);
                        GamePeekConstants.Log($"[Input] Accel x={accel.x:F3} y={accel.y:F3} z={accel.z:F3}");
                        InputInjector.InjectAccelerometer(accel.x, accel.y, accel.z);
                        break;

                    // ── WebRTC signaling ──────────────────────────────────────
#if UNITY_WEBRTC
                    case "answer":
                        var ans = UnityEngine.JsonUtility.FromJson<AnswerMessage>(json);
                        OnAnswerReceived(sessionId, ans.sdp);
                        break;

                    case "candidate":
                        var cand = UnityEngine.JsonUtility.FromJson<CandidateMessage>(json);
                        OnCandidateReceived(sessionId, cand.candidate, cand.sdpMid, cand.sdpMLineIndex);
                        break;
#else
                    case "answer":
                    case "candidate":
                        break; // WebRTC package not installed — signaling ignored.
#endif

                    // ── RTT ping/pong ─────────────────────────────────────────
                    case "ping":
                        // Pings are normally answered on the socket thread;
                        // fallback in case one slips through.
                        var ping = UnityEngine.JsonUtility.FromJson<PingPongMessage>(json);
                        _wsServer?.SendToSession(sessionId,
                            UnityEngine.JsonUtility.ToJson(new PingPongMessage { type = "pong", ts = ping.ts }));
                        break;

                    case "pong":
                        OnPongReceived(UnityEngine.JsonUtility.FromJson<PingPongMessage>(json).ts, receivedAt);
                        break;

                    default:
                        GamePeekConstants.LogWarning($"[WS] Unknown message type: {baseMsg.type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                GamePeekConstants.LogWarning($"[WS] Failed to parse message: {ex.Message}\n{json}");
            }
        }

        /// <summary>
        /// Handles a config message. Runs on the main thread (via the update
        /// drain), so any preceding hello has already set the host session.
        /// </summary>
        private void OnConfigReceived(string sessionId, ConfigMessage msg)
        {
            if (msg == null) return;

            if (sessionId != _hostSessionId)
            {
                GamePeekConstants.LogWarning($"[Auth] Config rejected from non-host session {sessionId}");
                return;
            }

            int width  = Config.Width;
            int height = Config.Height;
            if (!string.IsNullOrEmpty(msg.resolution))
            {
                var parts = msg.resolution.Split('x');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out int w) &&
                    int.TryParse(parts[1], out int h))
                {
                    width  = w;
                    height = h;
                }
            }

            // Resolution is always sent in portrait order (short × long).
            // Swap when the device is landscape so the capture RT matches the screen.
            if (msg.landscape && width < height) { int t = width; width = height; height = t; }
            else if (!msg.landscape && width > height) { int t = width; width = height; height = t; }

            int quality = msg.quality > 0 ? Mathf.Clamp(msg.quality, 1, 100) : Config.Quality;
            int fps     = msg.fps     > 0 ? Mathf.Clamp(msg.fps, 1, 120)     : Config.FpsCap;

            ApplyConfig(width, height, quality, fps);
        }

        /// <summary>
        /// Removes lingering <see cref="_devices"/> entries left behind by an
        /// abrupt drop (Wi-Fi off, app killed) when the same device reconnects.
        /// websocket-sharp only notices a half-open connection on its periodic
        /// sweep (~60 s), so until then the dead session would keep its slot —
        /// counting against the multi-device Pro gate and potentially holding
        /// the host role. A hello whose device name matches another session's
        /// entry can only be that device reconnecting, so the stale entry is
        /// evicted and its socket closed.
        /// </summary>
        private void EvictStaleSessions(string sessionId, string deviceName)
        {
            // "Unknown" is the X-Device-Name header fallback — too ambiguous to
            // treat two sessions carrying it as the same physical device.
            if (string.IsNullOrEmpty(deviceName) || deviceName == "Unknown") return;

            for (int i = _devices.Count - 1; i >= 0; i--)
            {
                var stale = _devices[i];
                if (stale.SessionId == sessionId || stale.DeviceName != deviceName) continue;

                _devices.RemoveAt(i);
                GamePeekConstants.Log($"[WS] Evicted stale session {stale.SessionId} — {stale.DeviceName} reconnected as session {sessionId}");

                if (_hostSessionId == stale.SessionId)
                {
                    _hostSessionId = _devices.Count > 0 ? _devices[0].SessionId : null;
                    if (_hostSessionId != null)
                        GamePeekConstants.Log($"[Auth] Host transferred to {_devices[0].DeviceName}");
                }

#if UNITY_WEBRTC
                if (_webRtcSessionId == stale.SessionId)
                    TearDownWebRTC();
#endif

                // Usually a no-op (the connection is already dead) but ends the
                // session cleanly if it is somehow still alive. Deferred a tick
                // (same pattern as the delayed closes in OnHelloReceived) so a
                // dead peer's close-handshake timeout cannot stall this hello.
                var staleServer  = _wsServer;
                var staleSession = stale.SessionId;
                EditorApplication.delayCall += () => staleServer?.CloseSession(staleSession);

                DeviceDisconnected?.Invoke(stale);
            }
        }

        /// <summary>
        /// Handles a hello handshake. Runs on the main thread (via the update drain).
        /// </summary>
        private void OnHelloReceived(string sessionId, HelloMessage hello)
        {
            // Evict any half-open session this device left behind on an abrupt
            // drop BEFORE gating, so a zombie entry cannot occupy the free slot.
            // The name falls back to the connect-time entry (X-Device-Name
            // header) when the hello omits one.
            string deviceName = hello?.deviceName;
            if (string.IsNullOrEmpty(deviceName))
            {
                int connectIdx = _devices.FindIndex(d => d.SessionId == sessionId);
                if (connectIdx >= 0) deviceName = _devices[connectIdx].DeviceName;
            }
            EvictStaleSessions(sessionId, deviceName);

            // Index of this session in _devices (recomputed after eviction). The
            // connect event is enqueued before any message from the same session,
            // so the device is normally already in the list.
            int deviceIdx = _devices.FindIndex(d => d.SessionId == sessionId);

            // ── Multi-device Pro gate ─────────────────────────────────────────
            // Streaming to more than one concurrent device is a Pro feature. The
            // first device is never gated; every additional one must claim Pro
            // in its hello. "Additional" means another session has already
            // completed a hello — not raw connect order — so a lingering zombie
            // session or a connection that never helloed cannot push the only
            // real device out of the free slot. This is honor-system — the tier
            // is client-asserted — consistent with the rest of the tier model.
            // Old app versions ignore unknown message types and simply see the close.
            if (hello?.tier != "pro" &&
                _devices.Exists(d => d.SessionId != sessionId && d.HelloReceived))
            {
                GamePeekConstants.Log($"[Auth] {hello?.deviceName ?? sessionId} rejected — multi-device streaming requires Pro.");
                _wsServer?.SendToSession(sessionId, "{\"type\":\"pro_required\"}");
                // Close on the next editor tick so the message flushes first
                // (same pattern as webrtc_unavailable below).
                var gatedServer = _wsServer;
                EditorApplication.delayCall += () => gatedServer?.CloseSession(sessionId);
                return;
            }

            // First device to send hello becomes the host (controls config and input).
            if (_hostSessionId == null)
            {
                _hostSessionId = sessionId;
                GamePeekConstants.Log($"[Auth] Host session set: {hello?.deviceName ?? sessionId}");
            }

            // Refresh the session's DeviceInfo from the hello payload. IsPro is
            // applied unconditionally — a Pro device that sends no deviceName must
            // still be recognised as Pro. The name stored at connect-time (the
            // X-Device-Name header fallback) is kept when the hello omits one.
            // HelloReceived marks the session as a real GamePeek client for the
            // Pro gate above.
            if (deviceIdx >= 0)
            {
                var old = _devices[deviceIdx];
                _devices[deviceIdx] = new DeviceInfo
                {
                    SessionId     = old.SessionId,
                    DeviceName    = string.IsNullOrEmpty(hello?.deviceName) ? old.DeviceName : hello.deviceName,
                    IPAddress     = old.IPAddress,
                    ConnectedAt   = old.ConnectedAt,
                    IsPro         = hello?.tier == "pro",
                    HelloReceived = true,
                };
                DeviceConnected?.Invoke(_devices[deviceIdx]);
            }

#if UNITY_WEBRTC
            if (hello?.client == "flutter_webrtc")
            {
                var socketMode = (SocketMode)EditorPrefs.GetInt(GamePeekConstants.PrefSocketMode, (int)SocketMode.WebRTC);
                if (socketMode == SocketMode.WebRTC)
                {
                    if (Application.isPlaying)
                        StartWebRTCNegotiation(sessionId);
                    else
                    {
                        // WebRTC's sync-context callback loop only runs in play mode; starting
                        // negotiation in edit mode causes native callbacks to post to a null or
                        // idle context, crashing/freezing the editor.  Tell the app first so it
                        // falls back to JPEG instantly instead of sitting out its ~12 s WebRTC
                        // timeout, then close so it reconnects cleanly once play mode begins.
                        _wsServer?.SendToSession(sessionId,
                            "{\"type\":\"webrtc_unavailable\",\"reason\":\"editor-not-playing\"}");
                        // Close on the next editor tick so the message flushes first.
                        var server = _wsServer;
                        EditorApplication.delayCall += () => server?.CloseSession(sessionId);
                    }
                }
                else
                    _wsServer?.CloseSession(sessionId);
                return;
            }
#endif
            GamePeekConstants.Log($"[WS] Hello from {hello?.client ?? "unknown"} ({hello?.deviceName ?? "?"}) session {sessionId}");

            // Tell the new client whether the editor is currently in Play Mode.
            var playModeJson = UnityEngine.JsonUtility.ToJson(
                new PlayModeMessage { type = "playmode", playing = Application.isPlaying });
            _wsServer?.SendToSession(sessionId, playModeJson);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            // Fire only after the transition is complete so Application.isPlaying is correct.
            if (change != PlayModeStateChange.EnteredPlayMode &&
                change != PlayModeStateChange.EnteredEditMode) return;

            BroadcastPlayMode();
        }

        private void BroadcastPlayMode()
        {
            var json = UnityEngine.JsonUtility.ToJson(
                new PlayModeMessage { type = "playmode", playing = Application.isPlaying });
            _wsServer?.BroadcastText(json);
        }

        private void HandleTouch(TouchMessage msg)
        {
            if (msg == null) return;

            // Keep the Game View focused so the Input System processes injected events.
            // Focus only on "began" — doing it for every message (dozens per second
            // during a drag) repeatedly steals focus from whatever the user is editing.
            if (Application.isPlaying)
            {
                _gameViewFocusWarningLogged = false; // reset when we enter Play Mode
                if (msg.phase == "began")
                    GameViewSize.GetMainGameView()?.Focus();
            }
            else if (!_gameViewFocusWarningLogged)
            {
                _gameViewFocusWarningLogged = true;
                GamePeekConstants.LogWarning("[Input] Touch received but the Editor is not in Play Mode — Input System events will not be processed. Enter Play Mode or click the Game View to enable input.");
            }

            GamePeekConstants.Log($"[Input] Touch phase={msg.phase} x={msg.x:F3} y={msg.y:F3} finger={msg.fingerId}");
            InputInjector.InjectTouch(msg.phase, msg.x, msg.y, msg.fingerId);
            var pos = new Vector2(msg.x, msg.y);
            GamePeekInput.OnTouch?.Invoke(pos);
            GamePeekInput.OnTouchDetailed?.Invoke(msg.fingerId, msg.phase, pos);
        }

        // ── WebRTC orchestration ──────────────────────────────────────────────
#if UNITY_WEBRTC

        /// <summary>
        /// Routes JSON messages received via the WebRTC DataChannel to the
        /// appropriate input injector or responds to ping messages.
        /// Must be called on the Unity main thread (via Enqueue).
        /// </summary>
        private void HandleInputJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                var base_ = UnityEngine.JsonUtility.FromJson<BaseMessage>(json);
                switch (base_?.type)
                {
                    case "touch":
                        HandleTouch(UnityEngine.JsonUtility.FromJson<TouchMessage>(json));
                        break;
                    case "gyro":
                        var g = UnityEngine.JsonUtility.FromJson<GyroMessage>(json);
                        InputInjector.InjectGyro(g.x, g.y, g.z);
                        break;
                    case "accel":
                        var a = UnityEngine.JsonUtility.FromJson<AccelMessage>(json);
                        InputInjector.InjectAccelerometer(a.x, a.y, a.z);
                        break;
                    case "config":
                        var cfg = UnityEngine.JsonUtility.FromJson<ConfigMessage>(json);
                        GamePeekConstants.Log($"[WS] Received: {json}");
                        OnConfigReceived(_webRtcSessionId ?? string.Empty, cfg);
                        break;
                    case "ping":
                        // DataChannel ping — respond with pong so Flutter can measure RTT.
                        var p = UnityEngine.JsonUtility.FromJson<PingPongMessage>(json);
                        _wsServer?.SendToSession(_webRtcSessionId ?? string.Empty,
                            UnityEngine.JsonUtility.ToJson(new PingPongMessage { type = "pong", ts = p.ts }));
                        break;
                }
            }
            catch (Exception ex)
            {
                GamePeekConstants.LogWarning($"[DC] Failed to handle input: {ex.Message}");
            }
        }

        private void StartWebRTCNegotiation(string sessionId)
        {
            GamePeekConstants.Log($"[WebRTC] Starting negotiation for session {sessionId}");

            TearDownWebRTC();

            _webRtcSessionId = sessionId;

            // Stop JPEG pipeline immediately — WebRTC will carry video.
            if (_capture != null) _capture.UseWebRTC = true;

            _webRtcStreamer = new WebRTCStreamer(Config.Width, Config.Height, Config.FpsCap,
                Config.MaxBitrateKbps, Config.WebRtcStunUrl);
            var streamer = _webRtcStreamer;
            var activeSessionId = sessionId;

            _webRtcStreamer.OfferReady += sdp =>
            {
                if (_webRtcStreamer != streamer || _webRtcSessionId != activeSessionId) return;
                var payload = UnityEngine.JsonUtility.ToJson(new WsOfferMsg { type = "offer", sdp = sdp });
                _wsServer?.SendToSession(activeSessionId, payload);
                GamePeekConstants.Log("[WebRTC] Offer sent to Flutter.");
            };

            _webRtcStreamer.IceCandidateReady += (candidate, sdpMid, sdpMLineIndex) =>
            {
                if (_webRtcStreamer != streamer || _webRtcSessionId != activeSessionId) return;
                var payload = UnityEngine.JsonUtility.ToJson(new WsCandidateMsg
                {
                    type           = "candidate",
                    candidate      = candidate,
                    sdpMid         = sdpMid,
                    sdpMLineIndex  = sdpMLineIndex,
                });
                _wsServer?.SendToSession(activeSessionId, payload);
            };

            _webRtcStreamer.Connected += () => Enqueue(() =>
            {
                if (_webRtcStreamer != streamer || _webRtcSessionId != activeSessionId) return;
                WebRtcActive = true;
                GamePeekConstants.Log("[WebRTC] P2P connection established — video flowing.");
            });

            _webRtcStreamer.Disconnected += () => Enqueue(() =>
            {
                if (_webRtcStreamer != streamer || _webRtcSessionId != activeSessionId) return;
                GamePeekConstants.Log("[WebRTC] P2P connection lost, reverting to JPEG.");
                TearDownWebRTC();
                // Resume JPEG pipeline
                if (_capture != null) _capture.UseWebRTC = false;
            });

            _webRtcStreamer.DataChannelMessage += json => Enqueue(() =>
            {
                if (_webRtcStreamer != streamer || _webRtcSessionId != activeSessionId) return;
                HandleInputJson(json);
            });

            try
            {
                _webRtcStreamer.StartNegotiation();
            }
            catch (Exception ex)
            {
                GamePeekConstants.LogError($"[WebRTC] StartNegotiation failed: {ex.Message}");
                TearDownWebRTC();
            }
        }

        private void TearDownWebRTC()
        {
            if (_webRtcStreamer == null) return;
            _webRtcStreamer.Dispose();
            _webRtcStreamer   = null;
            _webRtcSessionId  = null;
            WebRtcActive      = false;
            if (_capture != null) _capture.UseWebRTC = false;
            SmoothedRtt = 0f;
            _rttSamples.Clear();
        }

        private void OnAnswerReceived(string sessionId, string sdp)
        {
            if (sessionId != _webRtcSessionId) return;
            GamePeekConstants.Log("[WebRTC] SDP answer received from Flutter.");
            _webRtcStreamer?.SetRemoteAnswer(sdp);
        }

        private void OnCandidateReceived(string sessionId, string candidate, string sdpMid, int sdpMLineIndex)
        {
            if (sessionId != _webRtcSessionId) return;
            _webRtcStreamer?.AddIceCandidate(candidate, sdpMid, sdpMLineIndex);
        }

#endif // UNITY_WEBRTC

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetState(ConnectionState newState)
        {
            if (State == newState) return;
            State = newState;
            StateChanged?.Invoke(newState);
        }

        /// <summary>Enqueues an action to be executed on the next main-thread update.</summary>
        private void Enqueue(Action action) => _mainThreadQueue.Enqueue(action);
    }
}
