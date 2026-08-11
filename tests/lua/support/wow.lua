-- Stubs for the game itself: everything the generated Lua, the vendored libraries and the NSRT
-- excerpt reach for that WoW would provide. Nothing here reimplements addon logic.
--
-- Each stub records what it was asked to do on the returned recorder, so a spec asserts on
-- observable behaviour (a file played, a line printed, text spoken) rather than on strings.

local wow = {}

--- Installs a fresh set of globals. Returns a recorder the specs assert against.
-- @param options table
function wow.install()
    local recorder = {
        printed = {},        -- every print() line
        played = {},         -- every PlaySoundFile path, in order
        frames = {},         -- every CreateFrame, so specs can fire events at them
        files = {},          -- sound paths that exist on disk
        loadedAddOns = {},   -- folder names C_AddOns.IsAddOnLoaded answers yes for
        secrets = {}         -- values issecretvalue answers yes for
    }

    _G.GetLocale = function() return "enUS" end

    _G.print = function(...)
        local parts = {}
        for i = 1, select("#", ...) do
            parts[i] = tostring((select(i, ...)))
        end
        recorder.printed[#recorder.printed + 1] = table.concat(parts, " ")
    end

    -- WoW's global aliases for the string library. LibStub and the NSRT excerpt use them.
    _G.strtrim = function(text) return (text:gsub("^%s+", ""):gsub("%s+$", "")) end
    _G.strlower = string.lower
    _G.strupper = string.upper
    _G.strmatch = string.match
    _G.strfind = string.find
    _G.strsub = string.sub
    _G.strrep = string.rep
    _G.format = string.format
    _G.tinsert = table.insert
    _G.tremove = table.remove
    _G.wipe = function(t)
        for key in pairs(t) do t[key] = nil end
        return t
    end

    _G.CreateFrame = function()
        local frame = { events = {}, scripts = {} }
        function frame:RegisterEvent(event) self.events[event] = true end
        function frame:SetScript(handler, fn) self.scripts[handler] = fn end
        recorder.frames[#recorder.frames + 1] = frame
        return frame
    end

    _G.C_AddOns = {
        IsAddOnLoaded = function(name) return recorder.loadedAddOns[name] == true end
    }

    -- WoW returns willPlay=false for a path that is not in the client's files.
    _G.PlaySoundFile = function(soundPath, channel)
        recorder.played[#recorder.played + 1] = soundPath
        if not recorder.files[soundPath] then return false end
        return true, #recorder.played, channel
    end

    _G.issecretvalue = function(value) return recorder.secrets[value] == true end

    return recorder
end

return wow
