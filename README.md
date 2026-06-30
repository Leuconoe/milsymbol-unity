# Milsymbol Unity

Unity package tooling for generating MIL-STD-2525/APP-6 military symbol icons from the JavaScript `milsymbol` source at editor time.

This package does not generate symbols at build runtime. Unity Editor calls the bundled `milsymbol` submodule through Node.js, exports icon-only PNG files, and stores optional `MilsymbolIconAsset` metadata for runtime use.

![Milsymbol Unity icon generator](Documentation~/screenshot.png)

## Features

- Unity Package layout with runtime and editor assemblies.
- SIDC Builder dropdown workflow plus direct SIDC input.
- Letter SIDC and numeric SIDC generation through bundled `milsymbol`.
- Icon-only output: `infoFields=false` removes text fields and information-field arrows.
- PNG export through `milsymbol`'s Node-side `Symbol.asPNG()` API.
- Editor window at `Tools/Milsymbol/Icon Generator`.
- Auto Preview toggle with debounced generation.
- Unity color fields for fill, frame, icon, outline, and mono-color overrides.
- Automatic SIDC-based PNG file names, such as `SFAPMFQ----B---.png`.
- Optional `MilsymbolIconAsset` creation.
- `MilsymbolIconAsset` context menu regeneration that overwrites the existing PNG when possible.
- Sprite Atlas creation with the output folder registered as the packable.
- Unity menu-driven Node dependency installation.

## Requirements

- Unity 2021.3 or newer.
- Node.js installed. It does **not** need to be on the Editor's `PATH`: the editor tooling
  probes common install locations (Program Files, Homebrew, nvm, fnm, volta, asdf) and, on
  macOS/Linux, falls back to your login shell. If auto-detection fails, set the full path to
  the `node` binary in `Tools/Milsymbol/Icon Generator` > `Node Executable`.
- The `milsymbol` submodule checked out under this package.

## Installation

### Recommended: clone into your project's `Packages` folder

Unity Package Manager git-URL installs do **not** fetch git submodules, so the bundled
`milsymbol` source would be missing. Clone the package with submodules instead:

```bash
cd <your-unity-project>/Packages
git clone --recurse-submodules https://github.com/Leuconoe/milsymbol-unity.git
```

If you already cloned without `--recurse-submodules`, initialize the submodule:

```bash
git submodule update --init --recursive
```

From inside Unity you can also run:

```text
Tools/Milsymbol/Update milsymbol Submodule
```

This is invoked automatically the first time you install Node dependencies or generate an
icon, and reports an actionable message when the package is not a git working copy (the
case for UPM git-URL installs).

### Install Node dependencies

```text
Tools/Milsymbol/Install Node Dependencies
```

The installer runs `npm install --omit=dev` inside the bundled `milsymbol` submodule so only
production dependencies needed for PNG export are installed. npm is invoked through the
resolved `node` binary (`node npm-cli.js`) so it works even when `npm`/`npm.cmd` is not on the
Editor's `PATH`.

## Usage

1. Install the package through Package Manager.
2. Open `Tools/Milsymbol/Icon Generator`.
3. Install Node dependencies if the window shows `Missing`.
4. Build a SIDC with dropdowns, or disable `Use SIDC Builder` and enter one directly.
5. Adjust style, color, PNG size, and `Auto Preview` as needed.
6. Click `Generate` and `Save PNG`.

Generated PNG files are imported as Unity Sprites. If `Create .asset` is enabled, a `MilsymbolIconAsset` is saved next to the PNG with the SIDC, style, generated SVG source, dimensions, anchor, validity flag, and texture reference.

To regenerate an existing icon asset, right-click a `MilsymbolIconAsset` and choose:

```text
Milsymbol/Regenerate Icon
```

## Security Notes

- PNG paths written by normal package saves are constrained to the Unity project's `Assets` folder.
- Local file export uses Unity's save dialog and is intentionally allowed to write outside `Assets`.
- Node is launched without shell execution.
- JSON request files are written as UTF-8 without BOM and read with BOM stripping.
- Production dependency audit was checked with `npm audit --omit=dev`; no vulnerabilities were reported.
- Full dev dependency audit reports existing upstream development-tool vulnerabilities in `milsymbol`'s devDependencies. The Unity installer avoids installing those by using `--omit=dev`.

## Porting Boundary

The current port keeps JavaScript as the source of truth. Runtime C# code consumes generated PNG and ScriptableObject assets only. If runtime generation becomes necessary, the next step is either a C# draw-instruction pipeline rewrite or embedding a JavaScript runtime.

## Acknowledgements

This package is only a thin Unity editor wrapper. All military symbol rendering is done by
**[milsymbol](https://github.com/spatialillusions/milsymbol)** by Måns Beckman
([spatialillusions](https://github.com/spatialillusions)), released under the MIT License.
All credit and respect for the symbology engine and its MIL-STD-2525 / APP-6 icon data goes
to the original author and the milsymbol contributors. This project would not exist without
their work — please support and star the upstream project.

The bundled `milsymbol` submodule retains its original MIT license; see
`milsymbol/LICENSE`.

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
