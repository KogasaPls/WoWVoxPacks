# NSRT optional TTS: special displays and runtime reminders

This is the pre-implementation audit. The [implementation and full new sound list](2026-10-20-nsrt-optional-tts.md) record the subsequent additions and local generation.

Date: 2026-10-20. Bounded read-only source audit of NSRT commit
[`190084a202cd7359c42616419acf8d9c98cc25f3`](https://github.com/Reloe/NorthernSkyRaidTools/tree/190084a202cd7359c42616419acf8d9c98cc25f3)
against WoWVoxPack main `d202044441a32a3866715ab4e8ef5a02b476266e` and the existing generated output.
This supplements the ordinary `TTS = false` alert-table inventory. No source, overrides,
recordings, installed addon, UI session, or remote state was changed for this audit.

## Result

**Nine finite optional inputs supplement the ordinary editable-alert inventory:**

- `Drop-Pool` (Entombed Sentinels' special Tank Drop Pool reminder).
- `Soak {rt1}`, `Soak {rt2}`, `Soak {rt3}`, `Soak {rt4}`, `Soak {rt5}`,
  `Soak {rt6}`, `Soak {rt7}`, `Soak {rt8}` (Ula'tek transition reminders).

All nine are absent from the generated configured NSRT voice packs' media keys,
including the Callouts registrations folded into those packs. All nine also lack an
exact native `Media/Sounds/<input>.ogg` fallback. `Drop Pool`, `DropPool`, `Soak Circle`,
and similar recordings do not supply these exact keys. The marker braces are part of
the actual TTS input; they are converted to icons only in display text. [S1], [S2], [S3], [S29], [S33]

Three additional runtime inputs, **`SWAPPED`, `PICK UP`, and `Your Interrupt`**, are also
missing, but their literal definitions belong in the ordinary editable-alert inventory
and must not be counted twice. Thus this note identifies **12 distinct missing finite
inputs in runtime/special paths, of which nine are additions to that inventory**.
`Your Interrupt` is normally masked by its selected native Interrupt sound. [S4], [S5], [S6], [S30], [S31], [S32]

The existing generated packs already register Ula'tek's optional wave defaults
`< Left + Dodge` and `Right > + Dodge`. These resolve to `left.ogg` and `right.ogg`
respectively; key coverage alone does not assert that a recording speaks every word
in the display label. [S7]

## Comparison method and output boundary

The five voices configured in [appsettings.json](../../appsettings.json) are
`Neural2_C`, `Wavenet_E`, `Standard_D`, `Studio_Q`, and `Studio_O`. Their generated
`output/<voice>/WoWVoxPacks_NorthernSkyRaidTools_<voice>/Core.lua` files each contain
430 sound registrations and identical case-insensitive key sets. Every referenced
sound file exists in these five generated packs. Representative generated evidence:
[Standard_D Core.lua](../../output/Standard_D/WoWVoxPacks_NorthernSkyRaidTools_Standard_D/Core.lua).

I extracted the actual `LSM:Register("sound", ...)` keys from those files, also checked
the corresponding generated Callouts Core.lua files, stripped WoW color wrappers and
edge whitespace as NSRT's cache does, and compared case-insensitively. I did **not**
strip `WVP <voice>:` from Callouts keys: NSRT does not strip that prefix, so a matching
Callouts display name alone is insufficient. The NSRT pack folds useful Callouts
recordings into literal unprefixed registrations; the generated file, rather than the
source vocabulary or audio filename, is the coverage authority. [S8]

The output tree also contains an unconfigured `Chirp3_Aoede` pack with 94 keys and a
different key set. The five-voice coverage statements above do not certify that older
output. No generation or billed operation was run.

NSRT first resolves TTS through LibSharedMedia, then tries its own exact native path,
then falls back to the Blizzard voice. Explicit reminder `sound` is attempted before
TTS. `TTSOverSoundfile` deliberately bypasses the TTS sound-file lookup. Native audio
does not erase a WoWVoxPack gap. [S8], [S9]

## Reachable optional runtime inputs

| Path | Default input after enabling TTS | Setting and execution evidence | Pack result |
| --- | --- | --- | --- |
| Entombed Sentinels `BloodDropPool` | `Drop-Pool` | `TTS=false`, `isSpecialDisplay=true`, no `NoEdit`; runtime copies the alert and calls `CreateReminder`/`DisplayReminder`. The normal Sound panel is editable. [S1], [S28], [S33] | Missing. Existing `Drop Pool` key differs by punctuation. |
| Ula'tek `TransitionPatternSoaks` | `Soak {rt1}` through `Soak {rt8}` | Editable `TTS=false` alert; the finite pattern tables contain marker IDs 1–8. Runtime appends the marker to default `Soak` **before** `CreateReminder`. Boolean TTS uses the raw text captured before display icon substitution. [S2], [S3], [S29] | All eight missing. |
| Beloren `Color Swap` | `SWAPPED` | Editable `TTS=false` alert. Both preview and live paths pass the saved setting through `CreateReminder` before adding icon decoration to `info.text`; the decorations therefore do not enter the default TTS string. [S4], [S10], [S30] | Missing; overlaps ordinary inventory. |
| Midnight Falls `CrystalDropTimer` | `PICK UP` | Editable `TTS=false`, `enabled=false` alert; the crystal cast handler creates the reminder from the saved alert. Enable the alert and TTS to reach it. [S5], [S10], [S31] | Missing; overlaps ordinary inventory. |
| Twin Fangs `InterruptWhisperAlert` | `Your Interrupt` | Editable `TTS=false`, `enabled=false`; a whisper displays a reminder copied from the saved alert. The default color-wrapped `Interrupt` sound plays first, so clear/fail that sound to reach the TTS input. Optional sender names are added to `info.text` **after** reminder construction and do not change the default TTS input. [S6], [S9], [S10], [S32] | Missing; overlaps ordinary inventory. |
| Ula'tek `WaveDirection` | `< Left + Dodge`, `Right > + Dodge` | `NoEdit=true` locks the ordinary Sound panel, but custom Options provide an explicit Enable TTS checkbox and six editable text entries. Runtime uses the supplied text and copies `alert.TTS`. [S7], [S15], [S28], [S29] | Both literal media keys present. User replacements are unbounded. |

`isSpecialDisplay` alone is **not** a Sound-panel lock. It prevents ordinary
`FireEncounterAlerts` scheduling; the encounter-specific handler can still construct
a reminder. `NoEdit` locks Display and Sound, with custom Options providing any
exceptions. [S28], [S11]

## Rows that should not become missing optional recordings

| Row / visible text | Why its default is not a reachable optional TTS phrase |
| --- | --- |
| Coiled Altar and Ula'tek `WRONG TARGET` | Both special runtime constructors explicitly set `TTS=false`, rather than copying the editable alert setting. Enabling TTS in the normal panel cannot make these runtime displays speak. [S12], [S34] |
| Twin Fangs `Eternal Venom Overview` | `NoEdit=true`, no alternate TTS control; its runtime handler creates a debuff overview, not a speaking reminder. [S13] |
| Ula'tek `Auto Release` | `NoEdit=true`, no alternate TTS control; it is an automation control, not a configurable spoken alert. [S14], [S36] |
| Ula'tek `Input Direction` | `WaveDirectionPrompt` has `NoEdit=true` and `TTS=false`, with no alternate TTS option. Its shared helper copies that false value. The neighboring WaveDirection custom toggle only changes `WaveDirection.TTS`. [S7], [S15], [S35] |
| Sszorak debuff-target bars | Runtime creates reminders with `TTS=false` regardless of the table's editable TTS field, then adds player names to their display text. [S16] |
| Interrupt display bars | `ShowInterruptBar` explicitly writes `info.TTS=false`; interrupt audio instead uses the separately selected `InterruptSound`. [S17] |

Other nil/empty custom displays do not add finite optional English inputs: Beloren
Feather Color, Salhadaar CC Display, Vaelgor/Ezzorak HealthDisplay, Midnight Falls
RunesDisplay and InterruptDisplay, Rotmire InterruptDisplay, Sszorak WindsHelper,
Sentinels Radar, Coiled Altar Orb/Bomb Circle and Eternal Nightfall Absorb, Ula'tek
Grasping Fangs Overview and interrupt assignment displays. Their handlers render
textures, names, health, circles, bars, or interrupt ownership; changing a generic
TTS setting does not insert a TTS call into those handlers. Representative source
definitions/handlers: [S18], [S19], [S20], [S21]. Locked rows remain distinct from editable
fields whose specialized runtime handler simply does not consume TTS.

Lightblinded Vanguard's `Heal Absorb Ticks` has `text=""`. In Lua the empty string is
truthy: `not info.text` is false, so the spell-name fallback does not run. Enabling
boolean TTS with the default empty text supplies an empty TTS string, not the runtime
name of spell 1248721. A user-written TTS override is arbitrary content. [S3], [S22]

## Direct calls, spell names, and other dynamic content

The non-library addon/UI call-site scan found these direct TTS families:

- Vanguard's special taunt display calls `NSAPI:TTS(info.text)` directly. Its default
  `Taunt` key is covered. The per-alert `info.TTS` value is not consulted by this call;
  the global TTS switch still applies. Custom display text is arbitrary. [S23], [S8]
- Ready checks directly emit `Soulstone`, `Source of Magic`, `Blistering Scales`, and
  `Symbiotic Relationship`; all four are covered. `Rebuff ` is concatenated with a
  runtime spell name from the fixed buff table. Existing generated keys cover the
  expected English results `Rebuff Battle Shout`, `Rebuff Power Word: Fortitude`,
  `Rebuff Skyfury`, `Rebuff Arcane Intellect`, `Rebuff Soulstone`,
  `Rebuff Mark of the Wild`, and `Rebuff Blessing of the Bronze`. The names themselves
  come from `C_Spell.GetSpellInfo`, so build/locale-dependent output is not proved by
  the source comments. A failed spell lookup can produce the incomplete input
  `Rebuff `; that is a lookup-failure case, not another useful fixed callout. [S24]
- Countdown calls pass numbers to `NSAPI:TTS`; generated keys 1–10 are covered.
  User-configured larger or nonstandard countdown values are not a bounded default
  vocabulary claim. [S25]
- General settings' TTS Preview passes arbitrary user text. It defaults to empty and
  has no additional fixed English phrase. [S26]
- Reminder sound playback passes the reminder's string, raw text, or secret value
  through the generic TTS path. User notes, custom alerts, custom hooks, numeric
  durations, localized spell names, and arbitrary TTS overrides cannot be closed by
  enumerating fixed source literals. If text is **nil** and a spell is supplied,
  `SpellName`/`SpellNameTTS` can resolve the runtime name; the empty-string case differs.
  Optional duration announcements append ` in <TTSTimer>` only for reminders that
  are neither alerts nor assignments. [S3], [S9]

The runtime assignment families use the finite inputs `Soak`, `Don't soak`,
`Soak Left`, `Soak Right`, `Soak Front Left`, `Soak Front Right`, `Soak Back Left`,
`Soak Back Right`, and `Go to Front Left`, `Go to Front Right`, `Go to Back Left`,
`Go to Back Right`, `Go to Flex Spot`; these all have generated keys. Although a
mechanical cross-product would suggest `Soak Flex Spot`, that branch is unreachable:
non-healer group numbers are clamped to 4, and the fifth healer uses `Go to`. [S27]

## Evidence limits

This is a pinned source/control-flow and generated-registration audit. It does not
prove combat execution, secret-value legality, actual speech pronunciation, addon
load order, an installed voice selection, sound-cache refresh, or live encounter
timing. I did not drive the running WoW session or inspect SavedVariables. Native
fallback presence was checked as a source asset/path question, not by playing audio.
No implementation, audio generation, installation, push, or publication is implied.

## Pinned source references

[S1]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/EntombedSentinels.lua#L73-L95
[S2]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/Ulatek.lua#L747-L870
[S3]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/Reminders.lua#L101-L153
[S4]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS1/Beloren.lua#L78-L96
[S5]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS1/MidnightFalls.lua#L263-L282
[S6]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/TheTwinFangs.lua#L72-L99
[S7]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/Ulatek.lua#L547-L589
[S8]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/Functions.lua#L263-L320
[S9]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/Reminders.lua#L1572-L1624
[S10]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools_UI/UI/EncounterAlerts.lua#L3066-L3103
[S11]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/Reminders.lua#L2644-L2680
[S12]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/TheCoiledAltar.lua#L450-L470
[S13]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/TheTwinFangs.lua#L44-L69
[S14]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/Ulatek.lua#L479-L482
[S15]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/Ulatek.lua#L89-L107
[S16]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/Sszorak.lua#L323-L345
[S17]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/InterruptDisplay.lua#L87-L125
[S18]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS1/FallenKingSalhadaar.lua#L32-L100
[S19]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS1/VaelgorEzzorak.lua#L25-L60
[S20]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/TheCoiledAltar.lua#L180-L208
[S21]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/Ulatek.lua#L600-L660
[S22]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS1/LightblindedVanguard.lua#L16-L22
[S23]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS1/LightblindedVanguard.lua#L96-L135
[S24]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/ReadyCheck.lua#L5-L211
[S25]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/Functions.lua#L253-L260
[S26]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools_UI/UI/Options/General.lua#L150-L163
[S27]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS1/LightblindedVanguard.lua#L177-L216

[S28]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools_UI/UI/EncounterAlerts.lua#L4165-L4173
[S29]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/Ulatek.lua#L22-L41
[S30]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS1/Beloren.lua#L245-L277
[S31]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS1/MidnightFalls.lua#L574-L593
[S32]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/TheTwinFangs.lua#L245-L260
[S33]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/EntombedSentinels.lua#L469-L498
[S34]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/Ulatek.lua#L1075-L1091
[S35]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/Ulatek.lua#L593-L596
[S36]: https://github.com/Reloe/NorthernSkyRaidTools/blob/190084a202cd7359c42616419acf8d9c98cc25f3/NorthernSkyRaidTools/EncounterAlerts/MidnightS2/Ulatek.lua#L1119-L1126
