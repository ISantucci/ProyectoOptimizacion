using System.Runtime.InteropServices;
using UnityEngine;

namespace OptimizationGame.Telemetry
{
    /// <summary>
    /// Puente con el navegador (Assets/Plugins/WebGL/TelemetryBeacon.jslib). Fuera de Web
    /// devuelve valores de respaldo y no hace nada. No guarda estado: no es un singleton.
    /// </summary>
    public static class WebBeacon
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern double TelemetryBeacon_NowMs();
        [DllImport("__Internal")] private static extern string TelemetryBeacon_BrowserFamily();
        [DllImport("__Internal")] private static extern void TelemetryBeacon_SetPayload(string url, string payload);
#endif

        /// <summary>Milisegundos desde que el navegador empezó a cargar la página (incluye la descarga).</summary>
        public static double MillisecondsSincePageStart()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return TelemetryBeacon_NowMs();
#else
            return Time.realtimeSinceStartup * 1000.0;
#endif
        }

        /// <summary>Solo la familia del navegador (chrome, firefox, safari, edge, other): nada identificable.</summary>
        public static string BrowserFamily()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return TelemetryBeacon_BrowserFamily();
#else
            return Application.isEditor ? "editor" : "standalone";
#endif
        }

        /// <summary>Guarda el cuerpo que se manda con navigator.sendBeacon al cerrar la pestaña.</summary>
        public static void SetPayload(string url, string payload)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TelemetryBeacon_SetPayload(url, payload);
#endif
        }
    }
}
