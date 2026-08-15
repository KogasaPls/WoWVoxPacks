-- luacheck config for the generated addon Lua and the harness that runs it.
--
-- The point is the checks luacheck can make without a WoW client: unused locals, shadowing,
-- unreachable code, and globals nobody defines. So the WoW API surface is declared rather than
-- silenced wholesale.

std = "luajit"

-- Generated Lua wraps at whatever the sound name needs; wrapping it would be churn.
max_line_length = false

-- Vendored verbatim. Linting someone else's release only produces noise we must not fix.
exclude_files = {
    "libs",
    "support/nsrt.lua"
}

globals = {
    "NSAPI",
    -- Set by the harness.
    "_G",
    "WVP_HARNESS_DIR",
    "WVP_GENERATED_DIR",
    "BigWigsAPI",
    "BigWigsLoader"
}

read_globals = {
    "C_AddOns",
    "CreateFrame",
    "LibStub",
    "PlaySoundFile",
    -- WoW's global alias for string.format, which the BigWigs pack localises.
    "format"
}
