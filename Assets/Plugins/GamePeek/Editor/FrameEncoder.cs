using System;
using System.Diagnostics;
using System.Threading;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace GamePeek
{
    /// <summary>
    /// Encodes captured frames as JPEG off the Unity main thread and delivers the
    /// result to the <see cref="GamePeekWebSocketServer"/>.
    /// <para>
    /// The encoder is a single-slot pipeline: the main thread only memcpys the raw
    /// RGB24 pixels into a pooled ping-pong buffer; one long-lived background
    /// thread ("GamePeek JPEG Sender") encodes the frame with
    /// <c>ImageConversion.EncodeArrayToJPG</c> and broadcasts it. A new frame is
    /// only accepted once the previous one has been encoded <em>and</em> sent
    /// (<see cref="IsEncoding"/>), giving natural back-pressure, strict frame
    /// ordering, and zero queue growth. Steady-state operation allocates nothing
    /// on the main thread beyond the JPEG output array produced by Unity.
    /// </para>
    /// <para>
    /// <b>Threading model:</b>
    /// <list type="bullet">
    ///   <item><see cref="SubmitFrame(Texture2D)"/> and
    ///         <see cref="SubmitFrame(NativeArray{byte}, int, int)"/> are called from
    ///         the Unity <em>main</em> thread and only copy raw bytes into one of
    ///         two reused ping-pong buffers.</item>
    ///   <item>JPEG encoding and the network broadcast both run on the dedicated
    ///         sender thread; websocket-sharp handles its own thread safety.</item>
    ///   <item>Safety valve: if the off-thread encode ever throws, the encoder
    ///         permanently reverts to synchronous main-thread encoding for the
    ///         rest of the session (the sender thread then only performs the
    ///         network send).</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class FrameEncoder
    {
        // ── Encode format ─────────────────────────────────────────────────────

        // Every capture path hands us display-ready gamma (sRGB-encoded) bytes:
        // in Linear-colour-space projects the sources are sRGB render textures
        // whose ReadPixels/AsyncGPUReadback yields the gamma bytes untouched, and
        // in Gamma projects everything is gamma bytes anyway. R8G8B8_SRGB declares
        // exactly that, so EncodeArrayToJPG writes the bytes into the JPEG
        // untouched — the same pass-through EncodeToJPG performed on the old
        // TextureFormat.RGB24 / linear:false readback textures (whose underlying
        // GraphicsFormat is R8G8B8_SRGB). Declaring R8G8B8_UNorm instead would
        // mark the data as linear, and the encoder would apply a linear→sRGB
        // transfer — a second gamma pass that whitens the image.
        private const GraphicsFormat EncodeFormat = GraphicsFormat.R8G8B8_SRGB;

        // ── Latest-frame slot (producer: main thread; consumer: sender thread) ─

        private struct PendingFrame
        {
            public byte[] Raw;     // raw RGB24 gamma bytes (threaded encode path)
            public byte[] Jpeg;    // pre-encoded JPEG (main-thread fallback path)
            public int    Width;
            public int    Height;
            public int    Quality;
        }

        private readonly object _pendingLock = new object();
        private PendingFrame    _pending;
        private bool            _hasPending;

        // Ping-pong raw buffers reused across frames — the main thread memcpys the
        // next capture into one while the sender thread may still be reading the
        // other. Recreated only when the frame byte size changes.
        private readonly byte[][] _rawBuffers = new byte[2][];
        private int _rawWriteIndex;

        // ── Sender thread ─────────────────────────────────────────────────────
        private Thread         _senderThread;
        private AutoResetEvent _frameReady;
        private volatile bool  _senderRunning;

        // ── State ─────────────────────────────────────────────────────────────

        // True from frame acceptance until the sender thread has encoded AND
        // broadcast it. The capture loop's IsEncoding checks provide the
        // back-pressure that keeps the frame slot from ever growing.
        private volatile bool _busy;

        // Safety valve: EncodeArrayToJPG off the main thread is standard practice
        // but not explicitly documented as thread-safe. If the worker encode ever
        // throws, this flips permanently (one warning is logged) and every
        // subsequent frame is encoded synchronously on the main thread instead;
        // the sender thread keeps handling the network send.
        private volatile bool _threadedEncodeBroken;

        private GamePeekWebSocketServer _server;
        private volatile int _quality = 75;

        // ── Stats ─────────────────────────────────────────────────────────────
        private volatile float _lastEncodeMs;

        /// <summary>Milliseconds taken by the most recent encode operation.</summary>
        public float LastEncodeMs => _lastEncodeMs;

        /// <summary>
        /// <c>true</c> while a frame is being encoded or broadcast.
        /// The capture loop uses this to skip frames during back-pressure.
        /// </summary>
        public bool IsEncoding => _busy;

        // ── Constructor ───────────────────────────────────────────────────────

        /// <summary>
        /// Creates the encoder. <paramref name="server"/> may be null initially and
        /// set later via <see cref="SetServer"/>.
        /// </summary>
        /// <param name="server">WebSocket server to broadcast frames to.</param>
        /// <param name="quality">Initial JPEG quality [0, 100] (default 75).</param>
        public FrameEncoder(GamePeekWebSocketServer server, int quality = 75)
        {
            _server  = server;
            _quality = Mathf.Clamp(quality, 1, 100);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Sets or replaces the WebSocket server used for broadcasting.</summary>
        public void SetServer(GamePeekWebSocketServer server) => _server = server;

        /// <summary>
        /// Updates the JPEG quality used for subsequent encodes.
        /// </summary>
        /// <param name="quality">Quality value [1, 100].</param>
        public void SetQuality(int quality) => _quality = Mathf.Clamp(quality, 1, 100);

        /// <summary>
        /// Resets statistics counters (call when streaming (re-)starts).
        /// </summary>
        public void ResetStats()
        {
            _lastEncodeMs = 0f;
        }

        /// <summary>
        /// Submits a captured <see cref="Texture2D"/> for off-thread JPEG encoding
        /// and subsequent broadcast.
        /// <para>
        /// <b>Must be called from the Unity main thread.</b>
        /// The texture's CPU-side pixels are copied into a pooled buffer before
        /// this method returns; the caller keeps ownership of the texture and may
        /// reuse it for the next capture immediately.
        /// </para>
        /// </summary>
        /// <param name="texture">
        /// Texture to encode. Must be <see cref="TextureFormat.RGB24"/> with valid
        /// CPU-side pixel data (i.e. <c>ReadPixels</c> or
        /// <c>LoadRawTextureData</c> has already run — no <c>Apply()</c> needed,
        /// the encoder never touches the GPU copy).
        /// </param>
        /// <returns>
        /// <c>true</c> if the frame was accepted;
        /// <c>false</c> if the previous frame is still being encoded/sent or no
        /// client is connected (the frame is dropped, nothing is copied).
        /// </returns>
        public bool SubmitFrame(Texture2D texture)
        {
            if (texture == null) return false;
            if (texture.format != TextureFormat.RGB24)
            {
                GamePeekConstants.LogWarning(
                    $"[Encoder] Unsupported capture format {texture.format} (expected RGB24) — frame dropped.");
                return false;
            }
            return SubmitFrame(texture.GetRawTextureData<byte>(), texture.width, texture.height);
        }

        /// <summary>
        /// Submits one frame of raw RGB24 pixel data (display-ready gamma bytes,
        /// e.g. from <c>AsyncGPUReadback</c> on an sRGB render texture) for
        /// off-thread JPEG encoding and subsequent broadcast.
        /// <para>
        /// <b>Must be called from the Unity main thread.</b> The data is copied
        /// into a pooled buffer before this method returns, so the caller's
        /// <see cref="NativeArray{T}"/> may be invalidated afterwards.
        /// </para>
        /// </summary>
        /// <param name="rawRgb24">Tightly packed RGB24 pixel rows, bottom row first.</param>
        /// <param name="width">Frame width in pixels.</param>
        /// <param name="height">Frame height in pixels.</param>
        /// <returns>
        /// <c>true</c> if the frame was accepted;
        /// <c>false</c> if the previous frame is still being encoded/sent or no
        /// client is connected (the frame is dropped, nothing is copied).
        /// </returns>
        public bool SubmitFrame(NativeArray<byte> rawRgb24, int width, int height)
        {
            if (_busy) return false;
            if (_server == null || _server.ConnectedCount == 0) return false;
            if (width <= 0 || height <= 0) return false;

            int required = width * height * 3;
            if (rawRgb24.Length < required) return false;

            // Main-thread cost of the threaded path is just this memcpy into a
            // reused buffer.
            byte[] buffer = _rawBuffers[_rawWriteIndex];
            if (buffer == null || buffer.Length != required)
                _rawBuffers[_rawWriteIndex] = buffer = new byte[required];
            NativeArray<byte>.Copy(rawRgb24, buffer, required);
            _rawWriteIndex ^= 1;

            int    quality = _quality;
            byte[] jpeg    = null;

            if (_threadedEncodeBroken)
            {
                // Fallback: encode synchronously on the main thread (the documented
                // usage) and hand only the network send to the sender thread.
                jpeg = EncodeOnMainThread(buffer, width, height, quality);
                if (jpeg == null || jpeg.Length == 0) return false;
            }

            _busy = true;
            lock (_pendingLock)
            {
                _pending = new PendingFrame
                {
                    Raw     = jpeg == null ? buffer : null,
                    Jpeg    = jpeg,
                    Width   = width,
                    Height  = height,
                    Quality = quality,
                };
                _hasPending = true;
            }

            EnsureSenderThread();
            _frameReady.Set();
            return true;
        }

        /// <summary>
        /// Stops the background sender thread and clears the frame slot. Called by
        /// <see cref="FrameCapture.Stop"/> on streaming teardown and, as a safety
        /// net, before assembly reloads. Safe to call multiple times; the thread
        /// is restarted automatically should the encoder be reused afterwards.
        /// </summary>
        public void Stop()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;

            bool joined = true;
            if (_senderThread != null)
            {
                _senderRunning = false;
                _frameReady?.Set();
                joined = _senderThread.Join(1000);
                if (!joined)
                    GamePeekConstants.LogWarning(
                        "[Encoder] Sender thread did not stop within 1 s; it is a background thread and cannot keep the editor alive.");
                _senderThread = null;
            }

            // Only dispose the wait handle once the thread is provably gone.
            if (joined && _frameReady != null)
            {
                _frameReady.Dispose();
                _frameReady = null;
            }

            lock (_pendingLock)
            {
                _hasPending = false;
                _pending    = default;
            }
            _busy = false;
        }

        // ── Main-thread fallback encode ───────────────────────────────────────

        /// <summary>
        /// Synchronous main-thread encode, used only after the off-thread safety
        /// valve has tripped (see <see cref="_threadedEncodeBroken"/>).
        /// </summary>
        private byte[] EncodeOnMainThread(byte[] raw, int width, int height, int quality)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                return ImageConversion.EncodeArrayToJPG(
                    raw, EncodeFormat, (uint)width, (uint)height, 0, quality);
            }
            catch (Exception ex)
            {
                GamePeekConstants.LogWarning($"[Encoder] JPEG encode failed: {ex.Message}");
                return null;
            }
            finally
            {
                sw.Stop();
                _lastEncodeMs = (float)sw.Elapsed.TotalMilliseconds;
            }
        }

        // ── Sender thread ─────────────────────────────────────────────────────

        /// <summary>
        /// Lazily starts the single long-lived sender thread, restarting it if a
        /// previous one died. Called from the main thread only.
        /// </summary>
        private void EnsureSenderThread()
        {
            if (_senderRunning && _senderThread != null && _senderThread.IsAlive) return;

            // Belt-and-braces: ConnectionManager stops streaming (and with it this
            // encoder, via FrameCapture.Stop) before assembly reloads, but a leaked
            // sender thread must never outlive the domain. Re-armed together with
            // the thread; Stop() removes it.
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;

            _frameReady ??= new AutoResetEvent(false);
            _senderRunning = true;
            _senderThread  = new Thread(SenderLoop)
            {
                Name         = "GamePeek JPEG Sender",
                IsBackground = true,
            };
            _senderThread.Start();
        }

        /// <summary>
        /// Single long-lived consumer loop: waits for the latest-frame signal,
        /// encodes the frame (unless the main-thread fallback already did), and
        /// broadcasts it. Exactly one frame is in flight at any time — strict
        /// ordering, no queue growth.
        /// </summary>
        private void SenderLoop()
        {
            try
            {
                while (_senderRunning)
                {
                    _frameReady.WaitOne();
                    if (!_senderRunning) break;

                    PendingFrame frame;
                    lock (_pendingLock)
                    {
                        if (!_hasPending) continue;
                        frame       = _pending;
                        _pending    = default;
                        _hasPending = false;
                    }

                    try
                    {
                        byte[] jpeg = frame.Jpeg;

                        if (jpeg == null)
                        {
                            var sw = Stopwatch.StartNew();
                            try
                            {
                                jpeg = ImageConversion.EncodeArrayToJPG(
                                    frame.Raw, EncodeFormat,
                                    (uint)frame.Width, (uint)frame.Height, 0, frame.Quality);
                            }
                            catch (Exception ex)
                            {
                                // Safety valve: never retry threaded encoding this
                                // session — SubmitFrame reverts to main-thread encode.
                                _threadedEncodeBroken = true;
                                GamePeekConstants.LogWarning(
                                    "[Encoder] Off-thread JPEG encode failed — reverting to " +
                                    $"main-thread encoding for this session: {ex.Message}");
                                continue;
                            }
                            finally
                            {
                                sw.Stop();
                                _lastEncodeMs = (float)sw.Elapsed.TotalMilliseconds;
                            }
                        }

                        if (jpeg != null && jpeg.Length > 0)
                        {
                            try
                            {
                                _server?.BroadcastFrame(jpeg);
                            }
                            catch (Exception ex)
                            {
                                GamePeekConstants.LogWarning($"[Encoder] Broadcast failed: {ex.Message}");
                            }
                        }
                    }
                    finally
                    {
                        _busy = false;
                    }
                }
            }
            catch (Exception)
            {
                // WaitOne on a disposed handle after an unclean stop, or a thread
                // abort during domain unload — the loop simply ends.
                // EnsureSenderThread starts a fresh thread if the encoder is used
                // again.
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
