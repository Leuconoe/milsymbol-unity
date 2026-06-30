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

const here = path.dirname(fileURLToPath(import.meta.url));

// ---------------------------------------------------------------------------
// Human-readable SIDC description, sourced from milsymbol itself (single source
// of truth). The letter-SIDC icon names live only as keys in milsymbol's source
// (sId["KEY"] = [icn["NAME"], ...]); we extract KEY -> [names] once so any SIDC
// decodes correctly without hand-maintained tables.
// ---------------------------------------------------------------------------
function titleCase(value) {
  return value
    .toLowerCase()
    .split(" ")
    .filter(Boolean)
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(" ");
}

function cleanIconName(name) {
  // Names look like "AR.I.UNMANNED AERIAL VEHICLE" or "GR.IC.FF.INFANTRY";
  // the meaningful label is the last dotted segment. Slashes would clash with
  // the field separator, so replace them with spaces.
  const tail = name.split(".").pop().replace(/\//g, " ").trim();
  return titleCase(tail);
}

let iconIndex = null;
function buildIconIndex() {
  const index = {};
  const dir = path.join(here, "..", "..", "milsymbol", "src", "lettersidc", "sidc");
  let files = [];
  try {
    files = fs.readdirSync(dir).filter((f) => f.endsWith(".js"));
  } catch {
    return index;
  }

  for (const file of files) {
    let src;
    try {
      src = fs.readFileSync(path.join(dir, file), "utf8");
    } catch {
      continue;
    }

    // Drop comments so stray ';' or 'icn[' inside them do not pollute parsing.
    src = src.replace(/\/\*[\s\S]*?\*\//g, "").replace(/\/\/[^\n]*/g, "");

    for (const statement of src.split(";")) {
      if (statement.indexOf("sId[") === -1) {
        continue;
      }

      const keys = [...statement.matchAll(/sId\["([^"]+)"\]/g)].map((m) => m[1]);
      const names = [...statement.matchAll(/icn\["([^"]+)"\]/g)].map((m) => cleanIconName(m[1]));
      if (keys.length === 0 || names.length === 0) {
        continue;
      }

      for (const key of keys) {
        index[key] = names;
      }
    }
  }

  return index;
}

const SCHEME = { S: "Warfighting", G: "Tactical Graphics", W: "METOC", I: "Signals Intelligence", O: "Stability Operations", E: "Emergency Management" };
const AFFIL = { P: "Pending", U: "Unknown", A: "Assumed Friend", F: "Friend", N: "Neutral", S: "Suspect", H: "Hostile", D: "Exercise Friend", J: "Joker", K: "Faker" };
const DIM = { P: "Space", A: "Air", G: "Ground", S: "Sea Surface", U: "Subsurface", F: "SOF", X: "Other", Z: "Unknown" };
const STATUS = { A: "Planned", P: "Present", C: "Fully Capable", D: "Damaged", X: "Destroyed", F: "Full To Capacity" };
const MOD1 = { A: "Headquarters", E: "Task Force", F: "Feint Dummy", H: "Installation", M: "Mobility", N: "Towed Array" };
const MOD2 = { A: "Team Crew", B: "Squad", C: "Section", D: "Platoon", E: "Company", F: "Battalion", G: "Regiment", H: "Brigade", I: "Division", J: "Corps", K: "Army" };
const OOB = { A: "Air", E: "Electronic", C: "Civilian", G: "Ground", N: "Maritime", S: "Strategic Force" };

function decodeChar(map, ch) {
  if (!ch || ch === "-") {
    return "-";
  }
  return map[ch] || ch;
}

function describeSidc(rawSidc) {
  const s = (typeof rawSidc === "string" ? rawSidc : "").toUpperCase();
  if (s.length < 10) {
    return "";
  }

  if (iconIndex === null) {
    iconIndex = buildIconIndex();
  }

  const generic = s[0] + "-" + s[2] + "-" + s.substring(4, 10);
  let symbol = "-";
  if (iconIndex[generic] && iconIndex[generic].length > 0) {
    symbol = iconIndex[generic].join("_");
  } else if (s.substring(4, 10).replace(/-+$/, "").length > 0) {
    symbol = s.substring(4, 10); // unknown function id: raw
  }

  return [
    decodeChar(SCHEME, s[0]),
    decodeChar(AFFIL, s[1]),
    decodeChar(DIM, s[2]),
    decodeChar(STATUS, s[3]),
    symbol,
    decodeChar(MOD1, s[10]),
    decodeChar(MOD2, s[11]),
    s.substring(12, 14) || "--",
    decodeChar(OOB, s[14])
  ].join("/");
}

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
    anchorY: anchor.y,
    description: describeSidc(sidc)
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
