using System;
using System.Collections.Generic;
using System.Globalization;

namespace OptimizationGame.Telemetry
{
    /// <summary>
    /// Un evento del tracking plan (MET-001.1): nombre + parámetros primitivos
    /// (string, int, float, bool). Se crean pocos por minuto (oleadas, partidas, muestras de
    /// rendimiento): nunca por frame.
    /// </summary>
    public sealed class TelemetryEvent
    {
        private readonly List<KeyValuePair<string, object>> _params = new List<KeyValuePair<string, object>>(8);

        public string Name { get; }
        public string Timestamp { get; }
        public IReadOnlyList<KeyValuePair<string, object>> Params => _params;

        public TelemetryEvent(string name)
        {
            Name = name;
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        public TelemetryEvent With(string key, string value)
        {
            _params.Add(new KeyValuePair<string, object>(key, value ?? string.Empty));
            return this;
        }

        public TelemetryEvent With(string key, int value)
        {
            _params.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        public TelemetryEvent With(string key, float value)
        {
            _params.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        public TelemetryEvent With(string key, bool value)
        {
            _params.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }
    }

    /// <summary>Contexto común a todos los eventos de la sesión (MET-001.1, "Contexto").</summary>
    public sealed class TelemetryContext
    {
        public string Version { get; }
        public string Environment { get; }
        public string SessionId { get; }
        public string PlayerId { get; }
        public string Browser { get; }

        public TelemetryContext(string version, string environment, string sessionId, string playerId, string browser)
        {
            Version = version;
            Environment = environment;
            SessionId = sessionId;
            PlayerId = playerId;
            Browser = browser;
        }
    }
}
