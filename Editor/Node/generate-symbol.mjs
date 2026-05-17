import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  ms,
  app6b,
  std2525b,
  std2525c,
  app6d,
  std2525d,
  std2525e,
  path2d
} from "../../milsymbol/index.mjs";

ms.addIcons(app6b);
ms.addIcons(std2525b);
ms.addIcons(std2525c);
ms.addIcons(app6d);
ms.addIcons(std2525d);
ms.addIcons(std2525e);
ms.Path2D = path2d;

const [, , requestPath, responsePath] = process.argv;

if (!requestPath || !responsePath) {
  throw new Error("Usage: node generate-symbol.mjs <request.json> <response.json>");
}

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf8").replace(/^\uFEFF/, ""));
}

function writeJson(filePath, value) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, JSON.stringify(value, null, 2), "utf8");
}

function cleanString(value) {
  return typeof value === "string" ? value.trim() : "";
}

function colorByAffiliation(color) {
  if (!color) return "";
  return {
    Civilian: color,
    Friend: color,
    Hostile: color,
    Neutral: color,
    Unknown: color,
    Suspect: color
  };
}

function applyStyle(options, style) {
  if (!style) return;

  options.size = Number.isFinite(style.size) ? style.size : 100;
  options.frame = style.frame !== false;
  options.fill = style.fill !== false;
  options.square = style.square === true;
  options.alternateMedal = style.alternateMedal === true;
  options.civilianColor = style.civilianColor !== false;
  options.fillOpacity = Number.isFinite(style.fillOpacity) ? Math.min(Math.max(style.fillOpacity, 0), 1) : 1;
  options.strokeWidth = Number.isFinite(style.strokeWidth) ? style.strokeWidth : 4;
  options.outlineWidth = Number.isFinite(style.outlineWidth) ? style.outlineWidth : 0;

  const colorMode = cleanString(style.colorMode);
  if (colorMode) options.colorMode = colorMode;

  const monoColor = cleanString(style.monoColor);
  if (monoColor) options.monoColor = monoColor;

  const fillColor = cleanString(style.fillColor);
  if (fillColor) options.fillColor = fillColor;

  const frameColor = cleanString(style.frameColor);
  if (frameColor) options.frameColor = colorByAffiliation(frameColor);

  const iconColor = cleanString(style.iconColor);
  if (iconColor) options.iconColor = colorByAffiliation(iconColor);

  const outlineColor = cleanString(style.outlineColor);
  if (outlineColor) options.outlineColor = colorByAffiliation(outlineColor);
}

function standardName(standard) {
  if (standard === 1 || standard === "MilStd2525") return "2525";
  if (standard === 2 || standard === "App6") return "APP6";
  return "";
}

async function generate(request) {
  const sidc = cleanString(request.sidc);
  if (!sidc) {
    throw new Error("SIDC is required.");
  }

  const options = {
    sidc,
    icon: true,
    infoFields: request.iconOnly !== false ? false : true
  };

  applyStyle(options, request.style);

  const standard = standardName(request.standard);
  if (standard) {
    options.standard = standard;
  }

  const symbol = new ms.Symbol(options);
  const svg = symbol.asSVG();
  const anchor = symbol.getAnchor ? symbol.getAnchor() : { x: 0, y: 0 };
  const pngOutputPath = cleanString(request.pngOutputPath);
  if (pngOutputPath) {
    fs.mkdirSync(path.dirname(pngOutputPath), { recursive: true });
    const png = await symbol.asPNG({
      width: Number.isFinite(request.pngWidth) ? request.pngWidth : symbol.width,
      height: Number.isFinite(request.pngHeight) ? request.pngHeight : symbol.height
    });
    fs.writeFileSync(pngOutputPath, png);
  }

  return {
    ok: true,
    svg,
    pngPath: pngOutputPath,
    valid: symbol.isValid(),
    width: symbol.width,
    height: symbol.height,
    anchorX: anchor.x,
    anchorY: anchor.y
  };
}

try {
  const request = readJson(requestPath);
  writeJson(responsePath, await generate(request));
} catch (error) {
  writeJson(responsePath, {
    ok: false,
    error: error && error.stack ? error.stack : String(error)
  });
}
