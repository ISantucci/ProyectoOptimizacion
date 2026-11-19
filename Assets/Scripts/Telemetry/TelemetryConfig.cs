using UnityEngine;

namespace OptimizationGame.Telemetry
{
    /// <summary>
    /// Configuración de la telemetría (MET-001.1). Un asset por tipo de build: el borrador
    /// de itch (web_draft, testers avisados) y la página pública (web_public) difieren en el
    /// environment y en qué pasa si el jugador nunca eligió sobre el consentimiento.
    /// En el Editor el environment se reporta siempre como "editor".
    /// </summary>
    [CreateAssetMenu(fileName = "TelemetryConfig", menuName = "OptimizationGame/Telemetry Config")]
    public class TelemetryConfig : ScriptableObject
    {
        [Tooltip("web_draft (borrador de itch, playtest) o web_public (página pública).")]
        [SerializeField] private string _environment = "web_draft";

        [Tooltip("Envía los eventos a Unity Analytics (UGS). Requiere el proyecto vinculado en Edit > Project Settings > Services y los eventos creados en el Event Manager.")]
        [SerializeField] private bool _unityAnalyticsEnabled = true;

        [Tooltip("Environment de Unity Gaming Services. Vacío = production (el que existe por defecto).")]
        [SerializeField] private string _ugsEnvironment = "";

        [Tooltip("Envía los eventos a la planilla de Google Sheets (Apps Script).")]
        [SerializeField] private bool _sheetsEnabled = true;

        [Tooltip("URL /exec del Apps Script desplegado como aplicación web.")]
        [SerializeField] private string _sheetsEndpointUrl = "";

        [Tooltip("Token compartido con el Apps Script. Filtra tráfico ajeno; no es seguridad fuerte.")]
        [SerializeField] private string _sheetsToken = "";

        [Tooltip("Si el jugador nunca eligió, ¿se mide? true solo en el borrador con testers avisados; false en la página pública hasta que exista el aviso de consentimiento.")]
        [SerializeField] private bool _consentWhenUnset = true;

        [Tooltip("Mandar datos también desde el Editor (environment = editor). Apagado por defecto para no ensuciar la lectura.")]
        [SerializeField] private bool _sendFromEditor = false;

        [SerializeField, Min(5f)] private float _perfSampleSeconds = 15f;
        [SerializeField, Min(5f)] private float _flushSeconds = 20f;

        public string Environment => _environment;
        public bool UnityAnalyticsEnabled => _unityAnalyticsEnabled;
        public string UgsEnvironment => _ugsEnvironment;
        public bool SheetsEnabled => _sheetsEnabled && !string.IsNullOrEmpty(_sheetsEndpointUrl);
        public string SheetsEndpointUrl => _sheetsEndpointUrl;
        public string SheetsToken => _sheetsToken;
        public bool ConsentWhenUnset => _consentWhenUnset;
        public bool SendFromEditor => _sendFromEditor;
        public float PerfSampleSeconds => _perfSampleSeconds;
        public float FlushSeconds => _flushSeconds;
    }
}
