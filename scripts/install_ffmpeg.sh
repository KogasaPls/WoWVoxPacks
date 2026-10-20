#!/usr/bin/env bash
# BtbN's builds are hosted on GitHub. FedericoCarboni/setup-ffmpeg downloads from
# johnvansickle.com, which fails from runners with "fetch failed" (its issue #35).
# BtbN keeps one build per month for two years, so the pin below lasts until about
# 2028-09; move it to a newer month-end autobuild release and its sha256 before then.
set -euo pipefail

release=https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-09-30-13-08
asset=ffmpeg-n9.0.2-17-g2a571b6068-linux64-gpl-9.0
sha256=68ee646831adaae2495618346f3bba94ff207ff83bbd34d643e7004730d66269

cd "$RUNNER_TEMP"
curl -sSfL --retry 5 --retry-delay 10 -o "$asset.tar.xz" "$release/$asset.tar.xz"
echo "$sha256  $asset.tar.xz" | sha256sum -c -
tar -xJf "$asset.tar.xz"

"$RUNNER_TEMP/$asset/bin/ffmpeg" -version | sed -n 1p
echo "$RUNNER_TEMP/$asset/bin" >> "$GITHUB_PATH"
