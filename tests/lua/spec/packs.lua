-- The two packs that are not part of the Callouts work but ship in the same release:
-- ExBoss_WoWVoxPacks_{Voice}/Core.lua and BigWigs_Countdown_WoWVoxPacks_{Voice}/Countdown.lua.

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
