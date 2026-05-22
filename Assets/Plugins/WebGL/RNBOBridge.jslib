mergeInto(LibraryManager.library, {
  RNBO_Init: function(instanceIndex, patcherUrlPtr, depsUrlPtr) {
    const patcherUrl = UTF8ToString(patcherUrlPtr);
    const depsUrl = UTF8ToString(depsUrlPtr);

    if (!window.__lautirRnbo) {
      window.__lautirRnbo = {
        instances: {},
        patcherCache: {},
        depsCache: {},
        audioContext: null
      };
    }
    const st = window.__lautirRnbo;
    const key = String(instanceIndex);
    if (!st.instances[key]) {
      st.instances[key] = {
        ready: false,
        initStarted: false,
        lastError: "",
        device: null
      };
    }
    const slot = st.instances[key];

    if (slot.ready) return;
    if (slot.initStarted) return;
    slot.initStarted = true;
    slot.lastError = "";

    const fail = (e) => {
      slot.lastError = (e && e.message) ? e.message : ("" + e);
      console.error("RNBO init failed (instance " + instanceIndex + "):", e);
      slot.ready = false;
      slot.initStarted = false;
    };

    const ensureRnboScript = async () => {
      if (window.RNBO && window.RNBO.createDevice) return;
      await new Promise((resolve, reject) => {
        const el = document.createElement("script");
        el.src = "https://cdn.cycling74.com/rnbo/1.3.3/rnbo.min.js";
        el.async = true;
        el.onload = resolve;
        el.onerror = reject;
        document.head.appendChild(el);
      });
    };

    const init = async () => {
      await ensureRnboScript();

      if (!st.audioContext) {
        const WAContext = window.AudioContext || window.webkitAudioContext;
        st.audioContext = new WAContext();
      }
      if (st.audioContext.state !== "running") {
        await st.audioContext.resume();
      }

      let patcher = st.patcherCache[patcherUrl];
      if (!patcher) {
        patcher = await (await fetch(patcherUrl)).json();
        st.patcherCache[patcherUrl] = patcher;
      }

      let deps = st.depsCache[depsUrl];
      if (!deps) {
        deps = await (await fetch(depsUrl)).json();
        st.depsCache[depsUrl] = deps;
      }

      const device = await RNBO.createDevice({ context: st.audioContext, patcher });
      device.node.connect(st.audioContext.destination);

      if (device.loadDataBufferDependencies) {
        await device.loadDataBufferDependencies(deps);
      }

      slot.device = device;
      slot.ready = true;
      slot.lastError = "";
    };

    init().catch(fail);
  },

  RNBO_IsReady: function(instanceIndex) {
    const st = window.__lautirRnbo;
    if (!st || !st.instances) return 0;
    const slot = st.instances[String(instanceIndex)];
    return (slot && slot.ready) ? 1 : 0;
  },

  RNBO_LastError: function(instanceIndex) {
    const st = window.__lautirRnbo;
    let msg = "";
    if (st && st.instances) {
      const slot = st.instances[String(instanceIndex)];
      if (slot && slot.lastError) msg = slot.lastError;
    }
    const len = lengthBytesUTF8(msg) + 1;
    const ptr = _malloc(len);
    stringToUTF8(msg, ptr, len);
    return ptr;
  },

  RNBO_SetParamById: function(instanceIndex, paramIdPtr, value) {
    const id = UTF8ToString(paramIdPtr);
    const st = window.__lautirRnbo;
    if (!st || !st.instances) return 0;
    const slot = st.instances[String(instanceIndex)];
    if (!slot || !slot.ready || !slot.device) return 0;
    const p = slot.device.parametersById && slot.device.parametersById.get
      ? slot.device.parametersById.get(id)
      : null;
    if (!p) return 0;
    p.value = value;
    return 1;
  },

  RNBO_SendMessage: function(instanceIndex, tagPtr, value) {
    const tag = UTF8ToString(tagPtr);
    const st = window.__lautirRnbo;
    if (!st || !st.instances) return 0;
    const slot = st.instances[String(instanceIndex)];
    if (!slot || !slot.ready || !slot.device) return 0;
    if (!RNBO.MessageEvent) return 0;
    const t = (typeof RNBO.TimeNow !== "undefined" && RNBO.TimeNow !== null) ? RNBO.TimeNow : 0;
    const ev = new RNBO.MessageEvent(t, tag, [ value ]);
    slot.device.scheduleEvent(ev);
    return 1;
  }
});
