/**
 * ProyectoOptimizacion — endpoint de telemetría para Google Sheets.
 * MET-001.1 (plan de medición) · RQ-001.5 (telemetría mínima) · SOL-001.5.
 *
 * Cómo se instala (una vez):
 *   1. En la planilla: Extensiones > Apps Script, pegar este archivo entero.
 *   2. Ejecutar setup() una vez (crea las hojas "eventos" y "resumen").
 *   3. Implementar > Nueva implementación > Aplicación web:
 *        Ejecutar como: yo · Quién tiene acceso: cualquier usuario.
 *   4. Copiar la URL /exec en TelemetryConfig.asset (_sheetsEndpointUrl).
 *
 * El juego manda lotes { k: TOKEN, events: [...] } por POST text/plain.
 * Solo acepta los 9 eventos del plan; todo lo demás se descarta.
 */
const TOKEN = '07ce622849e95e6b09329e76';
const SHEET_EVENTS = 'eventos';
const SHEET_SUMMARY = 'resumen';
const MAX_BATCH = 200;
const EVENTS = ['session_start', 'run_start', 'wave_start', 'wave_end', 'player_death',
                'run_end', 'pickup_collected', 'perf_sample', 'session_end'];
const BASE = ['received_at', 'timestamp', 'event', 'version', 'environment',
              'session_id', 'player_id', 'browser'];
const PARAMS = ['load_ms', 'screen_w', 'screen_h', 'quality', 'run_index', 'wave_index',
                'room_id', 'duration_s', 'hp_pct', 'kills', 'killer_enemy_id', 'run_time_s',
                'result', 'waves_cleared', 'kind', 'fps_p50', 'fps_p05', 'frame_ms_p95',
                'enemies_alive', 'session_s', 'runs'];
const HEADER = BASE.concat(PARAMS);

/** Crea las hojas y el tablero de lectura. Se puede correr de nuevo sin perder datos. */
function setup() {
  eventsSheet_();
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  let s = ss.getSheetByName(SHEET_SUMMARY);
  if (!s) s = ss.insertSheet(SHEET_SUMMARY);
  s.clear();

  // Columnas de "eventos": C=event, E=environment, F=session_id, I=load_ms, M=run_index,
  // U=result, Y=fps_p05. El environment a leer se elige en B1.
  const rows = [
    ['environment', 'web_draft'],
    ['', ''],
    ['sesiones (session_start)', '=COUNTUNIQUE(IFERROR(FILTER(eventos!F2:F, eventos!C2:C="session_start", eventos!E2:E=B1)))'],
    ['sesiones con una oleada completa', '=COUNTUNIQUE(IFERROR(FILTER(eventos!F2:F, eventos!C2:C="wave_end", eventos!E2:E=B1)))'],
    ['KPI arranque efectivo', '=IFERROR(B4/B3, 0)'],
    ['', ''],
    ['carga mediana (s)', '=IFERROR(MEDIAN(FILTER(eventos!I2:I, eventos!C2:C="session_start", eventos!E2:E=B1))/1000, "")'],
    ['carga p75 (s)', '=IFERROR(PERCENTILE(FILTER(eventos!I2:I, eventos!C2:C="session_start", eventos!E2:E=B1), 0.75)/1000, "")'],
    ['fps p05 mínimo (perf_sample)', '=IFERROR(MIN(FILTER(eventos!Y2:Y, eventos!C2:C="perf_sample", eventos!E2:E=B1)), "")'],
    ['', ''],
    ['sesiones con revancha (run_index >= 2)', '=COUNTUNIQUE(IFERROR(FILTER(eventos!F2:F, eventos!C2:C="run_start", eventos!M2:M>=2, eventos!E2:E=B1)))'],
    ['tasa de revancha', '=IFERROR(B11/B3, 0)'],
    ['victorias', '=COUNTIFS(eventos!C2:C, "run_end", eventos!U2:U, "victory", eventos!E2:E, B1)'],
    ['derrotas', '=COUNTIFS(eventos!C2:C, "run_end", eventos!U2:U, "defeat", eventos!E2:E, B1)'],
    ['abandonos', '=COUNTIFS(eventos!C2:C, "run_end", eventos!U2:U, "abandon", eventos!E2:E, B1)']
  ];
  s.getRange(1, 1, rows.length, 2).setValues(rows.map(function (r) { return [r[0], '']; }));
  rows.forEach(function (r, i) {
    const cell = s.getRange(i + 1, 2);
    if (String(r[1]).charAt(0) === '=') cell.setFormula(r[1]); else cell.setValue(r[1]);
  });
  s.getRange('B5').setNumberFormat('0%');
  s.getRange('B12').setNumberFormat('0%');
  s.setColumnWidth(1, 300);
}

function doPost(e) {
  try {
    const body = JSON.parse((e && e.postData && e.postData.contents) || '{}');
    if (body.k !== TOKEN) return reply_(403, 'token');

    const list = Array.isArray(body.events) ? body.events.slice(0, MAX_BATCH) : [];
    const now = new Date();
    const rows = [];
    list.forEach(function (ev) {
      if (!ev || EVENTS.indexOf(ev.event) < 0) return;
      const p = ev.p || {};
      const row = [now, text_(ev.timestamp), ev.event, text_(ev.version), text_(ev.environment),
                   text_(ev.session_id), text_(ev.player_id), text_(ev.browser)];
      PARAMS.forEach(function (key) {
        const v = p.hasOwnProperty(key) ? p[key] : '';
        row.push(typeof v === 'string' ? v.slice(0, 100) : v);
      });
      rows.push(row);
    });

    if (rows.length > 0) {
      const lock = LockService.getScriptLock();
      lock.waitLock(10000);
      try {
        const sheet = eventsSheet_();
        sheet.getRange(sheet.getLastRow() + 1, 1, rows.length, HEADER.length).setValues(rows);
      } finally {
        lock.releaseLock();
      }
    }
    return reply_(200, rows.length);
  } catch (err) {
    return reply_(500, String(err));
  }
}

// GET a la URL /exec: crea o rehace las hojas "eventos" y "resumen" (sirve de setup y de prueba).
function doGet() {
  setup();
  return reply_(200, 'ok');
}

function eventsSheet_() {
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  let sheet = ss.getSheetByName(SHEET_EVENTS);
  if (!sheet) sheet = ss.insertSheet(SHEET_EVENTS);
  if (sheet.getLastRow() === 0) {
    sheet.appendRow(HEADER);
    sheet.setFrozenRows(1);
  }
  return sheet;
}

function text_(v) {
  return v === undefined || v === null ? '' : String(v).slice(0, 100);
}

function reply_(code, msg) {
  return ContentService
    .createTextOutput(JSON.stringify({ ok: code === 200, code: code, msg: msg }))
    .setMimeType(ContentService.MimeType.JSON);
}
