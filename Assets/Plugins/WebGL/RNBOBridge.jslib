mergeInto(LibraryManager.library, {
  RNBO_ResumeAudioOnGesture: function() {
    if (!window.__lautirRnbo) {
      window.__lautirRnbo = { instances: {}, patcherCache: {}, depsCache: {}, audioContext: null };
    }
    const ctx = (typeof WEBAudio !== "undefined" && WEBAudio.audioContext)
      ? WEBAudio.audioContext
      : window.__lautirRnbo.audioContext;
    if (!ctx) return;
    if (ctx.state !== "running") {
      ctx.resume().then(function() {
        const st = window.__lautirRnbo;
        if (!st || !st.instances || typeof RNBO === "undefined") return;
        Object.keys(st.instances).forEach(function(key) {
          const slot = st.instances[key];
          if (!slot || !slot.ready || !slot.device) return;
          const tNow = (typeof RNBO.TimeNow !== "undefined" && RNBO.TimeNow !== null) ? RNBO.TimeNow : 0;
          if (RNBO.TempoEvent) slot.device.scheduleEvent(new RNBO.TempoEvent(tNow, 60));
          if (RNBO.TransportEvent) {
            slot.device.scheduleEvent(new RNBO.TransportEvent(tNow, 0));
            slot.device.scheduleEvent(new RNBO.TransportEvent(tNow, 1));
          }
        });
      });
    }
  },

  RNBO_Init: function(instanceIndex, patcherUrlPtr, depsUrlPtr) {
    const patcherUrl = UTF8ToString(patcherUrlPtr);
    const depsUrl = UTF8ToString(depsUrlPtr);

    if (!window.__lautirRnbo) {
      window.__lautirRnbo = { instances: {}, patcherCache: {}, depsCache: {}, audioContext: null };
    }
    const st = window.__lautirRnbo;
    const key = String(instanceIndex);
    if (!st.instances[key]) {
      st.instances[key] = { ready: false, initStarted: false, lastError: "", device: null, bufferLoadState: 0 };
    }
    const slot = st.instances[key];
    if (slot.ready || slot.initStarted) return;
    slot.initStarted = true;
    slot.lastError = "";

    const fail = function(e) {
      slot.lastError = (e && e.message) ? e.message : ("" + e);
      console.error("RNBO init failed (instance " + instanceIndex + "):", e);
      slot.ready = false;
      slot.initStarted = false;
    };

    const getAudioContext = function() {
      if (typeof WEBAudio !== "undefined" && WEBAudio.audioContext) return WEBAudio.audioContext;
      return st.audioContext;
    };

    const waitForRunningAudioContext = async function() {
      const deadline = Date.now() + 120000;
      while (Date.now() < deadline) {
        let ctx = getAudioContext();
        if (!ctx && typeof WEBAudio === "undefined") {
          const WAContext = window.AudioContext || window.webkitAudioContext;
          ctx = new WAContext();
          st.audioContext = ctx;
        }
        if (ctx) {
          if (ctx.state !== "running") {
            try { await ctx.resume(); } catch (e) {}
          }
          if (ctx.state === "running") return ctx;
        }
        await new Promise(function(r) { setTimeout(r, 200); });
      }
      throw new Error("AudioContext did not reach running state within 120s — click the page to start audio");
    };

    const startTransport = function(device) {
      if (!device || typeof RNBO === "undefined") return;
      const tNow = (typeof RNBO.TimeNow !== "undefined" && RNBO.TimeNow !== null) ? RNBO.TimeNow : 0;
      if (RNBO.TempoEvent) device.scheduleEvent(new RNBO.TempoEvent(tNow, 60));
      if (RNBO.TransportEvent) {
        device.scheduleEvent(new RNBO.TransportEvent(tNow, 0));
        device.scheduleEvent(new RNBO.TransportEvent(tNow, 1));
        console.log("[LAUTIR] Transport running (instance " + instanceIndex + ")");
      }
    };

    const ensureRnboScript = async function() {
      if (window.RNBO && window.RNBO.createDevice) return;
      await new Promise(function(resolve, reject) {
        const el = document.createElement("script");
        el.src = "https://cdn.cycling74.com/rnbo/1.4.3/rnbo.min.js";
        el.async = true;
        el.onload = resolve;
        el.onerror = reject;
        document.head.appendChild(el);
      });
    };

    const init = async function() {
      await ensureRnboScript();

      // Share Unity's WEBAudio context — a separate AudioContext is inaudible when Unity audio works.
      const ctx = await waitForRunningAudioContext();
      st.audioContext = ctx;

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

      // Resolve dependency paths relative to dependencies.json (Unity: …/StreamingAssets/LautirSong/).
      const depsBaseUrl = depsUrl.replace(/[^/]+$/, "");
      const resolvedDeps = (deps || []).map(function(dep) {
        if (!dep || !dep.file || /^https?:\/\//i.test(dep.file)) return dep;
        return { id: dep.id, file: depsBaseUrl + dep.file.replace(/^\//, "") };
      });

      const createOpts = { context: ctx, patcher: patcher };
      if (RNBO.ParameterNotificationSetting) {
        createOpts.options = { parameterNotificationSetting: RNBO.ParameterNotificationSetting.All };
      }

      // loadbang runs during createDevice — only schedule after AudioContext is running.
      const device = await RNBO.createDevice(createOpts);
      device.node.connect(ctx.destination);

      if (device.loadDataBufferDependencies && resolvedDeps.length > 0) {
        const loadResults = await device.loadDataBufferDependencies(resolvedDeps);
        loadResults.forEach(function(r) {
          if (r.type !== "success") {
            console.warn("[LAUTIR] deps load failed id=" + r.id + ": " + (r.error || "unknown"));
          }
        });
      }

      startTransport(device);

      slot.device = device;
      slot.ready = true;
      slot.lastError = "";
      const sharedUnity = (typeof WEBAudio !== "undefined" && WEBAudio.audioContext === ctx);
      console.log("[LAUTIR] RNBO ready instance " + instanceIndex + " | AudioContext=" + ctx.state + " | sharedUnity=" + sharedUnity);
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
    if (RNBO.TempoEvent) slot.device.scheduleEvent(new RNBO.TempoEvent(tNow, 60));
    if (RNBO.TransportEvent) {
      slot.device.scheduleEvent(new RNBO.TransportEvent(tNow, 0));
      slot.device.scheduleEvent(new RNBO.TransportEvent(tNow, 1));
    }
    slot.device.scheduleEvent(new RNBO.MessageEvent(tNow, tag, [ value ]));
    return 1;
  },

  // bufferLoadState: 0 idle, 1 loading, 2 ok, 3 failed
  RNBO_LoadDataBufferFromUrl: function(instanceIndex, bufferIdPtr, urlPtr) {
    const bufferId = UTF8ToString(bufferIdPtr);
    const url = UTF8ToString(urlPtr);
    const st = window.__lautirRnbo;
    if (!st || !st.instances) return 0;
    const slot = st.instances[String(instanceIndex)];
    if (!slot || !slot.ready || !slot.device) {
      if (slot) slot.lastError = "RNBO device not ready";
      return 0;
    }
    if (slot.bufferLoadState === 1) return 0;

    const ctx = st.audioContext;
    if (!ctx) {
      slot.lastError = "AudioContext not available";
      return 0;
    }

    slot.bufferLoadState = 1;
    slot.lastError = "";

    (async function() {
      try {
        const response = await fetch(url);
        if (!response.ok) throw new Error("buffer fetch failed: " + url + " (" + response.status + ")");
        const contentType = response.headers.get("content-type") || "";
        const arrayBuf = await response.arrayBuffer();
        if (arrayBuf.byteLength < 12) {
          throw new Error("buffer too small (" + arrayBuf.byteLength + " bytes) from " + url);
        }
        const head = new Uint8Array(arrayBuf, 0, 4);
        const riff = String.fromCharCode(head[0], head[1], head[2], head[3]);
        if (riff !== "RIFF" && contentType.indexOf("audio") === -1) {
          throw new Error("not audio data from " + url + " (content-type: " + contentType + ", sig: " + riff + ")");
        }
        const audioBuf = await ctx.decodeAudioData(arrayBuf.slice(0));
        if (!slot.device.setDataBuffer) throw new Error("device.setDataBuffer not available");
        await slot.device.setDataBuffer(bufferId, audioBuf);
        slot.bufferLoadState = 2;
        console.log("[LAUTIR] Loaded buffer \"" + bufferId + "\" from " + url + " (instance " + instanceIndex + ")");
      } catch (e) {
        slot.bufferLoadState = 3;
        slot.lastError = (e && e.message) ? e.message : ("" + e);
        console.error("[LAUTIR] LoadDataBuffer failed (instance " + instanceIndex + "):", e);
      }
    })();
    return 1;
  },

  RNBO_GetDataBufferLoadState: function(instanceIndex) {
    const st = window.__lautirRnbo;
    if (!st || !st.instances) return 0;
    const slot = st.instances[String(instanceIndex)];
    if (!slot) return 0;
    return slot.bufferLoadState || 0;
  },

  RNBO_ResetDataBufferLoadState: function(instanceIndex) {
    const st = window.__lautirRnbo;
    if (!st || !st.instances) return;
    const slot = st.instances[String(instanceIndex)];
    if (slot) slot.bufferLoadState = 0;
  }
});
