# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1]

### Fixed

- Resolve `node`/`npm`/`git` from common install locations (Program Files, Homebrew,
  nvm, fnm, volta, asdf) instead of relying on the Editor inheriting the login-shell
  `PATH`. Fixes "npm not found" when Unity is launched from Unity Hub.
- Invoke npm through the resolved node binary (`node npm-cli.js`) to avoid the Windows
  `npm.cmd` shim and its `PATH` dependency.

### Added

- `Tools/Milsymbol/Update milsymbol Submodule` menu item and automatic
  `git submodule update --init --recursive` before dependency install and icon
  generation, with an actionable message when the package is not a git working copy
  (the case for UPM git-URL installs, which cannot fetch submodules).
- Submodule status row and `Update` button in the Icon Generator window.

### Changed

- README documents cloning into `Packages` with `--recurse-submodules` as the supported
  install path, and that Node no longer needs to be on the Editor `PATH`.

## [0.1.0]

### Added

- Initial release.
