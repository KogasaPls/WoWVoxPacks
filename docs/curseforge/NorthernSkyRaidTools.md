---
summary: Replaces Northern Sky Raid Tools' in-game text-to-speech with pre-rendered {Voice} recordings.
---
# WoWVoxPacks Northern Sky Raid Tools ({Voice})

A TTS voice pack for Northern Sky Raid Tools. Its assignments and alerts are spoken by the Google Cloud **{Voice}** voice ({VoiceDescription}) instead of the game's own text-to-speech.

Before NSRT speaks a word it looks for a sound of that name first. This pack supplies those sounds via LibSharedMedia, so the recording plays and the system TTS is never reached.

No configuration is required, just install and load the addon. Two NSRT settings can stop it working: turning NSRT's text-to-speech off, and turning on its *TTS over sound file* option.

## What it covers

- Raid cooldown assignments read from a note, by spell name: `Tranquility`, `Rallying Cry`, `Anti-Magic Zone`
- Encounter alerts ("Don't soak", "Stop Cast", "Watch Spawns") and NSRT's own vocabulary: `Soak`, `DropPool`, `Dispel`, directions, and the numbers `1` through `10`
- The ready-check reminders NSRT emits as fixed phrases, such as `Soulstone` and `Source of Magic`

## Requirements

Northern Sky Raid Tools, and LibSharedMedia-3.0, which comes bundled with almost every raid addon.

## Which WoWVoxPacks addon do I want?

| Pack | Use it when | Voices at once |
| --- | --- | --- |
| [BigWigs Voice]({Url:BigWigs_Voice}) | you want BigWigs to speak boss ability names | one enabled |
| [BigWigs Countdown]({Url:BigWigs_Countdown}) | you want the BigWigs countdown in this voice | as many as you like |
| [Northern Sky Raid Tools]({Url:NorthernSkyRaidTools}) | you use NSRT and want its callouts to stop going through the in-game text-to-speech | one |
| [Callouts]({Url:Callouts}) | you want to pick voice lines by hand in BigWigs, NSRT or Liquid Reminders | as many as you like |
| [ExBoss]({Url:ExBoss}) | you use ExBoss and want its callouts spoken in English | as many as you like |

## Voices

Wavenet_E (female), Neural2_C (female) and Studio_Q (male). Samples are on the [GitHub page](https://github.com/KogasaPls/WoWVoxPacks#voices). Install only one of them; a second pack registers nothing and says so in chat.

## Source

Generated from Google Cloud Text-to-Speech by [WoWVoxPacks](https://github.com/KogasaPls/WoWVoxPacks), Apache-2.0. A mispronunciation or a callout NSRT makes that this pack misses is worth an issue there.
