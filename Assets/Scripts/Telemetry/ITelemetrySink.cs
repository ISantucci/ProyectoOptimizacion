using System;

namespace OptimizationGame.Telemetry
{
    /// <summary>
    /// Destino de los eventos. Cada sink decide cómo y cuándo envía; ninguno puede romper el
    /// juego: si el backend falla, el sink se apaga en silencio.
    /// </summary>
    public interface ITelemetrySink : IDisposable
    {
        void Initialize(TelemetryContext context, bool consent);
        void SetConsent(bool granted);
        void Send(TelemetryEvent telemetryEvent);
        void Flush();

        /// <summary>
        /// Deja listo el evento de cierre de sesión para que salga aunque se cierre la pestaña.
        /// Los sinks que no pueden hacerlo lo ignoran.
        /// </summary>
        void PrepareSessionEnd(TelemetryEvent sessionEnd);
    }
}
