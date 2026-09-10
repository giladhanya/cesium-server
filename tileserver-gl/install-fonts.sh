#!/usr/bin/env bash
set -euo pipefail

DEST="${1:-$(pwd)/fonts}"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

echo "Installing OpenMapTiles glyph PBFs into: $DEST"
mkdir -p "$DEST"

# The OpenMapTiles fonts repository publishes generated PBF glyphs on gh-pages.
git clone --depth 1 --branch gh-pages \
  https://github.com/openmaptiles/fonts.git \
  "$TMP/openmaptiles-fonts"

# Copy only generated glyph directories/files.  Ignore source TTF/OTF if present.
find "$TMP/openmaptiles-fonts" -type f -name '*.pbf' -print0 | while IFS= read -r -d '' f; do
  rel="${f#"$TMP/openmaptiles-fonts/"}"
  mkdir -p "$DEST/$(dirname "$rel")"
  cp "$f" "$DEST/$rel"
done

count="$(find "$DEST" -type f -name '*.pbf' | wc -l)"
if [ "$count" -eq 0 ]; then
  echo "ERROR: no .pbf glyph files were found on the gh-pages branch."
  echo "Inspect: https://github.com/openmaptiles/fonts"
  exit 1
fi

echo "Installed $count glyph PBF files."
echo
echo "Top-level font stacks:"
find "$DEST" -mindepth 1 -maxdepth 1 -type d -printf '  %f\n' | sort
