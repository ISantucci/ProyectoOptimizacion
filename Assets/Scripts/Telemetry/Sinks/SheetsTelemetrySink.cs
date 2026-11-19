using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace OptimizationGame.Telemetry
{
    /// <summary>
    /// Manda los eventos a la planilla de Google Sheets a través de un Apps Script publicado
    /// como aplicación web (Tools/Telemetry/SheetsEndpoint.gs). Junta los eventos y los
    /// envía en lote cada FlushSeconds y al terminar una partida; nunca por evento ni por frame.
    ///
    /// POST con Content-Type text/plain: es una "simple request" y el navegador no hace
    /// preflight de CORS, que Apps Script no contesta. Si el envío falla, el lote se pierde:
    /// la telemetría nunca reintenta en loop ni bloquea el juego.
    /// </summary>
    public sealed class SheetsTelemetrySink : ITelemetrySink
    {
        private const int MaxPending = 200;

        private readonly string _url;
        private readonly string _token;
        private readonly List<string> _pending = new List<string>(64);
        private readonly StringBuilder _builder = new StringBuilder(2048);
        private TelemetryContext _context;
        private bool _consent;

        public SheetsTelemetrySink(string url, string token)
        {
            _url = url;
            _token = token ?? string.Empty;
        }

        public void Initialize(TelemetryContext context, bool consent)
        {
            _context = context;
            _consent = consent;
        }

        public void SetConsent(bool granted)
        {
            _consent = granted;
            if (!granted)
                _pending.Clear();
        }

        public void Send(TelemetryEvent telemetryEvent)
        {
            if (!_consent || telemetryEvent == null)
                return;

            if (_pending.Count >= MaxPending)
                _pending.RemoveAt(0);
            _pending.Add(SerializeEvent(telemetryEvent));
        }

        public void Flush()
        {
            if (!_consent || _pending.Count == 0 || string.IsNullOrEmpty(_url))
                return;

            string body = BuildBatch(_pending);
            _pending.Clear();
            Post(body);
        }

        public void PrepareSessionEnd(TelemetryEvent sessionEnd)
        {
            if (!_consent || sessionEnd == null || string.IsNullOrEmpty(_url))
                return;

            var single = new List<string>(1) { SerializeEvent(sessionEnd) };
            WebBeacon.SetPayload(_url, BuildBatch(single));
        }

        public void Dispose()
        {
            Flush();
        }

        private void Post(string body)
        {
            var request = new UnityWebRequest(_url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "text/plain;charset=utf-8");
            var operation = request.SendWebRequest();
            operation.completed += _ => request.Dispose();
        }

        private string BuildBatch(List<string> events)
        {
            _builder.Clear();
            _builder.Append("{\"k\":");
            AppendString(_builder, _token);
            _builder.Append(",\"events\":[");
            for (int i = 0; i < events.Count; i++)
            {
                if (i > 0) _builder.Append(',');
                _builder.Append(events[i]);
            }
            _builder.Append("]}");
            return _builder.ToString();
        }

        private string SerializeEvent(TelemetryEvent e)
        {
            var sb = new StringBuilder(256);
            sb.Append('{');
            AppendField(sb, "timestamp", e.Timestamp, first: true);
            AppendField(sb, "event", e.Name);
            AppendField(sb, "version", _context?.Version);
            AppendField(sb, "environment", _context?.Environment);
            AppendField(sb, "session_id", _context?.SessionId);
            AppendField(sb, "player_id", _context?.PlayerId);
            AppendField(sb, "browser", _context?.Browser);
            sb.Append(",\"p\":{");
            var parameters = e.Params;
            for (int i = 0; i < parameters.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendString(sb, parameters[i].Key);
                sb.Append(':');
                AppendValue(sb, parameters[i].Value);
            }
            sb.Append("}}");
            return sb.ToString();
        }

        private static void AppendField(StringBuilder sb, string key, string value, bool first = false)
        {
            if (!first) sb.Append(',');
            AppendString(sb, key);
            sb.Append(':');
            AppendString(sb, value ?? string.Empty);
        }

        private static void AppendValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case float f:
                    sb.Append(float.IsNaN(f) || float.IsInfinity(f) ? "0" : f.ToString("0.###", CultureInfo.InvariantCulture));
                    break;
                default:
                    AppendString(sb, value.ToString());
                    break;
            }
        }

        private static void AppendString(StringBuilder sb, string value)
        {
            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
