using System;
using System.Collections.Generic;
using Unity.Services.Analytics;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
#if ENABLE_UNITY_CONSENT
using UnityEngine.UnityConsent;
#endif

namespace OptimizationGame.Telemetry
{
    /// <summary>
    /// Manda los eventos a Unity Analytics (com.unity.services.analytics 6.3).
    ///
    /// En Unity 6.2+ el consentimiento va por el Developer Data framework
    /// (EndUserConsent.SetConsentState); antes, por Start/StopDataCollection. El SDK junta y
    /// sube los eventos solo (cada 60 s); Flush() adelanta la subida al terminar una partida.
    ///
    /// Requisitos fuera del código (los hace el owner): proyecto vinculado a Unity Cloud en
    /// Edit > Project Settings > Services, y los 9 eventos de MET-001.1 creados en el Event
    /// Manager del dashboard con sus parámetros. Un evento que no está definido allá llega
    /// marcado como inválido.
    ///
    /// Solo se mandan los parámetros propios del evento: Unity ya registra usuario, sesión,
    /// versión y plataforma, y version/session_id/player_id/browser quedan para la planilla.
    /// </summary>
    public sealed class UnityAnalyticsTelemetrySink : ITelemetrySink
    {
        private const int MaxQueued = 64;

        private readonly string _ugsEnvironment;
        private readonly List<TelemetryEvent> _queue = new List<TelemetryEvent>(16);
        private bool _consent;
        private bool _ready;
        private bool _failed;

        public UnityAnalyticsTelemetrySink(string ugsEnvironment)
        {
            _ugsEnvironment = ugsEnvironment;
        }

        public void Initialize(TelemetryContext context, bool consent)
        {
            _consent = consent;
            InitializeServicesAsync();
        }

        public void SetConsent(bool granted)
        {
            _consent = granted;
            if (!granted)
                _queue.Clear();
            if (_ready)
                ApplyConsent();
        }

        public void Send(TelemetryEvent telemetryEvent)
        {
            if (_failed || !_consent || telemetryEvent == null)
                return;

            if (!_ready)
            {
                if (_queue.Count < MaxQueued)
                    _queue.Add(telemetryEvent);
                return;
            }

            Record(telemetryEvent);
        }

        public void Flush()
        {
            if (!_ready || _failed || !_consent)
                return;

            try
            {
                AnalyticsService.Instance.Flush();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Telemetry] Unity Analytics Flush falló: {exception.Message}");
            }
        }

        public void PrepareSessionEnd(TelemetryEvent sessionEnd)
        {
            // Unity Analytics no tiene envío al cerrar la pestaña: session_end va solo a la planilla.
        }

        public void Dispose()
        {
            Flush();
        }

        private async void InitializeServicesAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    var options = new InitializationOptions();
                    if (!string.IsNullOrEmpty(_ugsEnvironment))
                        options.SetEnvironmentName(_ugsEnvironment);
                    await UnityServices.InitializeAsync(options);
                }

                _ready = true;
                ApplyConsent();

                for (int i = 0; i < _queue.Count; i++)
                    Record(_queue[i]);
                _queue.Clear();
            }
            catch (Exception exception)
            {
                _failed = true;
                _queue.Clear();
                Debug.LogWarning($"[Telemetry] Unity Analytics no se pudo inicializar ({exception.Message}). ¿Está vinculado el proyecto en Project Settings > Services? Los eventos siguen yendo a la planilla.");
            }
        }

        private void ApplyConsent()
        {
            try
            {
#if ENABLE_UNITY_CONSENT
                EndUserConsent.SetConsentState(new ConsentState
                {
                    AnalyticsIntent = _consent ? ConsentStatus.Granted : ConsentStatus.Denied,
                    AdsIntent = ConsentStatus.Denied
                });
#else
                if (_consent)
                    AnalyticsService.Instance.StartDataCollection();
                else
                    AnalyticsService.Instance.StopDataCollection();
#endif
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Telemetry] No se pudo aplicar el consentimiento de Unity Analytics: {exception.Message}");
            }
        }

        private static void Record(TelemetryEvent telemetryEvent)
        {
            try
            {
                var customEvent = new CustomEvent(telemetryEvent.Name);
                var parameters = telemetryEvent.Params;
                for (int i = 0; i < parameters.Count; i++)
                    customEvent.Add(parameters[i].Key, parameters[i].Value);
                AnalyticsService.Instance.RecordEvent(customEvent);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Telemetry] Unity Analytics RecordEvent({telemetryEvent.Name}) falló: {exception.Message}");
            }
        }
    }
}
