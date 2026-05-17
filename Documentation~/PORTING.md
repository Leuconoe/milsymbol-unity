# Milsymbol Porting Notes

## Source pipeline

The bundled `milsymbol` source builds a symbol through this path:

1. `index.mjs` exports the `ms` object and SIDC modules.
2. `index.js` registers APP-6 and MIL-STD-2525 icon sets with `ms.addIcons(...)`.
3. `src/ms/symbol.js` creates a `ms.Symbol`.
4. `src/ms/symbol/setoptions.js` resolves metadata, colors, draw instructions, bounds, size, and anchor.
5. Symbol parts in `src/symbolfunctions` add the frame, icon, status modifiers, graphical modifiers, text fields, and direction arrow.
6. `src/ms/symbol/assvg.js` serializes draw instructions into SVG.

## Unity package approach

The first Unity port keeps milsymbol's source of truth in JavaScript and calls it at editor time through `Editor/Node/generate-symbol.mjs`.

This avoids manually rewriting the large APP-6 and MIL-STD-2525 geometry tables in C# while still giving Unity stable runtime artifacts:

- `.png` file saved into `Assets`.
- `Milsymbol Icons.spriteatlas` in the output folder, with the output folder registered as the packable.
- Optional `MilsymbolIconAsset` containing SIDC, style, PNG texture reference, SVG source, dimensions, anchor, and validity.
- Optional local `.png` export through `milsymbol`'s Node-side `Symbol.asPNG()` API.

## Icon-only behavior

Text is excluded by forcing:

```js
infoFields: false
```

That disables the text field path in `src/symbolfunctions/textfields.js` and the information-field guarded direction arrow path in `src/symbolfunctions/directionarrow.js`.

The generator still keeps:

- frame and fill geometry from `basegeometry.js`
- core military icon geometry from `icon.js`
- graphical modifiers from `modifier.js`, `statusmodifier.js`, `stack-extension.js`, `engagmentbar.js`, and `affliationdimension.js`

This matches the current requirement: military symbol icons only, no displayed text labels.

## Next porting step

If runtime generation becomes necessary, the same boundary can be moved from the Node bridge to either:

- a native C# rewrite of the draw-instruction pipeline, or
- an embedded JavaScript runtime.

For the current package, generation is intentionally editor-time so builds do not depend on Node.js.

PNG export is also editor tooling. The forked `milsymbol` submodule adds `Symbol.asPNG({ width, height })`, implemented with `@resvg/resvg-js`. Unity passes the same SIDC/style request to `Editor/Node/generate-symbol.mjs`, which calls `new ms.Symbol(options).asPNG(...)` and writes the PNG file for Unity to import.
