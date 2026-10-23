import fs from "node:fs";
import http from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, "../..");
const appRoot = process.argv[2] ? path.resolve(process.argv[2]) : repoRoot;
const outputDir = path.join(here, "output");
const fixtureRoot = path.join(outputDir, "fixtures", "Hime_&_Mikoto");

const checks = [];

function record(name, pass, detail) {
  checks.push({ name, pass, detail });
  const mark = pass ? "PASS" : "FAIL";
  console.log(`${mark} ${name}: ${detail}`);
}

function text(value) {
  const bytes = Buffer.from(value, "utf8");
  const length = Buffer.alloc(4);
  length.writeInt32LE(bytes.length, 0);
  return Buffer.concat([length, bytes]);
}

function f32(values) {
  const buffer = Buffer.alloc(4 * values.length);
  values.forEach((value, index) => buffer.writeFloatLE(value, index * 4));
  return buffer;
}

function i32(value) {
  const buffer = Buffer.alloc(4);
  buffer.writeInt32LE(value, 0);
  return buffer;
}

function u16(value) {
  const buffer = Buffer.alloc(2);
  buffer.writeUInt16LE(value, 0);
  return buffer;
}

function u8(value) {
  return Buffer.from([value & 0xff]);
}

function i8(value) {
  return Buffer.from([value & 0xff]);
}

function buildPmx() {
  const header = Buffer.concat([
    Buffer.from("PMX ", "ascii"),
    f32([2]),
    u8(8),
    u8(1),
    u8(0),
    u8(1), u8(1), u8(1), u8(1), u8(1), u8(1),
    text("Desk Pet"),
    text("Desk Pet"),
    text(""),
    text(""),
  ]);
  const vertex = (position, uv) => Buffer.concat([
    f32(position),
    f32([0, 0, 1]),
    f32(uv),
    u8(0),
    i8(0),
    f32([1]),
  ]);
  const vertices = Buffer.concat([
    i32(3),
    vertex([0, 1, 0], [0.5, 0]),
    vertex([-0.6, 0, 0], [0, 1]),
    vertex([0.6, 0, 0], [1, 1]),
  ]);
  const indices = Buffer.concat([i32(3), u8(0), u8(1), u8(2)]);
  const textures = Buffer.concat([
    i32(2),
    text("tex/肌 色.png"),
    text("clothes/Hime_&_Mikoto.png"),
  ]);
  const material = Buffer.concat([
    i32(1),
    text("body"),
    text("body"),
    f32([1, 1, 1, 1]),
    f32([0, 0, 0]),
    f32([5]),
    f32([0.6, 0.6, 0.6]),
    u8(1),
    f32([0, 0, 0, 1]),
    f32([1]),
    i8(0),
    i8(1),
    u8(1),
    u8(0),
    i8(-1),
    text(""),
    i32(3),
  ]);
  const bone = (name, position, parent) => Buffer.concat([
    text(name),
    text(name),
    f32(position),
    i8(parent),
    i32(0),
    u16(2 | 4 | 8),
    f32([0, 1, 0]),
  ]);
  const bones = Buffer.concat([
    i32(3),
    bone("center", [0, 0, 0], -1),
    bone("upper", [0, 1, 0], 0),
    bone("head", [0, 2, 0], 1),
  ]);
  const morphs = Buffer.concat([
    i32(1),
    text("blink"),
    text("blink"),
    i8(4),
    i8(1),
    i32(1),
    u8(0),
    f32([0, 0.1, 0]),
  ]);
  const display = Buffer.concat([
    i32(1),
    text("Root"),
    text("Root"),
    u8(1),
    i32(2),
    u8(0), i8(0),
    u8(1), i8(0),
  ]);
  return Buffer.concat([
    header,
    vertices,
    indices,
    textures,
    material,
    bones,
    morphs,
    display,
    i32(0),
    i32(0),
  ]);
}

function buildVmd() {
  const signature = Buffer.alloc(30);
  Buffer.from("Vocaloid Motion Data 0002", "ascii").copy(signature);
  const modelName = Buffer.alloc(20);
  Buffer.from("Desk Pet", "ascii").copy(modelName);
  const boneName = Buffer.alloc(15);
  Buffer.from("center", "ascii").copy(boneName);
  const interpolation = Buffer.alloc(64, 20);
  const frame = (frameNumber, rotationX) => {
    const buffer = Buffer.alloc(111);
    boneName.copy(buffer, 0);
    buffer.writeUInt32LE(frameNumber, 15);
    buffer.writeFloatLE(0, 19);
    buffer.writeFloatLE(0, 23);
    buffer.writeFloatLE(0, 27);
    buffer.writeFloatLE(rotationX, 31);
    buffer.writeFloatLE(0, 35);
    buffer.writeFloatLE(0, 39);
    buffer.writeFloatLE(1, 43);
    interpolation.copy(buffer, 47);
    return buffer;
  };
  const count = Buffer.alloc(4);
  count.writeUInt32LE(2, 0);
  const morphCount = Buffer.alloc(4);
  return Buffer.concat([signature, modelName, count, frame(0, 0), frame(180, 0.2), morphCount]);
}

function writeFixtures() {
  fs.rmSync(path.join(outputDir, "fixtures"), { recursive: true, force: true });
  fs.mkdirSync(path.join(fixtureRoot, "tex"), { recursive: true });
  fs.mkdirSync(path.join(fixtureRoot, "clothes"), { recursive: true });
  const pmx = buildPmx();
  const png = Buffer.from(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
    "base64",
  );
  fs.writeFileSync(path.join(fixtureRoot, "desk pet.pmx"), pmx);
  fs.writeFileSync(path.join(fixtureRoot, "Mikoto.physics-stable.pmx"), pmx);
  fs.writeFileSync(path.join(fixtureRoot, "tex", "肌 色.png"), png);
  fs.writeFileSync(path.join(fixtureRoot, "clothes", "Hime_&_Mikoto.png"), png);
  fs.writeFileSync(path.join(fixtureRoot, "wave.vmd"), buildVmd());
}

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
  const seen = [];
  const server = http.createServer(async (request, response) => {
    try {
      const host = String(request.headers.host || "").split(":")[0].toLowerCase();
      const url = new URL(request.url || "/", `http://${request.headers.host}`);
      let pathname = "";
      try {
        pathname = decodeURIComponent(url.pathname);
      } catch {
        response.writeHead(400, { "Content-Type": "text/plain" });
        response.end("bad encoding");
        return;
      }
      seen.push({ host, path: pathname });

      if (host === "ext0.local") {
        const fileName = pathname.split("/").filter(Boolean).at(-1) || "";
        const file = safeJoin(fixtureRoot, fileName);
        if (!file || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
          response.writeHead(404, { "Content-Type": "text/plain" });
          response.end("missing");
          return;
        }
        // Deliberately no Access-Control-Allow-Origin. This is the broken shape.
        response.writeHead(200, { "Content-Type": "application/octet-stream" });
        response.end(fs.readFileSync(file));
        return;
      }

      if (pathname.startsWith("/ext/0/")) {
        let relative = pathname.slice("/ext/0/".length);
        if (relative === "missing.pmx") {
          response.writeHead(404, { "Content-Type": "text/plain", "Cache-Control": "no-store" });
          response.end("missing");
          return;
        }
        if (relative === "corrupt.pmx") {
          response.writeHead(200, {
            "Content-Type": "application/octet-stream",
            "Access-Control-Allow-Origin": "*",
            "Cache-Control": "no-store",
          });
          response.end(Buffer.from("this is not a pmx"));
          return;
        }
        if (relative === "slow.pmx") {
          await new Promise((resolve) => setTimeout(resolve, 3500));
          relative = "desk pet.pmx";
        }
        const file = safeJoin(fixtureRoot, relative);
        if (!file || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
          response.writeHead(404, { "Content-Type": "text/plain", "Cache-Control": "no-store" });
          response.end("missing");
          return;
        }
        response.writeHead(200, {
          "Content-Type": contentType(file),
          "Access-Control-Allow-Origin": "*",
          "Cache-Control": "no-store",
        });
        response.end(fs.readFileSync(file));
        return;
      }

      if (pathname.startsWith("/web/")) {
        const file = safeJoin(path.join(appRoot, "web"), pathname.slice("/web/".length));
        if (!file || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
          response.writeHead(404, { "Content-Type": "text/plain" });
          response.end("missing web file");
          return;
        }
        response.writeHead(200, {
          "Content-Type": contentType(file),
          "Cache-Control": "no-store",
        });
        response.end(fs.readFileSync(file));
        return;
      }

      response.writeHead(404, { "Content-Type": "text/plain" });
      response.end("not found");
    } catch (error) {
      if (!response.headersSent) {
        response.writeHead(500, { "Content-Type": "text/plain" });
      }
      response.end(String(error));
    }
  });

  return new Promise((resolve) => {
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      resolve({ server, port: address.port, seen });
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
      return { found, messages, elapsedMs: Date.now() - start };
    }
    await page.waitForTimeout(100);
  }
  throw new Error(`${label} timed out after ${timeoutMs}ms. messages=${JSON.stringify(messages).slice(0, 2500)}`);
}

async function send(page, message) {
  await page.evaluate((payload) => window.__hostSend(payload), message);
}

async function main() {
  fs.mkdirSync(outputDir, { recursive: true });
  const bundlePath = path.join(appRoot, "web", "mmd.bundle.js");
  const bundle = fs.readFileSync(bundlePath, "utf8");
  if (!bundle.includes("withAssetTimeout") || !bundle.includes("model-loaded")) {
    throw new Error(`Bundle at ${bundlePath} does not contain the load timeout fix.`);
  }
  const xaml = fs.readFileSync(path.join(repoRoot, "MainWindow.xaml"), "utf8");
  record(
    "empty-prompt-markup",
    xaml.includes("拖入 PMX 开始"),
    "The empty state string lives in the WPF window, which this browser harness cannot paint.",
  );

  writeFixtures();
  const { server, port, seen } = await startServer();
  const browser = await chromium.launch({
    headless: true,
    args: [
      "--host-resolver-rules=MAP pet.local 127.0.0.1,MAP ext0.local 127.0.0.1",
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

  try {
    await page.goto(`http://pet.local:${port}/web/index.html`, { waitUntil: "domcontentloaded" });
    const ready = await waitForMessage(
      page,
      (message) => message.type === "runtime-ready",
      45000,
      "runtime-ready",
    );
    const startupMessages = ready.messages;
    const startupErrors = startupMessages.filter((message) => message.type === "runtime-error");
    record(
      "startup-no-model",
      startupErrors.length === 0 && !startupMessages.some((message) => message.type === "model-loaded"),
      `runtime-ready in ${ready.elapsedMs}ms, runtime-error count ${startupErrors.length}, no load was sent`,
    );
    await page.screenshot({ path: path.join(outputDir, "startup.png") });

    const crossId = "cross-origin-id";
    const crossStarted = Date.now();
    await send(page, {
      type: "load-character",
      index: 0,
      url: `http://ext0.local:${port}/Mikoto.physics-stable.pmx`,
      requestId: crossId,
    });
    const cross = await waitForMessage(
      page,
      (message) => message.type === "runtime-error" && message.requestId === crossId,
      8000,
      "cross-origin runtime-error",
    );
    const crossElapsed = Date.now() - crossStarted;
    const crossLoaded = cross.messages.some((message) => message.type === "model-loaded" && message.requestId === crossId);
    const crossMessage = String(cross.found.message || "");
    record(
      "cross-origin-blocked",
      !crossLoaded
        && crossElapsed < 8000
        && crossMessage.length > 0
        && /status:\s*0|unable to load|failed to fetch|networkerror|cors/i.test(crossMessage)
        && !/within 30 seconds/i.test(crossMessage),
      `${crossElapsed}ms requestId=${cross.found.requestId} message=${crossMessage.slice(0, 300)}`,
    );

    const missingId = "missing-id";
    const missingStarted = Date.now();
    await send(page, {
      type: "load-character",
      index: 0,
      url: `http://pet.local:${port}/ext/0/missing.pmx`,
      requestId: missingId,
    });
    const missing = await waitForMessage(
      page,
      (message) => message.type === "runtime-error" && message.requestId === missingId,
      8000,
      "missing-file runtime-error",
    );
    const missingElapsed = Date.now() - missingStarted;
    const missingMessage = String(missing.found.message || "");
    record(
      "missing-file-error",
      missingElapsed < 8000
        && missingMessage.length > 0
        && !/within 30 seconds/i.test(missingMessage)
        && !/within 20 seconds/i.test(missingMessage),
      `${missingElapsed}ms message=${missingMessage.slice(0, 300)}`,
    );

    const corruptId = "corrupt-id";
    const corruptStarted = Date.now();
    await send(page, {
      type: "load-character",
      index: 0,
      url: `http://pet.local:${port}/ext/0/corrupt.pmx`,
      requestId: corruptId,
    });
    const corrupt = await waitForMessage(
      page,
      (message) => message.type === "runtime-error" && message.requestId === corruptId,
      8000,
      "corrupt-file runtime-error",
    );
    const corruptElapsed = Date.now() - corruptStarted;
    const corruptMessage = String(corrupt.found.message || "");
    record(
      "corrupt-file-error",
      corruptElapsed < 8000 && corruptMessage.length > 0 && !/within 30 seconds/i.test(corruptMessage),
      `${corruptElapsed}ms message=${corruptMessage.slice(0, 300)}`,
    );

    const firstId = "slow-load";
    const secondId = "fast-load";
    await send(page, {
      type: "load-character",
      index: 0,
      url: `http://pet.local:${port}/ext/0/slow.pmx`,
      requestId: firstId,
    });
    await page.waitForTimeout(200);
    const fastStarted = Date.now();
    await send(page, {
      type: "load-character",
      index: 1,
      url: `http://pet.local:${port}/ext/0/${encodeURIComponent("desk pet.pmx")}`,
      requestId: secondId,
    });
    const loaded = await waitForMessage(
      page,
      (message) => message.type === "model-loaded" && message.requestId === secondId,
      8000,
      "second model-loaded",
    );
    await page.waitForTimeout(4000);
    const afterCancel = await posted(page);
    const firstLoaded = afterCancel.some((message) => message.type === "model-loaded" && message.requestId === firstId);
    const secondErrors = afterCancel.filter((message) => message.type === "runtime-error" && message.requestId === secondId);
    record(
      "supersede-while-loading",
      !firstLoaded && secondErrors.length === 0 && loaded.found.requestId === secondId,
      `second model replied in ${loaded.elapsedMs}ms; first model-loaded=${firstLoaded}; second errors=${secondErrors.length}`,
    );

    const model = loaded.found;
    const textureHits = seen.filter((entry) => entry.host === "pet.local" && entry.path.startsWith("/ext/0/"));
    const sawSkin = textureHits.some((entry) => entry.path.includes("肌 色.png"));
    const sawOutfit = textureHits.some((entry) => entry.path.includes("Hime_&_Mikoto.png"));
    const sawSpace = textureHits.some((entry) => entry.path.includes("desk pet.pmx"));
    record(
      "load-character",
      model.modelName === "Desk Pet"
        && model.requestId === secondId
        && sawSkin
        && sawOutfit
        && sawSpace,
      `model=${model.modelName} bones=${model.boneCount} morphs=${(model.morphs || []).length} skin=${sawSkin} outfit=${sawOutfit} spacedName=${sawSpace}`,
    );
    await page.screenshot({ path: path.join(outputDir, "load-character.png") });

    const motionId = "motion-id";
    const motionStarted = Date.now();
    await send(page, {
      type: "load-motion",
      name: "wave",
      url: `http://pet.local:${port}/ext/0/wave.vmd`,
      loop: false,
      requestId: motionId,
    });
    const motion = await waitForMessage(
      page,
      (message) => message.type === "motion-loaded" && message.requestId === motionId,
      8000,
      "motion-loaded",
    );
    await send(page, { type: "play" });
    let firstFrame = null;
    let lastFrame = null;
    const frameDeadline = Date.now() + 6000;
    while (Date.now() < frameDeadline) {
      const frame = await page.evaluate(() => {
        const runtime = window.MmdDeskPetRuntime && window.MmdDeskPetRuntime.runtime;
        const value = runtime && runtime.currentFrameTime;
        return typeof value === "number" && Number.isFinite(value) ? value : null;
      });
      if (typeof frame === "number") {
        firstFrame = firstFrame === null ? frame : firstFrame;
        lastFrame = frame;
        if (lastFrame - firstFrame >= 2 || lastFrame >= 3) {
          break;
        }
      }
      await page.waitForTimeout(100);
    }
    const advanced = typeof firstFrame === "number" && typeof lastFrame === "number" && (lastFrame - firstFrame >= 2 || lastFrame >= 3);
    record(
      "load-motion-playback",
      motion.found.requestId === motionId
        && Number(motion.found.boneTracks) > 0
        && advanced,
      `${Date.now() - motionStarted}ms boneTracks=${motion.found.boneTracks} endFrame=${motion.found.endFrame} frames ${firstFrame} -> ${lastFrame}`,
    );
    await page.screenshot({ path: path.join(outputDir, "playback.png") });
  } finally {
    const report = {
      appRoot,
      checks,
      consoleErrors: consoleNotes.slice(0, 40),
      requests: seen,
    };
    fs.writeFileSync(path.join(outputDir, "report.json"), JSON.stringify(report, null, 2));
    fs.writeFileSync(
      path.join(outputDir, "log.txt"),
      checks.map((check) => `${check.pass ? "PASS" : "FAIL"} ${check.name}: ${check.detail}`).join("\n")
        + "\n\nconsole:\n"
        + consoleNotes.slice(0, 40).join("\n")
        + "\n",
    );
    await browser.close();
    await new Promise((resolve) => server.close(resolve));
  }

  if (checks.some((check) => !check.pass)) {
    process.exitCode = 1;
  }
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
