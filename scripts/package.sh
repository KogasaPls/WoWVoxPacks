#!/usr/bin/env bash

## Ensure the RELEASE_TAG variable is set
if [ -z "$RELEASE_TAG" ]; then
  echo "RELEASE_TAG is not set. Exiting..."
  exit 1
fi

mkdir -p dist

for dir in $(find output/ -mindepth 1 -maxdepth 1 -type d); do
  echo "Packaging $dir"
  basename=$(basename $dir)
  archive_name="WoWVoxPacks_${basename}_${RELEASE_TAG}.zip"
  zip -9 -r "dist/$archive_name" "$dir" -x "**.wav"
done


