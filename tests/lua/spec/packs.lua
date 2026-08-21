-- The packs that are not part of the Callouts work but ship in the same release:
-- BigWigs_Voice_WoWVoxPacks_{Voice}/Core.lua, ExBoss_WoWVoxPacks_{Voice}/Core.lua and
-- BigWigs_Countdown_WoWVoxPacks_{Voice}/Countdown.lua.

local harness = require("support.harness")
local World = require("support.world")

local test, equal, truthy, falsy = harness.test, harness.equal, harness.truthy, harness.falsy

local function StubBigWigs()
    local registered = {}
    _G.BigWigsAPI = {
        GetLocale = function() return setmetatable({}, { __index = function(_, k) return k end }) end,
        -- Called with a colon, so BigWigsAPI itself arrives as the first argument.
        RegisterCountdown = function(_, key, files) registered[key] = files end
    }
    return registered
end

local VOICE_SOUNDS = "Interface\\AddOns\\BigWigs_Voice_WoWVoxPacks_Neural2_C\\Sounds\\"

--- Stands in for BigWigs itself: keeps the handler the pack registers, and records anything
--- the pack hands back for BigWigs to sound instead.
local function StubBigWigs_Voice()
    local bigwigs = { handedBack = {} }
    _G.BigWigsAPI = { RegisterVoicePack = function(pack) bigwigs.pack = pack end }
    _G.BigWigsLoader = {
        -- Both are called with a dot, so the addon table arrives as the first argument.
        RegisterMessage = function(_, message, handler)
            if message == "BigWigs_Voice" then bigwigs.handler = handler end
        end,
        SendMessage = function(_, message, _, key, sound)
            bigwigs.handedBack[#bigwigs.handedBack + 1] = { message = message, key = key, sound = sound }
        end
    }
    return bigwigs
end

local function LoadBigWigs_Voice(world)
    local bigwigs = StubBigWigs_Voice()
    world:LoadAddOn("BigWigs_Voice_WoWVoxPacks_Neural2_C")
    truthy(bigwigs.handler, "the pack no longer registers a BigWigs_Voice handler")
    return bigwigs
end

test("bigwigs voice: plays the recording named after the spell", function()
    local world = World.new()
    local bigwigs = LoadBigWigs_Voice(world)
    world.recorder.files[VOICE_SOUNDS .. "381862.ogg"] = true

    bigwigs.handler("BigWigs_Voice", {}, 381862, "Alarm")

    equal(world:LastPlayed(), VOICE_SOUNDS .. "381862.ogg")
    equal(#bigwigs.handedBack, 0, "the pack handed a spell it can voice back to BigWigs")
end)

-- No y variant is rendered for any spell, so before the fallback existed every on-me callout
-- lost the spell name and came out as whatever generic sound BigWigs had for it.
test("bigwigs voice: an on-me callout falls back to the spell recording", function()
    local world = World.new()
    local bigwigs = LoadBigWigs_Voice(world)
    world.recorder.files[VOICE_SOUNDS .. "381862.ogg"] = true

    bigwigs.handler("BigWigs_Voice", {}, 381862, "Alarm", true)

    equal(world.recorder.played[1], VOICE_SOUNDS .. "381862y.ogg")
    equal(world:LastPlayed(), VOICE_SOUNDS .. "381862.ogg")
    equal(#bigwigs.handedBack, 0, "an on-me callout was handed back with the recording present")
end)

test("bigwigs voice: an on-me callout prefers the y recording when one exists", function()
    local world = World.new()
    local bigwigs = LoadBigWigs_Voice(world)
    world.recorder.files[VOICE_SOUNDS .. "381862.ogg"] = true
    world.recorder.files[VOICE_SOUNDS .. "381862y.ogg"] = true

    bigwigs.handler("BigWigs_Voice", {}, 381862, "Alarm", true)

    equal(world:LastPlayed(), VOICE_SOUNDS .. "381862y.ogg")
    equal(#world.recorder.played, 1, "the y recording played and the pack kept going")
end)

test("bigwigs voice: hands a spell it has no recording for back to BigWigs", function()
    local world = World.new()
    local bigwigs = LoadBigWigs_Voice(world)

    bigwigs.handler("BigWigs_Voice", {}, 999999, "Alarm", true)

    equal(#bigwigs.handedBack, 1, "BigWigs was not asked to sound the callout itself")
    equal(bigwigs.handedBack[1].message, "BigWigs_Sound")
    equal(bigwigs.handedBack[1].sound, "Alarm")
end)

test("exboss: registers its labels with LibSharedMedia", function()
    local world = World.new():LoadAddOn("ExBoss_WoWVoxPacks_Neural2_C")

    truthy(world.lsm:Fetch("sound", "[ExBoss WoWVoxPacks Neural2_C]准备引线", true),
        "the shipped ExBoss key stopped resolving")
    equal(#world.lsm:List("sound") > 100, true, "far fewer labels registered than expected")
end)

test("exboss: loads without erroring when no LibStub is present", function()
    local world = World.new({ libstub = false })

    world:LoadAddOn("ExBoss_WoWVoxPacks_Neural2_C")

    falsy(_G.LibStub, "ExBoss should not have created a LibStub")
    equal(#world.recorder.printed, 0)
end)

test("countdown: registers ten files with BigWigs and never touches LibStub", function()
    local world = World.new({ libstub = false })
    local registered = StubBigWigs()

    world:LoadAddOn("BigWigs_Countdown_WoWVoxPacks_Neural2_C", "Countdown.lua")

    local files = registered["WoWVoxPacks: Neural2_C"]
    truthy(files, "the shipped countdown key stopped being registered")
    equal(#files, 10)
    equal(files[1],
        "Interface\\AddOns\\BigWigs_Countdown_WoWVoxPacks_Neural2_C\\Sounds\\countdown_1.ogg")
    equal(files[10],
        "Interface\\AddOns\\BigWigs_Countdown_WoWVoxPacks_Neural2_C\\Sounds\\countdown_10.ogg")
    falsy(_G.LibStub, "the countdown pack asked for LibSharedMedia it does not use")
    equal(#world.recorder.printed, 0)
end)
