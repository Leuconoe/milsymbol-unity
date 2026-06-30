# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0]

### Added

- Batch generation: enter comma/semicolon/newline-separated SIDCs and generate them all to
  the Output Folder in one click (with tooltip and word-wrapped input).
- `Texture Size` setting for the imported sprite's `maxTextureSize` (default 128).
- `Variant` dropdown for symbols that support a function-id sub-type (UAV roles —
  Reconnaissance / Attack / Bomber etc., and fixed/rotary wing roles); disabled for symbols
  without variants.
- Tooltips on builder fields, with a `(?)` marker on labels that have hover help.
- Acknowledgements section crediting the upstream
  [milsymbol](https://github.com/spatialillusions/milsymbol) project (MIT, by Måns Beckman).

### Changed

- SIDC field is always directly editable, even while the SIDC Builder is enabled; typing
  syncs the builder dropdowns and vice-versa.
- Saving (PNG / SVG / `.asset`) overwrites a same-named file instead of creating a numbered
  copy; Output Folder persists across editor restarts.
- `MilsymbolIconAsset` stores a decoded SIDC description (sourced from milsymbol so any SIDC
  decodes accurately, e.g. `Warfighting/Friend/Air/Present/Unmanned Aerial Vehicle_Reconnaissance/-/-/--/-`)
  and no longer stores the generated SVG or texture asset path.

## [0.1.1]

### Fixed

- Resolve `node`/`npm`/`git` from common install locations (Program Files, Homebrew,
  nvm, fnm, volta, asdf) instead of relying on the Editor inheriting the login-shell
  `PATH`. Fixes "npm not found" when Unity is launched from Unity Hub.
- Invoke npm through the resolved node binary (`node npm-cli.js`) to avoid the Windows
  `npm.cmd` shim and its `PATH` dependency.
- Output Folder picker now updates the field immediately instead of only after the next
  editor refresh (drops keyboard focus and repaints).
- The Output Folder now persists across full editor restarts (stored in `EditorPrefs`)
  instead of resetting to the default.

### Added

- `Tools/Milsymbol/Update milsymbol Submodule` menu item and automatic
  `git submodule update --init --recursive` before dependency install and icon
  generation, with an actionable message when the package is not a git working copy
  (the case for UPM git-URL installs, which cannot fetch submodules).
- Submodule status row and `Update` button in the Icon Generator window.
- `Texture Size` setting in the Icon Generator that sets the imported sprite's
  `maxTextureSize` (default 128). Regeneration preserves the existing size.
- The SIDC field is now always directly editable, even while the SIDC Builder is
  enabled. Typing a SIDC syncs the builder dropdowns, and the builder only rewrites
  the SIDC when a dropdown actually changes.

### Changed

- `MilsymbolIconAsset` now stores a decoded SIDC description string covering all builder
  fields, slash-separated, with the function id split into Symbol Part / Domain / Specific
  Symbol and empty positions shown as `-` (e.g.
  `Warfighting/Friend/Air/Present/Unit/Air/Unmanned Aerial Vehicle/-/Squad/--/-`), and no
  longer stores the generated SVG or the texture asset path.
  **Breaking:** the `Svg` and `TextureAssetPath` properties were removed; `SetGeneratedData`
  takes a decoded-SIDC string in place of the SVG and drops the texture-path argument.
- Saving an icon (PNG, SVG, and `.asset`) now overwrites an existing file with the same
  name instead of creating a numbered copy.
- The Generator window's Node setup section is now a foldout: expanded when setup is
  incomplete and collapsed once the submodule and Node dependencies are ready.
- README documents cloning into `Packages` with `--recurse-submodules` as the supported
  install path, and that Node no longer needs to be on the Editor `PATH`.

## [0.1.0]

### Added

- Initial release.
