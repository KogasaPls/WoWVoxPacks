-- usage: luajit run_specs.lua <harness-dir> <generated-lua-dir>
--
-- <generated-lua-dir> holds one folder per addon, each containing the Lua the C# generators
-- emit. run.sh produces it; it is never a copy pasted into this tree.

local args = { ... }

WVP_HARNESS_DIR = assert(args[1], "harness directory argument missing")
WVP_GENERATED_DIR = assert(args[2], "generated Lua directory argument missing")

package.path = WVP_HARNESS_DIR .. "/?.lua;" .. package.path

local harness = require("support.harness")

for _, spec in ipairs({ "callouts", "northern_sky_raid_tools", "packs" }) do
    require("spec." .. spec)
end

os.exit(harness.run() == 0 and 0 or 1)
