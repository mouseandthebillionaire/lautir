/**
 * Local RNBO listen test — open in your browser and hear the patch.
 *
 * Usage:
 *   node Tools/rnbo-listen.mjs
 *
 * Then open the printed URL and click "Start / resume audio".
 */
import { createServer } from "http";
import { readFileSync, existsSync } from "fs";
import { join, extname } from "path";
import { fileURLToPath } from "url";
import { dirname } from "path";
import { execSync } from "child_process";

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, "..");
const exportDir = join(root, "Assets/StreamingAssets/LautirSong");

const mime = {
  ".html": "text/html",
  ".json": "application/json",
  ".wav": "audio/wav",
};

function serve(res, path) {
  if (!existsSync(path)) {
    res.writeHead(404);
    res.end("not found: " + path);
    return;
  }
  res.writeHead(200, { "Content-Type": mime[extname(path)] || "text/plain" });
  res.end(readFileSync(path));
}

const server = createServer((req, res) => {
  res.setHeader("Access-Control-Allow-Origin", "*");
  if (req.url === "/" || req.url === "/index.html") {
    serve(res, join(__dirname, "rnbo-listen.html"));
  } else if (req.url === "/export.json") {
    serve(res, join(exportDir, "lautirSong.export.json"));
  } else if (req.url === "/deps.json") {
    serve(res, join(exportDir, "dependencies.json"));
  } else if (req.url?.startsWith("/media/")) {
    serve(res, join(exportDir, req.url.slice(1)));
  } else {
    res.writeHead(404);
    res.end("404");
  }
});

const PORT = Number(process.env.PORT) || 8765;
await new Promise((resolve, reject) => {
  server.listen(PORT, "127.0.0.1", () => resolve());
  server.on("error", reject);
});
const url = `http://127.0.0.1:${PORT}/`;

console.log("\nRNBO listen test");
console.log("  " + url);
console.log("Keep this terminal open while listening.");
console.log("Re-export in Max → copy export + media/ into StreamingAssets/LautirSong/ → refresh → Start.");
console.log("If dependencies.json is [], put pad WAVs in media/ and fix ids (pad0, pad1).\n");

try {
  execSync(`open "${url}"`, { stdio: "ignore" });
} catch {
  // linux/windows: user opens URL manually
}

process.on("SIGINT", () => {
  server.close();
  process.exit(0);
});
