---
summary: TTS sound files for Northern Sky Raid Tools, using Google Cloud {Voice} ({VoiceDescription}).
---
# WoWVoxPacks Northern Sky Raid Tools ({Voice})

A TTS voice pack for Northern Sky Raid Tools. Its assignments and alerts are spoken by the Google Cloud **{Voice}** voice ({VoiceDescription}) instead of the game's own text-to-speech.

Before NSRT speaks a word it looks for a sound of that name first. This pack supplies those sounds via LibSharedMedia, so the recording plays and the system TTS is never reached.

No configuration is required, just install and load the addon. Two NSRT settings can stop it working: turning NSRT's text-to-speech off, and turning on its *TTS over sound file* option.

## What it covers

- Raid cooldown assignments read from a note, by spell name: `Tranquility`, `Rallying Cry`, `Anti-Magic Zone`
- Encounter alerts ("Don't soak", "Stop Cast", "Watch Spawns") and the mechanic words NSRT uses: `Soak`, `Dispel`, `Frontal`, directions, and the numbers `1` through `10`
- The ready-check reminders NSRT emits as fixed phrases, such as `Soulstone` and `Source of Magic`

## World of Warcraft: Forever

A separate World of Warcraft: Forever file is available from this project. It carries only sounds that can play on Forever: NSRT's own sound names and the generic raid note phrases such as `Preposition at`, `Soak` and `Go to` with markers and positions. It leaves out the Midnight encounter alerts and the Rebuff reminders.

## Requirements

Northern Sky Raid Tools, and LibSharedMedia-3.0, which comes bundled with almost every raid addon.

{PacksTable}

## Voices

Wavenet_E (en_US female), Neural2_C (en_US female) and Studio_Q (en_US male). Samples are on the [GitHub page](https://github.com/KogasaPls/WoWVoxPacks#voices). Install only one of them; a second pack registers nothing and says so in chat.

## Source

Generated from Google Cloud Text-to-Speech by [WoWVoxPacks](https://github.com/KogasaPls/WoWVoxPacks), Apache-2.0. A mispronunciation or a callout NSRT makes that this pack misses is worth an issue there.
