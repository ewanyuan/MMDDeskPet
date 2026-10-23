import fs from "node:fs";
import http from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, "../..");
const modelUrlPath = "/assets/default/uka/MS_Uka.pmx";
const motionUrlPath = "/assets/default/motions/mmddeskpet_groove_20s.vmd";

function contentType(filePath) {
  switch (path.extname(filePath).toLowerCase()) {
    case ".html": return "text/html; charset=utf-8";
    case ".js": return "text/javascript; charset=utf-8";
    case ".wasm": return "application/wasm";
    case ".png": return "image/png";
    case ".css": return "text/css";
    default: return "application/octet-stream";
  }
}

function safeJoin(root, relative) {
  const resolvedRoot = path.resolve(root);
  const resolved = path.resolve(resolvedRoot, relative);
  if (resolved !== resolvedRoot && !resolved.startsWith(resolvedRoot + path.sep)) {
    return null;
  }
  return resolved;
}

function startServer() {
  const hits = [];
  const server = http.createServer((request, response) => {
    const url = new URL(request.url || "/", `http://${request.headers.host}`);
    let pathname = "";
    try {
      pathname = decodeURIComponent(url.pathname);
    } catch {
      response.writeHead(400, { "Content-Type": "text/plain" });
      response.end("bad encoding");
      return;
    }
    const relative = pathname.replace(/^\/+/, "");
    const file = safeJoin(repoRoot, relative);
    const exists = file && fs.existsSync(file) && fs.statSync(file).isFile();
    const status = exists ? 200 : 404;
    hits.push({ path: pathname, status });
    if (!exists) {
      response.writeHead(404, { "Content-Type": "text/plain", "Access-Control-Allow-Origin": "*" });
      response.end("missing");
      return;
    }
    response.writeHead(status, {
      "Content-Type": contentType(file),
      "Access-Control-Allow-Origin": "*",
      "Cache-Control": "no-store",
    });
    response.end(fs.readFileSync(file));
  });
  return new Promise((resolve) => {
    server.listen(0, "127.0.0.1", () => {
      resolve({ server, port: server.address().port, hits });
    });
  });
}

async function posted(page) {
  return page.evaluate(() => window.__posted.slice());
}

async function waitForMessage(page, predicate, timeoutMs, label) {
  const start = Date.now();
  let messages = [];
  while (Date.now() - start < timeoutMs) {
    messages = await posted(page);
    const found = messages.find(predicate);
    if (found) {
      return found;
    }
    await page.waitForTimeout(100);
  }
  throw new Error(`${label} timed out after ${timeoutMs}ms. messages=${JSON.stringify(messages).slice(0, 2500)}`);
}

const { server, port, hits } = await startServer();
const browser = await chromium.launch({
  headless: true,
  args: [
    "--host-resolver-rules=MAP pet.local 127.0.0.1",
    "--use-gl=angle",
    "--use-angle=swiftshader",
    "--enable-unsafe-swiftshader",
    "--ignore-gpu-blocklist",
  ],
});
const page = await browser.newPage({ viewport: { width: 480, height: 760 } });
const consoleNotes = [];
page.on("console", (message) => {
  if (message.type() === "error") {
    consoleNotes.push(message.text());
  }
});
page.on("pageerror", (error) => consoleNotes.push(String(error)));
await page.addInitScript(() => {
  const listeners = [];
  window.__posted = [];
  window.chrome = window.chrome || {};
  window.chrome.webview = {
    postMessage(message) {
      window.__posted.push(JSON.parse(JSON.stringify(message)));
    },
    addEventListener(type, listener) {
      if (type === "message") {
        listeners.push(listener);
      }
    },
    removeEventListener(type, listener) {
      const index = listeners.indexOf(listener);
      if (index >= 0) {
        listeners.splice(index, 1);
      }
    },
  };
  window.__hostSend = (message) => {
    for (const listener of listeners.slice()) {
      listener({ data: message });
    }
  };
});

let failed = false;
try {
  await page.goto(`http://pet.local:${port}/web/index.html`, { waitUntil: "domcontentloaded" });
  await waitForMessage(page, (message) => message.type === "runtime-ready", 45000, "runtime-ready");
  await page.evaluate((payload) => window.__hostSend(payload), {
    type: "load-character",
    index: 0,
    url: `http://pet.local:${port}${modelUrlPath}`,
    requestId: "uka",
  });
  const model = await waitForMessage(
    page,
    (message) => (message.type === "model-loaded" || message.type === "runtime-error") && message.requestId === "uka",
    90000,
    "uka model",
  );
  if (model.type !== "model-loaded") {
    throw new Error(`model failed: ${JSON.stringify(model).slice(0, 1500)}`);
  }
  await page.evaluate((payload) => window.__hostSend(payload), {
    type: "load-motion",
    name: "groove",
    url: `http://pet.local:${port}${motionUrlPath}`,
    loop: true,
    requestId: "groove",
  });
  const motion = await waitForMessage(
    page,
    (message) => (message.type === "motion-loaded" || message.type === "runtime-error") && message.requestId === "groove",
    30000,
    "groove motion",
  );
  if (motion.type !== "motion-loaded") {
    throw new Error(`motion failed: ${JSON.stringify(motion).slice(0, 1500)}`);
  }
  await page.evaluate((payload) => window.__hostSend(payload), { type: "play" });
  let first = null;
  let last = null;
  const deadline = Date.now() + 8000;
  while (Date.now() < deadline) {
    const frame = await page.evaluate(() => {
      const runtime = window.MmdDeskPetRuntime && window.MmdDeskPetRuntime.runtime;
      const value = runtime && runtime.currentFrameTime;
      return typeof value === "number" && Number.isFinite(value) ? value : null;
    });
    if (typeof frame === "number") {
      first = first === null ? frame : first;
      last = frame;
      if (last - first >= 2 || last >= 3) {
        break;
      }
    }
    await page.waitForTimeout(100);
  }
  const missing = hits.filter((hit) => hit.status === 404);
  const advanced = typeof first === "number" && typeof last === "number" && (last - first >= 2 || last >= 3);
  const report = {
    modelName: model.modelName,
    boneCount: model.boneCount,
    morphs: Array.isArray(model.morphs) ? model.morphs.length : 0,
    boneTracks: motion.boneTracks,
    endFrame: motion.endFrame,
    loop: motion.loop,
    frames: [first, last],
    advanced,
    missing,
    consoleNotes: consoleNotes.slice(0, 20),
  };
  console.log(JSON.stringify(report, null, 2));
  if (!advanced || missing.length > 0 || motion.loop !== true || !(Number(motion.boneTracks) > 0)) {
    failed = true;
  }
} catch (error) {
  console.error(error);
  console.error(JSON.stringify({
    missing: hits.filter((hit) => hit.status === 404).slice(0, 40),
    consoleNotes: consoleNotes.slice(0, 20),
  }, null, 2));
  failed = true;
} finally {
  await browser.close();
  server.close();
}
process.exit(failed ? 1 : 0);
