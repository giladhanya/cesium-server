TileServer GL local assets

sprites/
  sprite.json
  sprite.png
  sprite@2x.json
  sprite@2x.png

The sprite atlas is intentionally empty but valid. The current 16 overlay
styles do not use icon-image, so this is sufficient. Replace it later with
a real OpenMapTiles/OSM Bright sprite atlas when POI icons are added.

fonts/
  Populate this directory by running:

    ./install-fonts.sh ./fonts

The script downloads pre-generated OpenMapTiles glyph PBFs. After it
finishes, TileServer GL can serve glyphs locally/offline.

Recommended config.json paths:
  "fonts": "config/fonts",
  "sprites": "config/sprites"

For styles that render text, add:
  "glyphs": "{fontstack}/{range}.pbf"

For styles that use icons, add:
  "sprite": "{styleJsonFolder}/../sprites/sprite"

Note: the exact sprite path should be tested with your TileServer GL config
layout. For a shared sprite atlas, an absolute container-local path or an
HTTP endpoint exposed by TileServer GL may be preferable.
