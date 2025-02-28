#!/usr/bin/env bash

## Ensure the RELEASE_TAG variable is set
if [ -z "$RELEASE_TAG" ]; then
  echo "RELEASE_TAG is not set. Exiting..."
  exit 1
fi

mkdir -p dist

cd output || exit 1

while IFS= read -r -d '' file; do
  echo "Packaging $file"
  cd "$file" || exit 1
  basename=$(basename "$file")
  archive_name="WoWVoxPacks_${basename}_${RELEASE_TAG}.zip"
  zip -r -q -9 "../../dist/$archive_name" . -x "*.wav"
  cd .. || exit 1
done < <(find . -mindepth 1 -maxdepth 1 -type d -print0)