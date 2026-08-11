-- Northern Sky Raid Tools, version 12.0.114 (## Version in NorthernSkyRaidTools.toc).
--
-- A verbatim excerpt of the three functions the WoWVoxPacks Northern Sky Raid Tools addon's hook
-- sits in front of:
--
--   * GetTTSSoundFile and NSAPI:TTS, Functions.lua lines 198 and 227
--   * NSI:CacheSounds, Reminders.lua line 1170
--
-- Copied unchanged so the harness exercises NSRT's real lookup, normalisation and colour-code
-- stripping. Only the surrounding scaffolding below is ours: in the addon, NSI is the private
-- table, NSAPI is a global, and NSRT is a SavedVariable.
--
-- To refresh: copy the functions again from a newer NSRT and update the version above. If the
-- excerpt stops matching upstream, the specs are testing a fiction.

NSI = { LSM = LibStub("LibSharedMedia-3.0") }
NSAPI = {}
NSRT = NSRT or {}
NSRT.Settings = NSRT.Settings or { TTS = true, TTSVoice = 0 }
NSRT.ReminderSettings = NSRT.ReminderSettings or { TTSOverSoundfile = false }

-- >>> BEGIN VERBATIM: NorthernSkyRaidTools/Reminders.lua

function NSI:CacheSounds()
    self.LSMSoundCache = {}
    for _, lsmKey in ipairs(self.LSM:List("sound")) do
        local clean = lsmKey:gsub("|c%x%x%x%x%x%x%x%x", "")
                        :gsub("|r", "")
                        :match("^[%s|]*(.-)[%s|]*$")
        self.LSMSoundCache[clean] = lsmKey
        self.LSMSoundCache[strlower(clean)] = lsmKey
        local numeric = tonumber(clean)
        if numeric then
            self.LSMSoundCache[tostring(numeric)] = lsmKey
        end
    end
end

-- <<< END VERBATIM

-- >>> BEGIN VERBATIM: NorthernSkyRaidTools/Functions.lua

local path = "Interface\\AddOns\\NorthernSkyRaidTools\\Media\\Sounds\\"
local function GetTTSSoundFile(sound)
    if not NSI.LSM or not sound then return end

    sound = strtrim(tostring(sound))
    local soundPath = NSI.LSM:Fetch("sound", sound, true)
    if soundPath then return soundPath end

    if not NSI.LSMSoundCache and NSI.CacheSounds then
        NSI:CacheSounds()
    end

    local numeric = tonumber(sound)
    local function GetCachedKey()
        local key = NSI.LSMSoundCache and (NSI.LSMSoundCache[sound] or NSI.LSMSoundCache[strlower(sound)])
        if not key and numeric then
            key = NSI.LSMSoundCache and NSI.LSMSoundCache[tostring(numeric)]
        end
        return key
    end

    local lsmKey = GetCachedKey()
    if not lsmKey and NSI.CacheSounds then
        NSI:CacheSounds()
        lsmKey = GetCachedKey()
    end
    return lsmKey and NSI.LSM:Fetch("sound", lsmKey, true)
end

function NSAPI:TTS(sound, voice) -- NSAPI:TTS("Bait Frontal")
    if NSRT.Settings["TTS"] then
        local secret = issecretvalue(sound)
        local forceTTS = NSRT.ReminderSettings and NSRT.ReminderSettings.TTSOverSoundfile
        local soundFile = (not forceTTS and not secret) and (GetTTSSoundFile(sound) or path..sound..".ogg")
        local handle = soundFile and select(2, PlaySoundFile(soundFile, "Master"))
        if handle then
            return
        else
            sound = tostring(sound)
            local num = voice or NSRT.Settings["TTSVoice"]
            local voices = C_VoiceChat.GetTtsVoices()
            local validVoice = false
            if voices then
                for i, v in ipairs(voices) do
                    if v.voiceID == num then
                        validVoice = true
                        break
                    end
                end
            end
            if not validVoice then num = 0 end
            C_VoiceChat.SpeakText(
                num,
                sound,
                C_TTSSettings and C_TTSSettings.GetSpeechRate() or 0,
                NSRT.Settings.TTSVolume,
                NSRT.Settings.TTSOverlap
            )
        end
    end
end

-- <<< END VERBATIM
