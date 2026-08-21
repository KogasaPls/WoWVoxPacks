# WoWVoxPacks

A collection of modern TTS voices for World of Warcraft addons.

## AddOns

- WoWVoxPacks_{Voice}_BigWigs_Voice: A clone of [BigWigs_Voice](https://www.curseforge.com/wow/addons/bigwigs_voice)
  with a different TTS voice. Install as many voices as you like, but **enable only one BigWigs voice pack at a time**,
  BigWigs_Voice included: every pack registers the same name and `BigWigsAPI.RegisterVoicePack` errors on a duplicate.
- WoWVoxPacks_{Voice}_BigWigs_Countdown: Adds a new voice option for BigWigs' countdown (must be configured in BigWigs).
- WoWVoxPacks_{Voice}_Callouts: Spell names and other generic callouts.
- WoWVoxPacks_{Voice}_NorthernSkyRaidTools: Install exactly one voice addon for Northern Sky Raid Tools. It contains
  its own recordings, including the Callouts spell names under plain keys so NSRT plays them automatically for
  assignments and alerts, and needs neither Callouts nor an in-game setting.

<video src='https://github.com/user-attachments/assets/8bceffae-2e57-49cb-bb74-aab43ac65ae7' width=180></video>

## Voices

Here's a sample of the original BigWigs_Voice audio for comparison.

- <video src='https://github.com/user-attachments/assets/9aeffdd5-a0a0-4021-9869-e2827241be27' width=10>BigWigs_Voice
  sample</video>

The following voices are available.

### Google Cloud Text-to-Speech

- Neural2_C (Female)
  - <video src='https://github.com/user-attachments/assets/b5b99b0b-cfdf-4106-8461-d9df8588a4e4' width=10></video>
- Wavenet_E (Female)
  - <video src='https://github.com/user-attachments/assets/78969d82-4878-403c-8201-c220f26f8ecc' width=10></video>
- Standard_D (Male)
  - <video src='https://github.com/user-attachments/assets/bc28901b-165b-4d73-8fb5-956e53a95acd' width=10></video>
- Studio_Q (Male)
  - <video src='https://github.com/user-attachments/assets/c6f39c01-4934-41dd-9f4e-7b3ca7acffdc' width=10></video>
- Studio_O (Female)
  - <video src='https://github.com/user-attachments/assets/04bd2217-f757-4165-8414-9ea080eec041' width=10></video>

## CurseForge pages

`docs/curseforge/` holds one page per addon, written with `{Voice}` where the voice name goes.
CurseForge has no API for editing a project description, so publishing is a paste into the web
editor; `scripts/curseforge_description.py --addon Callouts --voice Wavenet_E` prints the
substituted markdown, and `--summary` prints the summary field.

## Tests

`dotnet test` covers the generators. `tests/lua/run.sh` regenerates the addon Lua from the C#
and actually runs it against the real LibSharedMedia and a verbatim Northern Sky Raid Tools
excerpt, then luachecks it; it needs `luajit` (or `lua5.1`) and `luacheck` on PATH. CI runs both.
