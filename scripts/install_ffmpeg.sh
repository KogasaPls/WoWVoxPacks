#!/usr/bin/env bash
# BtbN's builds are hosted on GitHub. FedericoCarboni/setup-ffmpeg downloads from
# johnvansickle.com, which fails from runners with "fetch failed" (its issue #35).
set -euo pipefail

asset=ffmpeg-n9.0-latest-linux64-gpl-9.0
release=https://github.com/BtbN/FFmpeg-Builds/releases/download/latest

cd "$RUNNER_TEMP"
curl -sfL --retry 3 -o checksums.sha256 "$release/checksums.sha256"
curl -sfL --retry 3 -o "$asset.tar.xz" "$release/$asset.tar.xz"
grep "  $asset.tar.xz\$" checksums.sha256 | sha256sum -c -
tar -xJf "$asset.tar.xz"

"$RUNNER_TEMP/$asset/bin/ffmpeg" -version | sed -n 1p
echo "$RUNNER_TEMP/$asset/bin" >> "$GITHUB_PATH"
