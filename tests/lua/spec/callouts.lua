-- The media pack: WoWVoxPacks_Callouts_{Voice}/Core.lua.

local harness = require("support.harness")
local World = require("support.world")

local test, equal, falsy = harness.test, harness.equal, harness.falsy

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

test("callouts: registers its sounds and says nothing", function()
    local world = World.new()
    world.recorder.loadedAddOns["SharedMedia_Abilities_WoWVoxPacks_Neural2_C"] = true
    world:LoadCallouts("Neural2_C"):Login():Login()

    equal(#world.recorder.printed, 0, "the media pack printed something at login")
    equal(#world.recorder.frames, 0, "the media pack created a frame")
end)

test("callouts: loads and registers nothing when no LibStub is present", function()
    local world = World.new({ libstub = false })

    world:LoadCallouts("Neural2_C"):Login()

    equal(#world.recorder.printed, 0, "the media pack talked with no LibSharedMedia installed")
    falsy(_G.LibStub, "the media pack should not have created a LibStub")
end)
