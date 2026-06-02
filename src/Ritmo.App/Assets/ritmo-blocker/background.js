// Extensión de bloqueo de Ritmo (#8, bloqueo "duro" a nivel de red).
// Consulta el estado del bloqueo a Ritmo (servidor local 127.0.0.1) y, si hay una sesión de
// concentración activa con webs bloqueadas, bloquea esos dominios con declarativeNetRequest.
// Si Ritmo no responde (cerrado o sin sesión), no bloquea nada (fail-open).

const ENDPOINT = "http://127.0.0.1:47615/state";
const ALARM = "ritmo-poll";

// El periodo mínimo real de las alarmas MV3 es ~30 s; el bloqueo "blando" de Ritmo cubre el
// instante inicial minimizando la ventana mientras la extensión sincroniza.
function ensureAlarm() {
  chrome.alarms.create(ALARM, { periodInMinutes: 0.5 });
}

chrome.runtime.onInstalled.addListener(() => { ensureAlarm(); poll(); });
chrome.runtime.onStartup.addListener(() => { ensureAlarm(); poll(); });
chrome.alarms.onAlarm.addListener((a) => { if (a.name === ALARM) poll(); });

async function poll() {
  let active = false;
  let domains = [];
  try {
    const res = await fetch(ENDPOINT, { cache: "no-store" });
    if (res.ok) {
      const state = await res.json();
      active = !!state.active;
      domains = Array.isArray(state.domains) ? state.domains : [];
    }
  } catch (e) {
    active = false;
    domains = [];
  }
  await applyRules(active ? domains : []);
}

async function applyRules(domains) {
  const existing = await chrome.declarativeNetRequest.getDynamicRules();
  const removeRuleIds = existing.map((r) => r.id);
  const addRules = domains.slice(0, 200).map((d, i) => ({
    id: i + 1,
    priority: 1,
    action: { type: "block" },
    condition: {
      urlFilter: "||" + String(d).toLowerCase().trim(),
      resourceTypes: ["main_frame", "sub_frame"]
    }
  }));
  await chrome.declarativeNetRequest.updateDynamicRules({ removeRuleIds, addRules });
}
