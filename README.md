# Milsymbol Unity

Unity editor tooling for generating **MIL-STD-2525 / APP-6** military symbol icons as PNG
sprites, powered by the JavaScript [`milsymbol`](https://github.com/spatialillusions/milsymbol)
library. Symbols are generated at **edit time** — runtime code only consumes the produced
PNG sprites and optional `MilsymbolIconAsset` metadata.

![Milsymbol Unity icon generator](Documentation~/screenshot.png)

## How it works

1. The Unity editor calls **Node.js**, which runs upstream `milsymbol` to produce an SVG for a
   given SIDC.
2. The SVG is rasterized to PNG by **[`@resvg/resvg-js`](https://www.npmjs.com/package/@resvg/resvg-js)**
   (Node side) — no Unity vector-graphics package required.
3. The PNG is imported as a Unity Sprite; an optional `MilsymbolIconAsset` stores the SIDC, a
   human-readable decoded description, style, size, anchor, and texture reference.

`milsymbol` and `@resvg/resvg-js` are **downloaded from npm at setup** — there is no git
submodule to manage.

## Requirements

- Unity **2021.3** or newer.
- **Node.js** installed. It does *not* need to be on the Editor's `PATH`: the tooling probes
  common install locations (Program Files, Homebrew, nvm, fnm, volta, asdf) and, on
  macOS/Linux, falls back to your login shell. If auto-detection fails, set the full path to
  the `node` binary in `Tools/Milsymbol/Icon Generator` › `Node Executable`.

## Installation

Install via Unity Package Manager (Git URL) or clone into your project's `Packages` folder:

```text
https://github.com/Leuconoe/milsymbol-unity.git
```

Then download the Node dependencies (one time):

```text
Tools/Milsymbol/Install Node Dependencies
```

This runs `npm install` in `Editor/Node~`, fetching the latest published `milsymbol` and
`@resvg/resvg-js` into `Editor/Node~/node_modules` (git-ignored, and ignored by Unity because
the folder name ends with `~`). It also runs automatically the first time you generate an
icon. npm is invoked through the resolved `node` binary, so it works even when `npm`/`npm.cmd`
is not on the Editor's `PATH`.

## Usage

Open `Tools/Milsymbol/Icon Generator`.

### Build a symbol

- **SIDC field** — always directly editable. Type a 15-character letter SIDC, or use the
  builder below; the two stay in sync.
- **SIDC Builder** — Coding Scheme / Affiliation / Battle Dimension / Status, a **Specific
  Symbol** dropdown for the chosen domain, a **Variant** dropdown for sub-types (e.g. UAV
  roles: Reconnaissance / Attack / Bomber), modifiers, country code, and order of battle.
  For symbols not in the curated list, type the SIDC directly.
- **Style** — size, frame, fill, square canvas, colors (fill / frame / icon / outline / mono),
  opacity, stroke and outline width.
- Hover any field's `(?)` label for help.

### Output

- **Output Folder** — project-relative folder under `Assets` (persists across restarts).
- **Texture Size** — imported sprite `maxTextureSize` (default 128).
- **Generate** then **Save PNG**. PNG files are imported as Sprites and registered into a
  `Milsymbol Icons.spriteatlas`. With `Create .asset` on, a `MilsymbolIconAsset` is written
  next to the PNG.

### Batch generation

In the **Batch** section, enter multiple SIDCs separated by commas, semicolons, or new lines
and click **Generate To Folder** to write them all into the Output Folder at once.

### Regenerate

Right-click a `MilsymbolIconAsset` and choose `Milsymbol/Regenerate Icon` (the existing PNG is
overwritten and the previous texture size is preserved).

## MilsymbolIconAsset

Stores the SIDC, a **decoded description** sourced from milsymbol itself (so any SIDC decodes
accurately, e.g. `Warfighting/Friend/Air/Present/Unmanned Aerial Vehicle_Reconnaissance/-/-/--/-`),
the style, dimensions, anchor, validity flag, and the texture reference.

## Security Notes

- PNG paths written by normal package saves are constrained to the project's `Assets` folder.
- Local file export uses Unity's save dialog and is intentionally allowed to write outside `Assets`.
- Node is launched without shell execution. JSON request files are UTF-8 without BOM.
- Setup installs only the two production dependencies (`milsymbol`, `@resvg/resvg-js`) into
  `Editor/Node~/node_modules`, which is git-ignored.

## Acknowledgements

This package is a thin Unity editor wrapper. All military symbol rendering is done by
**[milsymbol](https://github.com/spatialillusions/milsymbol)** by Måns Beckman
([spatialillusions](https://github.com/spatialillusions)), released under the MIT License. All
credit and respect for the symbology engine and its MIL-STD-2525 / APP-6 icon data goes to the
original author and the milsymbol contributors — please support and star the upstream project.
SVG rasterization uses [`@resvg/resvg-js`](https://github.com/yisibl/resvg-js) (MIT). Both are
downloaded from npm at setup and retain their own licenses.

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
