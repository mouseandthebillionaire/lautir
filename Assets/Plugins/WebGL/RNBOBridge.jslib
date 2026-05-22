mergeInto(LibraryManager.library, {
  // Call synchronously from a tap/key handler so mobile browsers unlock audio.
  RNBO_ResumeAudioOnGesture: function() {
    if (!window.__lautirRnbo) {
      window.__lautirRnbo = {
        instances: {},
        patcherCache: {},
        depsCache: {},
        audioContext: null
      };
    }
    const st = window.__lautirRnbo;
    if (!st.audioContext) {
      const WAContext = window.AudioContext || window.webkitAudioContext;
      st.audioContext = new WAContext();
    }
    if (st.audioContext.state !== "running") {
      st.audioContext.resume();
    }
  },

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
        // Must match lautirSynth.export.json meta.rnboversion (currently 1.4.3).
        el.src = "https://cdn.cycling74.com/rnbo/1.4.3/rnbo.min.js";
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
        const patchRes = await fetch(patcherUrl);
        if (!patchRes.ok) throw new Error("patcher fetch failed: " + patcherUrl + " (" + patchRes.status + ")");
        patcher = await patchRes.json();
        st.patcherCache[patcherUrl] = patcher;
      }

      let deps = st.depsCache[depsUrl];
      if (!deps) {
        const depsRes = await fetch(depsUrl);
        if (!depsRes.ok) throw new Error("deps fetch failed: " + depsUrl + " (" + depsRes.status + ")");
        deps = await depsRes.json();
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

      // transportUsed: true in export — start transport inline (jslib strips top-level helpers).
      const tNow = (typeof RNBO.TimeNow !== "undefined" && RNBO.TimeNow !== null) ? RNBO.TimeNow : 0;
      if (RNBO.TransportEvent) {
        device.scheduleEvent(new RNBO.TransportEvent(tNow, 1));
        console.log("[LAUTIR] Transport running (instance " + instanceIndex + ")");
      } else {
        console.warn("[LAUTIR] TransportEvent unavailable (instance " + instanceIndex + ")");
      }

      console.log("[LAUTIR] RNBO ready instance " + instanceIndex + " | AudioContext=" + st.audioContext.state);
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
    if (!p) {
      console.warn("[LAUTIR] SetParamById unknown: " + id + " (instance " + instanceIndex + ")");
      return 0;
    }
    p.value = value;
    return 1;
  },

  RNBO_SendMessage: function(instanceIndex, tagPtr, value) {
    const tag = UTF8ToString(tagPtr);
    const st = window.__lautirRnbo;
    if (!st || !st.instances) return 0;
    const slot = st.instances[String(instanceIndex)];
    if (!slot || !slot.ready || !slot.device) {
      console.warn("[LAUTIR] SendMessage before ready: " + tag + " (instance " + instanceIndex + ")");
      return 0;
    }
    if (!RNBO.MessageEvent) {
      console.warn("[LAUTIR] RNBO.MessageEvent missing");
      return 0;
    }

    const tNow = (typeof RNBO.TimeNow !== "undefined" && RNBO.TimeNow !== null) ? RNBO.TimeNow : 0;
    if (RNBO.TransportEvent) {
      slot.device.scheduleEvent(new RNBO.TransportEvent(tNow, 1));
    }
    const ev = new RNBO.MessageEvent(tNow, tag, [ value ]);
    slot.device.scheduleEvent(ev);
    console.log("[LAUTIR] SendMessage " + tag + " → instance " + instanceIndex + " | AudioContext=" + (window.__lautirRnbo && window.__lautirRnbo.audioContext ? window.__lautirRnbo.audioContext.state : "?"));
    return 1;
  }
});
