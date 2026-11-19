using System;
using System.Collections.Generic;
using OptimizationGame.Interfaces;
using UnityEngine;

namespace OptimizationGame.Telemetry
{
    /// <summary>
    /// Sistema puro de telemetría (MET-001.1). Arma el contexto de la sesión, guarda el
    /// consentimiento, reparte cada evento a los sinks y mide el rendimiento.
    ///
    /// Se registra en el CustomUpdateManager como always-tick (pauseWithGameplay: false) para
    /// contar el tiempo de sesión también en menús; solo muestrea frames mientras hay juego.
    /// Sin asignaciones por frame: los frames van a un buffer preasignado y los percentiles se
    /// calculan cada PerfSampleSeconds sobre una copia también preasignada.
    /// </summary>
    public sealed class TelemetrySystem : ITickable, IDisposable
    {
        private const string PlayerIdKey = "telemetry_player_id";
        private const string ConsentKey = "telemetry_consent"; // 1 sí · 0 no · ausente = no eligió
        private const int MaxFrameSamples = 4096;

        private readonly TelemetryConfig _config;
        private readonly Func<int> _enemiesAlive;
        private readonly List<ITelemetrySink> _sinks = new List<ITelemetrySink>(2);

        private readonly float[] _frameMs = new float[MaxFrameSamples];
        private readonly float[] _sortBuffer = new float[MaxFrameSamples];
        private int _frameCount;

        private bool _sampling;
        private float _perfTimer;
        private float _flushTimer;
        private float _sessionSeconds;
        private int _runsStarted;
        private bool _disposed;

        public TelemetryContext Context { get; }
        public bool Consent { get; private set; }
        public bool HasConsentDecision => PlayerPrefs.HasKey(ConsentKey);
        public float RunSeconds { get; private set; }

        public TelemetrySystem(TelemetryConfig config, Func<int> enemiesAlive)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _enemiesAlive = enemiesAlive;

            string environment = Application.isEditor ? "editor" : _config.Environment;
            Context = new TelemetryContext(
                Application.version,
                environment,
                Guid.NewGuid().ToString("N"),
                LoadOrCreatePlayerId(),
                WebBeacon.BrowserFamily());

            Consent = HasConsentDecision ? PlayerPrefs.GetInt(ConsentKey) == 1 : _config.ConsentWhenUnset;

            bool canSend = !Application.isEditor || _config.SendFromEditor;
            if (canSend && _config.UnityAnalyticsEnabled)
                _sinks.Add(new UnityAnalyticsTelemetrySink(_config.UgsEnvironment));
            if (canSend && _config.SheetsEnabled)
                _sinks.Add(new SheetsTelemetrySink(_config.SheetsEndpointUrl, _config.SheetsToken));

            for (int i = 0; i < _sinks.Count; i++)
                _sinks[i].Initialize(Context, Consent);
        }

        // --- API de eventos ---------------------------------------------------------------

        public void Track(TelemetryEvent telemetryEvent)
        {
            if (_disposed || !Consent || telemetryEvent == null)
                return;

            for (int i = 0; i < _sinks.Count; i++)
                _sinks[i].Send(telemetryEvent);
        }

        /// <summary>session_start: la build cargó y el menú es visible.</summary>
        public void TrackSessionStart()
        {
            Track(new TelemetryEvent("session_start")
                .With("load_ms", (int)WebBeacon.MillisecondsSincePageStart())
                .With("screen_w", Screen.width)
                .With("screen_h", Screen.height)
                .With("quality", CurrentQualityName()));
            PrepareSessionEnd();
        }

        public void SetConsent(bool granted)
        {
            PlayerPrefs.SetInt(ConsentKey, granted ? 1 : 0);
            PlayerPrefs.Save();
            Consent = granted;
            for (int i = 0; i < _sinks.Count; i++)
                _sinks[i].SetConsent(granted);
        }

        // --- Partida y muestreo -----------------------------------------------------------

        public void NotifyRunStarted()
        {
            _runsStarted++;
            RunSeconds = 0f;
            _frameCount = 0;
            _perfTimer = 0f;
        }

        public int RunsStarted => _runsStarted;

        /// <summary>Muestrea frames solo con juego activo (no en menú, pausa ni pantallas finales).</summary>
        public void SetSampling(bool active)
        {
            _sampling = active;
        }

        /// <summary>perf_sample inmediato (fin de oleada). Sin frames suficientes, no emite.</summary>
        public void EmitPerfSampleNow()
        {
            EmitPerfSample();
        }

        public void Flush()
        {
            for (int i = 0; i < _sinks.Count; i++)
                _sinks[i].Flush();
            PrepareSessionEnd();
        }

        public void Tick(float deltaTime)
        {
            if (_disposed)
                return;

            _sessionSeconds += deltaTime;

            if (_sampling)
            {
                RunSeconds += deltaTime;
                if (_frameCount < MaxFrameSamples)
                    _frameMs[_frameCount++] = deltaTime * 1000f;

                _perfTimer += deltaTime;
                if (_perfTimer >= _config.PerfSampleSeconds)
                    EmitPerfSample();
            }

            _flushTimer += deltaTime;
            if (_flushTimer >= _config.FlushSeconds)
            {
                _flushTimer = 0f;
                Flush();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Flush();
            for (int i = 0; i < _sinks.Count; i++)
                _sinks[i].Dispose();
            _sinks.Clear();
            _disposed = true;
        }

        // --- Internos ---------------------------------------------------------------------

        private void EmitPerfSample()
        {
            _perfTimer = 0f;
            int count = _frameCount;
            _frameCount = 0;
            if (count < 10)
                return;

            Array.Copy(_frameMs, _sortBuffer, count);
            Array.Sort(_sortBuffer, 0, count);
            float medianMs = _sortBuffer[count / 2];
            float p95Ms = _sortBuffer[Mathf.Min(count - 1, (int)(count * 0.95f))];

            Track(new TelemetryEvent("perf_sample")
                .With("fps_p50", medianMs > 0f ? 1000f / medianMs : 0f)
                .With("fps_p05", p95Ms > 0f ? 1000f / p95Ms : 0f)
                .With("frame_ms_p95", p95Ms)
                .With("enemies_alive", _enemiesAlive != null ? _enemiesAlive() : 0)
                .With("quality", CurrentQualityName()));
        }

        private void PrepareSessionEnd()
        {
            if (!Consent)
                return;

            var sessionEnd = new TelemetryEvent("session_end")
                .With("session_s", (int)_sessionSeconds)
                .With("runs", _runsStarted);
            for (int i = 0; i < _sinks.Count; i++)
                _sinks[i].PrepareSessionEnd(sessionEnd);
        }

        private static string LoadOrCreatePlayerId()
        {
            string id = PlayerPrefs.GetString(PlayerIdKey, string.Empty);
            if (!string.IsNullOrEmpty(id))
                return id;

            id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PlayerIdKey, id);
            PlayerPrefs.Save();
            return id;
        }

        private static string CurrentQualityName()
        {
            int level = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;
            return level >= 0 && level < names.Length ? names[level] : level.ToString();
        }
    }
}
