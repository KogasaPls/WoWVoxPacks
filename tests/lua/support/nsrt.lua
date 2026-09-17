-- Northern Sky Raid Tools, version 12.1.23 (## Version in NorthernSkyRaidTools.toc).
--
-- A verbatim excerpt of the three functions the WoWVoxPacks Northern Sky Raid Tools addon's hook
-- sits in front of:
--
--   * GetTTSSoundFile and NSAPI:TTS, Functions.lua lines 254 and 272
--   * NSI:CacheSounds, Reminders.lua line 1515
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
    local cache = NSI.LSMSoundCache
    local lsmKey = cache and (cache[sound] or cache[strlower(sound)])
    if cache and not lsmKey and numeric then
        lsmKey = cache[tostring(numeric)]
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
            NSI.TTSVoiceValidity = NSI.TTSVoiceValidity or {}
            local validVoice = NSI.TTSVoiceValidity[num]
            if validVoice == nil then
                validVoice = false
                local voices = C_VoiceChat.GetTtsVoices()
                if voices then
                    for i, v in ipairs(voices) do
                        if v.voiceID == num then
                            validVoice = true
                            break
                        end
                    end
                end
                NSI.TTSVoiceValidity[num] = validVoice
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
