-- Per-voice Northern Sky Raid Tools packs, exercised through NSRT's real lookup excerpt.

local harness = require("support.harness")
local World = require("support.world")

local test, equal, falsy = harness.test, harness.equal, harness.falsy

local function SoundPath(voice, file)
    return "Interface\\AddOns\\WoWVoxPacks_NorthernSkyRaidTools_" .. voice .. "\\Sounds\\" .. file
end

test("northern sky raid tools: a literal key resolves to the per-voice recording", function()
    local world = World.new():LoadNsrt():LoadNorthernSkyRaidTools("Neural2_C")

    world:TTS("DropPool")

    equal(world:LastPlayed(), SoundPath("Neural2_C", "drop_pool.ogg"))
end)

test("northern sky raid tools: NSRT case-insensitive fallback resolves the literal key", function()
    local world = World.new():LoadNsrt():LoadNorthernSkyRaidTools("Neural2_C")

    world:TTS("droppool")

    equal(world:LastPlayed(), SoundPath("Neural2_C", "drop_pool.ogg"))
end)

test("northern sky raid tools: NSRT numeric normalization resolves the literal key", function()
    local world = World.new():LoadNsrt():LoadNorthernSkyRaidTools("Neural2_C")

    for _, input in ipairs({ "01", "1.0", 1 }) do
        world:TTS(input)
        equal(world:LastPlayed(), SoundPath("Neural2_C", "one.ogg"), "input " .. tostring(input))
    end
end)

test("northern sky raid tools: loads quietly without LibSharedMedia", function()
    local world = World.new({ libstub = false })

    world:LoadNorthernSkyRaidTools("Neural2_C")

    falsy(_G.LibStub)
    falsy(_G.WoWVoxPacksNorthernSkyRaidToolsVoice)
    equal(#world.recorder.printed, 0)
end)

test("northern sky raid tools: a second voice reports one conflict and registers nothing", function()
    local world = World.new():LoadNsrt():LoadNorthernSkyRaidTools("Neural2_C")
    local registrations = world:CaptureLsmRegistrations()

    world:LoadNorthernSkyRaidTools("Studio_O")
    world:TTS("DropPool")

    equal(#registrations, 0, "the second voice pack attempted LibSharedMedia registration")
    equal(world:LastPlayed(), SoundPath("Neural2_C", "drop_pool.ogg"))
    equal(#world.recorder.printed, 1)
    equal(world.recorder.printed[1],
        "WoWVoxPacks: another Northern Sky Raid Tools voice pack is already active " ..
        "(Neural2_C); Studio_O registered nothing.")
end)
