#!/usr/bin/env bash
#
# Runs the addon Lua users actually get: regenerates it from the C#, executes it against the
# real LibSharedMedia stack and a verbatim NSRT excerpt, then lints it.
#
#   tests/lua/run.sh
#
# Needs the .NET SDK, luajit (or lua5.1) and luacheck on PATH. Set WVP_LUA_OUT to keep the
# generated Lua somewhere you can read it afterwards.

set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"

if [[ -n "${WVP_LUA_OUT:-}" ]]; then
    generated="$WVP_LUA_OUT"
else
    # Outside the repo on purpose: generated Lua is never committed and never gitignored.
    generated="$(mktemp -d -t wowvoxpack-lua-XXXXXX)"
    trap 'rm -rf "$generated"' EXIT
fi

lua_bin="${LUA:-}"
if [[ -z "$lua_bin" ]]; then
    for candidate in luajit lua5.1; do
        if command -v "$candidate" >/dev/null 2>&1; then
            lua_bin="$candidate"
            break
        fi
    done
fi

if [[ -z "$lua_bin" ]]; then
    # CallbackHandler-1.0 needs loadstring, so 5.2+ is not a substitute.
    echo "no luajit or lua5.1 on PATH" >&2
    exit 1
fi

echo "==> generating addon Lua into $generated"
dotnet run --project "$here/generator" -c Release -- "$root" "$generated"

echo "==> running specs with $lua_bin"
"$lua_bin" "$here/run_specs.lua" "$here" "$generated"

echo "==> luacheck"
cd "$here"
luacheck --config .luacheckrc "$generated" spec support run_specs.lua
