-- The media pack: WoWVoxPacks_Callouts_{Voice}/Core.lua.

local harness = require("support.harness")
local World = require("support.world")

local test, equal, truthy, falsy = harness.test, harness.equal, harness.truthy, harness.falsy

local function SoundPath(voice, file)
    return "Interface\\AddOns\\WoWVoxPacks_Callouts_" .. voice .. "\\Sounds\\" .. file
end

test("callouts: registers its sounds under the colour-wrapped WVP key", function()
    local world = World.new():LoadCallouts("Neural2_C")

    equal(world.lsm:Fetch("sound", "|cffff7f3fWVP Neural2_C: AMS|r", true),
        SoundPath("Neural2_C", "ams.ogg"), "AMS did not resolve to the pack's file")
end)

test("callouts: registers a hidden defensive sourced from lorrgs", function()
    local world = World.new():LoadCallouts("Neural2_C")

    equal(world.lsm:Fetch("sound", "|cffff7f3fWVP Neural2_C: Anti-Magic Shell|r", true),
        SoundPath("Neural2_C", "anti_magic_shell.ogg"),
        "the lorrgs defensive did not resolve to the pack's file")
end)

test("callouts: two packs register side by side without colliding", function()
    local world = World.new():LoadCallouts("Neural2_C"):LoadCallouts("Studio_O")

    equal(world.lsm:Fetch("sound", "|cffff7f3fWVP Neural2_C: AMS|r", true),
        SoundPath("Neural2_C", "ams.ogg"))
    equal(world.lsm:Fetch("sound", "|cffff7f3fWVP Studio_O: AMS|r", true),
        SoundPath("Studio_O", "ams.ogg"))
end)

test("callouts: prints nothing when the pre-rename folder is not installed", function()
    local world = World.new():LoadCallouts("Neural2_C"):Login()

    equal(#world.recorder.printed, 0,
        "the media pack warned about a folder that is not installed")
end)

test("callouts: warns once, and only once, when the pre-rename folder is installed", function()
    local world = World.new()
    world.recorder.loadedAddOns["SharedMedia_Abilities_WoWVoxPacks_Neural2_C"] = true
    world:LoadCallouts("Neural2_C"):Login():Login():Login()

    equal(#world.recorder.printed, 1, "the stale-folder warning did not fire exactly once")
    truthy(world.recorder.printed[1]:find("safe to delete once you have re-picked your sounds",
        1, true), "the warning no longer tells the user deleting now is not urgent")
end)

test("callouts: loads and registers nothing when no LibStub is present", function()
    local world = World.new({ libstub = false })

    world:LoadCallouts("Neural2_C"):Login()

    equal(#world.recorder.printed, 0, "the media pack talked with no LibSharedMedia installed")
    falsy(_G.LibStub, "the media pack should not have created a LibStub")
end)
