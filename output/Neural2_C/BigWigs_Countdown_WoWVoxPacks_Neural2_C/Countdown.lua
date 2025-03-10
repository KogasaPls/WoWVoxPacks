local L = BigWigsAPI:GetLocale("BigWigs")
local LSM = LibStub("LibSharedMedia-3.0")

local key = "WoWVoxPacks: Neural2_C"
local path = "Interface\\AddOns\\BigWigs_Countdown_WoWVoxPacks_Neural2_C\\Sounds\\countdown_%d.ogg"
--------------------------------------------------------------------------------

BigWigsAPI:RegisterCountdown(key, {
    path:format(1),
    path:format(2),
    path:format(3),
    path:format(4),
    path:format(5),
    path:format(6),
    path:format(7),
    path:format(8),
    path:format(9),
    path:format(10),
})