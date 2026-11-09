#!/usr/bin/env bash
set -euo pipefail

## Ensure the RELEASE_TAG variable is set
if [ -z "$RELEASE_TAG" ]; then
  echo "RELEASE_TAG is not set. Exiting..."
  exit 1
fi

mkdir -p dist

# zip MERGES into an existing archive rather than replacing it, so a second run
# for the same tag would re-inject files a later exclude was meant to drop. Stale
# archives from an older tag also match create-release.yml's ./dist/*.zip glob.
rm -f dist/*.zip

# SoundFiles.json is build metadata for the regular addons, so it does not ship in those
# archives. Northern Sky Raid Tools loads its manifest at runtime to keep each voice pack
# self-contained, so that addon is the intentional exception.
base_excludes=(-x "*.wav" -x "*/.wvp-*")

# Zip $2.. into $1, additionally excluding any of the given directories'
# Sounds/ folder if it's empty. Sounds/ folders that actually hold media are
# left alone.
zip_addon_dirs() {
  local archive="$1"
  shift
  local excludes=("${base_excludes[@]}")
  local d
  for d in "$@"; do
    if [[ "$(basename "$d")" != WoWVoxPacks_NorthernSkyRaidTools_* ]]; then
      excludes+=(-x "$d/SoundFiles.json")
    fi
    if [ -d "$d/Sounds" ] && [ -z "$(find "$d/Sounds" -mindepth 1 -print -quit)" ]; then
      excludes+=(-x "$d/Sounds" -x "$d/Sounds/*")
    fi
  done
  zip -r -q -9 "$archive" "$@" "${excludes[@]}"
}

# Package every voice/addon directory under $1; $2 is inserted before the release tag in
# archive names.
package_tree() {
  local root="$1" suffix="$2" file voice_name addon_dir addon_name archive_name
  cd "$root" || exit 1

  while IFS= read -r -d '' file; do
    voice_name=$(basename "$(dirname "$file")")
    addon_dir=$(basename "$file")

    echo "Processing directory: $file"

    cd "$voice_name" || exit 1

    # Trim _${voice_name} out of addon_name if it exists
    addon_name=${addon_dir%"_$voice_name"}

    # Trim _WoWVoxPacks / WoWVoxPacks_ out of addon_name if it exists
    addon_name=${addon_name%"_WoWVoxPacks"}
    addon_name=${addon_name#"WoWVoxPacks_"}

    echo "Creating archive for voice: $voice_name, addon: $addon_name"

    archive_name="WoWVoxPacks_${voice_name}_${addon_name}${suffix}_${RELEASE_TAG}.zip"

    zip_addon_dirs "$repo_root/dist/$archive_name" "$addon_dir" || {
      echo "Failed to create archive for $file"
      exit 1
    }

    echo "Created archive: dist/$archive_name"

    cd .. || exit 1
  done < <(find . -mindepth 2 -maxdepth 2 -type d -print0)
  cd "$repo_root" || exit 1
}

repo_root=$(pwd)
package_tree output ""
if [ -d output-forever ]; then
  package_tree output-forever "_Forever"
fi
