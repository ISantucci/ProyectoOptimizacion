// Puente de telemetría para la build Web (MET-001.1 / RQ-001.5).
// - NowMs: tiempo desde el inicio de la carga de la página (incluye la descarga de la build).
// - BrowserFamily: solo la familia del navegador, nunca el user agent entero.
// - SetPayload: guarda el último session_end; sale con sendBeacon en pagehide, una sola vez.
mergeInto(LibraryManager.library, {
  TelemetryBeacon_NowMs: function () {
    return (typeof performance !== 'undefined' && performance.now) ? performance.now() : 0;
  },

  TelemetryBeacon_BrowserFamily: function () {
    var ua = (typeof navigator !== 'undefined' && navigator.userAgent) ? navigator.userAgent : '';
    var family = 'other';
    if (ua.indexOf('Edg/') >= 0) family = 'edge';
    else if (ua.indexOf('Firefox/') >= 0) family = 'firefox';
    else if (ua.indexOf('Chrome/') >= 0 || ua.indexOf('CriOS/') >= 0) family = 'chrome';
    else if (ua.indexOf('Safari/') >= 0) family = 'safari';
    var size = lengthBytesUTF8(family) + 1;
    var buffer = _malloc(size);
    stringToUTF8(family, buffer, size);
    return buffer;
  },

  TelemetryBeacon_SetPayload: function (urlPtr, payloadPtr) {
    var url = UTF8ToString(urlPtr);
    var payload = UTF8ToString(payloadPtr);
    if (typeof window === 'undefined') return;
    if (!window.__ogTelemetryBeacon) {
      window.__ogTelemetryBeacon = { url: url, payload: payload, sent: false };
      window.addEventListener('pagehide', function () {
        var beacon = window.__ogTelemetryBeacon;
        if (!beacon || beacon.sent || !beacon.payload || !navigator.sendBeacon) return;
        try {
          beacon.sent = navigator.sendBeacon(beacon.url, new Blob([beacon.payload], { type: 'text/plain' }));
        } catch (e) { /* la telemetría nunca rompe el juego */ }
      });
    }
    window.__ogTelemetryBeacon.url = url;
    window.__ogTelemetryBeacon.payload = payload;
  }
});
