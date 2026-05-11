mergeInto(LibraryManager.library, {
  RNBO_Init: function(patcherUrlPtr, depsUrlPtr) {
    const patcherUrl = UTF8ToString(patcherUrlPtr);
    const depsUrl = UTF8ToString(depsUrlPtr);

    if (!window.__lautirRnbo) window.__lautirRnbo = {};
    const state = window.__lautirRnbo;
    state.ready = false;
    state.lastError = "";

    const fail = (e) => {
      state.lastError = (e && e.message) ? e.message : ("" + e);
      console.error("RNBO init failed:", e);
      state.ready = false;
    };

    const ensureRnboScript = async () => {
      if (window.RNBO && window.RNBO.createDevice) return;
      await new Promise((resolve, reject) => {
        const el = document.createElement("script");
        // Match patch export meta.rnboversion (lautirSynth uses 1.3.3).
        el.src = "https://cdn.cycling74.com/rnbo/1.3.3/rnbo.min.js";
        el.async = true;
        el.onload = resolve;
        el.onerror = reject;
        document.head.appendChild(el);
      });
    };

    const init = async () => {
      await ensureRnboScript();

      // Create or reuse AudioContext. Must be resumed from user gesture.
      if (!state.audioContext) {
        const WAContext = window.AudioContext || window.webkitAudioContext;
        state.audioContext = new WAContext();
      }
      if (state.audioContext.state !== "running") {
        await state.audioContext.resume();
      }

      const patcher = await (await fetch(patcherUrl)).json();
      const deps = await (await fetch(depsUrl)).json();

      // RNBO.createDevice expects { context, patcher } — see Cycling '74 docs (not audioContext).
      const device = await RNBO.createDevice({ context: state.audioContext, patcher });
      device.node.connect(state.audioContext.destination);

      state.device = device;

      // Load any exported buffer dependencies.
      if (device.loadDataBufferDependencies) {
        await device.loadDataBufferDependencies(deps);
      }

      state.ready = true;
      state.lastError = "";
    };

    init().catch(fail);
  },

  RNBO_IsReady: function() {
    return (window.__lautirRnbo && window.__lautirRnbo.ready) ? 1 : 0;
  },

  RNBO_LastError: function() {
    const msg = (window.__lautirRnbo && window.__lautirRnbo.lastError) ? window.__lautirRnbo.lastError : "";
    const len = lengthBytesUTF8(msg) + 1;
    const ptr = _malloc(len);
    stringToUTF8(msg, ptr, len);
    return ptr;
  },

  RNBO_SetParamById: function(paramIdPtr, value) {
    const id = UTF8ToString(paramIdPtr);
    const state = window.__lautirRnbo;
    if (!state || !state.device || !state.ready) return 0;
    const dev = state.device;
    const p = dev.parametersById && dev.parametersById.get
      ? dev.parametersById.get(id)
      : null;
    if (!p) return 0;
    p.value = value;
    return 1;
  },

  RNBO_SendMessage: function(tagPtr, value) {
    const tag = UTF8ToString(tagPtr);
    const state = window.__lautirRnbo;
    if (!state || !state.device || !state.ready) return 0;
    // RNBO.TimeNow is numeric 0 — do not use !RNBO.TimeNow (that skips all messages).
    if (!RNBO.MessageEvent) return 0;
    const t = (typeof RNBO.TimeNow !== "undefined" && RNBO.TimeNow !== null) ? RNBO.TimeNow : 0;
    const ev = new RNBO.MessageEvent(t, tag, [ value ]);
    state.device.scheduleEvent(ev);
    return 1;
  }
});

