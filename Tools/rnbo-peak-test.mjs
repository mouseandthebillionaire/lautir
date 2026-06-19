/**
 * Headless RNBO peak test — loads lautirSong export and measures device output.
 * Usage: node tools/rnbo-peak-test.mjs
 */
import { createServer } from "http";
import { readFileSync, existsSync } from "fs";
import { join, extname } from "path";
import { fileURLToPath } from "url";
import { dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, "..");
const exportDir = join(root, "Assets/StreamingAssets/LautirSong");

const mime = {
  ".json": "application/json",
  ".html": "text/html",
  ".wav": "audio/wav",
};

function serveFile(res, path) {
  if (!existsSync(path)) {
    res.writeHead(404);
    res.end("not found: " + path);
    return;
  }
  const body = readFileSync(path);
  res.writeHead(200, { "Content-Type": mime[extname(path)] || "text/plain" });
  res.end(body);
}

const server = createServer((req, res) => {
  res.setHeader("Access-Control-Allow-Origin", "*");
  if (req.url === "/") {
    serveFile(res, join(__dirname, "rnbo-peak-test.html"));
  } else if (req.url === "/export.json") {
    serveFile(res, join(exportDir, "lautirSong.export.json"));
  } else if (req.url === "/deps.json") {
    serveFile(res, join(exportDir, "dependencies.json"));
  } else if (req.url?.startsWith("/media/")) {
    serveFile(res, join(exportDir, req.url.slice(1)));
  } else {
    res.writeHead(404);
    res.end("404");
  }
});

await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
const port = server.address().port;
const url = `http://127.0.0.1:${port}/`;

const { chromium } = await import("playwright");
const browser = await chromium.launch();
const page = await browser.newPage();

page.on("console", (msg) => console.log("[page]", msg.text()));

await page.goto(url, { waitUntil: "networkidle" });
const result = await page.evaluate(async () => {
  const wait = (ms) => new Promise((r) => setTimeout(r, ms));
  const set = (d, id, v) => {
    const p = d.parametersById.get(id);
    if (!p) return false;
    p.value = v;
    return true;
  };

  const peak = (node, seconds = 2) =>
    new Promise((resolve) => {
      const ctx = node.context;
      const analyser = ctx.createAnalyser();
      analyser.fftSize = 2048;
      node.connect(analyser);
      const buf = new Float32Array(analyser.fftSize);
      let max = 0;
      let frames = 0;
      const maxFrames = Math.ceil((seconds * ctx.sampleRate) / analyser.fftSize);
      const tick = () => {
        analyser.getFloatTimeDomainData(buf);
        for (let i = 0; i < buf.length; i++) max = Math.max(max, Math.abs(buf[i]));
        if (++frames >= maxFrames) {
          analyser.disconnect();
          resolve(max);
          return;
        }
        requestAnimationFrame(tick);
      };
      tick();
    });

  const ctx = new AudioContext();
  await ctx.resume();
  const patchRes = await fetch("/export.json");
  const patcher = await patchRes.json();
  const depsRes = await fetch("/deps.json");
  let deps = await depsRes.json();
  if ((!deps || deps.length === 0) && patcher.desc?.externalDataRefs?.length) {
    deps = patcher.desc.externalDataRefs.map((ref) => ({
      id: ref.id,
      file: `media/${ref.file}`,
    }));
  }

  const device = await RNBO.createDevice({
    context: ctx,
    patcher,
    options: {
      parameterNotificationSetting: RNBO.ParameterNotificationSetting?.All ?? 0,
    },
  });
  device.node.connect(ctx.destination);
  if (device.loadDataBufferDependencies) await device.loadDataBufferDependencies(deps);

  const messages = [];
  device.messageEvent?.subscribe((ev) => messages.push(`${ev.tag}:${ev.payload}`));

  const paramMethods = {};
  for (const id of ["melody_0/begin", "melody_0/note", "melody_0/melody"]) {
    const p = device.parametersById.get(id);
    paramMethods[id] = p ? Object.getOwnPropertyNames(Object.getPrototypeOf(p)).sort() : null;
  }

  const tNow = RNBO.TimeNow ?? 0;
  device.scheduleEvent(new RNBO.TempoEvent(tNow, 60));
  device.scheduleEvent(new RNBO.TransportEvent(tNow, 1));

  const baseline = await peak(device.node, 0.5);

  // Mirror jslib bootstrap (scoped polyphonic params)
  set(device, "melody_0/begin", 0);
  set(device, "melody_0/begin", 1);
  set(device, "melody_1/begin", 0);
  set(device, "melody_1/begin", 1);
  device.scheduleEvent(new RNBO.MessageEvent(tNow, "rnboReceive", [1]));
  await wait(1500);

  const afterBootstrap = await peak(device.node, 2);

  // Full param set like PlayMelody
  set(device, "melody_0/noteDensity", 7);
  set(device, "melody_0/melody", 10);
  set(device, "melody_0/timbre", 500);
  set(device, "melody_0/note", 2);
  set(device, "melody_0/leftDelay", 300);
  set(device, "melody_0/rightDelay", 400);
  set(device, "melody_0/feedback", 0.5);
  set(device, "limiterGain", 0);
  set(device, "melody_0/begin", 0);
  set(device, "melody_0/begin", 1);
  await wait(1500);
  const afterFull = await peak(device.node, 2);

  // Param edges that should bang noteLogic / phrase chain in the patch
  set(device, "melody_0/note", 1);
  set(device, "melody_0/note", 3);
  set(device, "melody_0/melody", 1);
  set(device, "melody_0/melody", 5);
  set(device, "melody_0/timbre", 100);
  set(device, "melody_0/timbre", 500);
  await wait(3000);
  const afterParamEdges = await peak(device.node, 2);

  // Re-trigger begin after long wait (covers delay 1000 + noteLogic delay 100)
  set(device, "melody_0/begin", 0);
  await wait(50);
  set(device, "melody_0/begin", 1);
  await wait(3000);
  const afterLongBegin = await peak(device.node, 2);

  // Probe RNBO event types + parameter scheduling
  const rnboKeys = Object.keys(RNBO).filter((k) => /event|param|bang/i.test(k)).sort();
  let afterParamEvent = null;
  if (RNBO.ParameterEvent) {
    const t = RNBO.TimeNow ?? 0;
    device.scheduleEvent(new RNBO.ParameterEvent(t, "melody_0/note", 2));
    device.scheduleEvent(new RNBO.ParameterEvent(t, "melody_0/begin", 1));
    await wait(3000);
    afterParamEvent = await peak(device.node, 2);
  }

  // Boost externally — if peak rises, patch generates signal but master *~ gain is stuck at 0
  const boost = ctx.createGain();
  boost.gain.value = 20;
  device.node.disconnect();
  device.node.connect(boost);
  boost.connect(ctx.destination);
  await wait(2000);
  const afterExternalBoost = await peak(device.node, 2);

  const paramIds = [...device.parametersById.keys()];
  return {
    baseline,
    afterBootstrap,
    afterFull,
    afterParamEdges,
    afterLongBegin,
    afterParamEvent,
    rnboKeys,
    afterExternalBoost,
    messages: messages.slice(0, 20),
    paramMethods,
    paramCount: paramIds.length,
    ctxState: ctx.state,
  };
});

console.log("\n=== RNBO peak test results ===");
console.log(JSON.stringify(result, null, 2));

await browser.close();
server.close();
