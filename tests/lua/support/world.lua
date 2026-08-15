-- One WoW session: the stubbed game, the real LibSharedMedia stack, and whichever generated
-- addon folders a spec asks for. Every spec starts from a new world, so LibStub's registry,
-- LibSharedMedia's media table and the Northern Sky Raid Tools addon's own upvalues are fresh.

local wow = require("support.wow")

local World = {}
World.__index = World

local LIBS = WVP_HARNESS_DIR .. "/libs"
local SUPPORT = WVP_HARNESS_DIR .. "/support"

-- Everything the addons, the libraries or the NSRT excerpt leave lying around in _G.
local GLOBALS = {
    "LibStub", "NSAPI", "NSRT", "WoWVoxPacksNorthernSkyRaidToolsVoice",
    "BigWigsAPI", "BigWigsLoader"
}

local function Load(path, ...)
    local chunk, err = loadfile(path)
    if not chunk then error(err, 0) end
    return chunk(...)
end

--- @param options table
---   libstub false to run with no LibStub at all (no LibSharedMedia provider installed)
function World.new(options)
    options = options or {}
    for _, name in ipairs(GLOBALS) do _G[name] = nil end

    local self = setmetatable({}, World)
    self.recorder = wow.install()

    if options.libstub ~= false then
        Load(LIBS .. "/LibStub/LibStub.lua")
        Load(LIBS .. "/CallbackHandler-1.0/CallbackHandler-1.0.lua")
        Load(LIBS .. "/LibSharedMedia-3.0/LibSharedMedia-3.0.lua")
        self.lsm = LibStub("LibSharedMedia-3.0")
    end

    return self
end

--- Loads one generated addon folder exactly as the game would: mark it enabled, run its Lua
--- with the addon name and its private table, which is what `...` yields in a WoW addon file.
function World:LoadAddOn(folder, file)
    self.recorder.loadedAddOns[folder] = true
    self.addOnTables = self.addOnTables or {}
    self.addOnTables[folder] = {}
    Load(WVP_GENERATED_DIR .. "/" .. folder .. "/" .. (file or "Core.lua"), folder,
        self.addOnTables[folder])
    self:RefreshFiles()
    return self
end

function World:LoadCallouts(voice)
    return self:LoadAddOn("WoWVoxPacks_Callouts_" .. voice)
end

function World:LoadNorthernSkyRaidTools(voice)
    return self:LoadAddOn("WoWVoxPacks_NorthernSkyRaidTools_" .. (voice or "Neural2_C"))
end

--- Every path LibSharedMedia knows about is a file that exists, which is what lets
--- PlaySoundFile answer honestly for a key that resolved and refuse one that did not.
function World:RefreshFiles()
    if not self.lsm then return end
    for _, soundPath in pairs(self.lsm:HashTable("sound")) do
        self.recorder.files[soundPath] = true
    end
end

function World:LoadNsrt()
    Load(SUPPORT .. "/nsrt.lua")
    return self
end

function World:CaptureLsmRegistrations()
    local registrations = {}
    local original = self.lsm.Register
    self.lsm.Register = function(lsm, mediaType, key, path, ...)
        registrations[#registrations + 1] = { mediaType = mediaType, key = key, path = path }
        return original(lsm, mediaType, key, path, ...)
    end
    return registrations
end

function World:Event(event, ...)
    for _, frame in ipairs(self.recorder.frames) do
        if frame.events[event] and frame.scripts.OnEvent then
            frame.scripts.OnEvent(frame, event, ...)
        end
    end
    return self
end

function World:Login()
    return self:Event("PLAYER_LOGIN")
end

function World:TTS(sound)
    NSAPI:TTS(sound)
    return self
end

function World:LastPlayed()
    return self.recorder.played[#self.recorder.played]
end

return World
