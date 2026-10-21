#!/usr/bin/env bash
# BtbN's builds are hosted on GitHub. FedericoCarboni/setup-ffmpeg downloads from
# johnvansickle.com, which fails from runners with "fetch failed" (its issue #35).
# Follow the newest release branch within this major, with the digest from the same lookup.
set -euo pipefail

major=9
selected=$(gh api repos/BtbN/FFmpeg-Builds/releases/tags/latest | jq -er --arg major "$major" '
  [.assets[] | select(.name | test("^ffmpeg-n" + $major + "\\.[0-9]+-latest-linux64-gpl-" + $major + "\\.[0-9]+\\.tar\\.xz$"))]
  | sort_by(.name | capture("^ffmpeg-n[0-9]+\\.(?<minor>[0-9]+)-").minor | tonumber)
  | last
  | select((.digest // "") | test("^sha256:[0-9a-f]{64}$"))
  | [.name, (.digest | ltrimstr("sha256:")), .browser_download_url] | @tsv')
IFS=$'\t' read -r archive sha256 url <<< "$selected"
asset=${archive%.tar.xz}

cd "$RUNNER_TEMP"
curl -sSfL --retry 5 --retry-delay 10 --retry-all-errors -o "$archive" "$url"
echo "$sha256  $archive" | sha256sum -c -
tar -xJf "$archive"

"$RUNNER_TEMP/$asset/bin/ffmpeg" -version | sed -n 1p
echo "$RUNNER_TEMP/$asset/bin" >> "$GITHUB_PATH"
