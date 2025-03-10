#!/usr/bin/env bash

## Ensure the RELEASE_TAG variable is set
if [ -z "$RELEASE_TAG" ]; then
  echo "RELEASE_TAG is not set. Exiting..."
  exit 1
fi

mkdir -p dist

cd output || exit 1

while IFS= read -r -d '' file; do
  voice_name=$(basename "$(dirname "$file")")
  addon_name=$(basename "$file")

  # Trim _${voice_name} out of addon_name if it exists
  addon_name=${addon_name%"_$voice_name"}

  # Trim _WoWVoxPacks out of addon_name if it exists
  addon_name=${addon_name%"_WoWVoxPacks"}

  echo "Processing directory: $file"
  echo "Creating archive for voice: $voice_name, addon: $addon_name"

  cd "$file" || exit 1

  archive_name="WoWVoxPacks_${voice_name}_${addon_name}_${RELEASE_TAG}.zip"
  zip -r -q -9 "../../../dist/$archive_name" . -x "*.wav"

  if [ $? -ne 0 ]; then
    echo "Failed to create archive for $file"
    exit 1
  fi

  echo "Created archive: dist/$archive_name"

  cd ../.. || exit 1
done < <(find . -mindepth 2 -maxdepth 2 -type d -print0)