# Missing optional NSRT sounds

Date: 2026-10-20. Implemented against WoWVoxPack `d20204444` and NSRT upstream commit
[`190084a202cd7359c42616419acf8d9c98cc25f3`](https://github.com/Reloe/NorthernSkyRaidTools/tree/190084a202cd7359c42616419acf8d9c98cc25f3).
The installed NSRT version was 12.1.32; its missing literal phrases matched the pinned upstream snapshot.

## Scope and evidence

Added 53 exact optional TTS inputs: 44 distinct phrases from editable alerts, the special `Drop-Pool` reminder, and eight Ula'tek `Soak {rtN}` marker phrases. The prior Left/Right wave aliases remain unchanged.
The investigation executed every encounter alert initializer in an isolated Lua harness, capturing 197 registration calls, of which 121 declared TTS false or nil. That captured reassigned alert text as well as table literals. Runtime/special paths and locked settings were then inspected in source. See the [bounded special-path audit](2026-10-20-nsrt-optional-tts-special-paths.md) for the latter and its pinned sources.

The five configured NSRT packs each now register 483 media keys. They share the same new input-to-audio mappings. The source entries live in [nsrt-extra-vocabulary.txt](../../nsrt-extra-vocabulary.txt), because the existing automatic alert extractor skips TTS disabled by default; pronunciation and filename aliases live in [CalloutPronunciations.json](../../src/WoWVoxPack.AddOns.Callouts/CalloutPronunciations.json).

## Local generation

- Dry run: exactly 44 entirely new recordings per voice, no existing audio re-renders, no removals, and unchanged voice recipes.
- Generated locally with the existing NSRT builder, pronunciation resolver, Google TTS provider, sound service, and orchestrator; only NSRT was registered in the temporary runner.
- Voices: Neural2_C, Wavenet_E, Standard_D, Studio_Q, Studio_O.
- Exactly 220 synthesis calls completed, producing 220 new Ogg Vorbis files.
- All 220 files decoded successfully; each is 44100 Hz, with durations from 0.40 to 1.45 seconds.
- Every one of the 53 new keys resolves to an existing file in every configured pack. All earlier media-key mappings are preserved, and no earlier Ogg file changed.
- Unit suite: 185 passed; Lua harness: 19 passed; luacheck: zero warnings/errors in 14 files.
- No live-client execution or installation was performed. Arbitrary custom TTS text and non-English localization are outside this finite default-English inventory.

## All added inputs

“Existing” reuses audio already in the pack. “Shared new” reuses a recording generated for another new input. Every other row has one new recording per voice.

| NSRT input | Spoken text | Audio file | Generation |
| --- | --- | --- | --- |
| `Add Soak` | Add Soak | `add_soak.ogg` | New |
| `AoE` | AOE | `aoe.ogg` | New |
| `Aura of Wrath` | Aura of Wrath | `aura_of_wrath.ogg` | New |
| `Blink Nova` | Blink Nova | `blink_nova.ogg` | New |
| `Blood-Soak` | Blood Soak | `blood_soak.ogg` | New |
| `Bomb inc` | Bomb incoming | `bomb_inc.ogg` | New |
| `Boss Spawn` | Boss Spawn | `boss_spawn.ogg` | New |
| `Damage Amp` | Damage Amp | `damage_amp.ogg` | New |
| `Defensives` | Defensives | `defensives.ogg` | New |
| `Devotion Aura` | Devotion Aura | `devotion_aura.ogg` | New |
| `Dispels` | Dispels | `dispels.ogg` | New |
| `Dmg amp` | Damage Amp | `damage_amp.ogg` | Shared new |
| `Dmg amp in` | Damage Amp in | `damage_amp_in.ogg` | New |
| `Drag out` | Drag out | `drag_out.ogg` | New |
| `Drop-Pool` | Drop Pool | `drop_pool.ogg` | Existing |
| `Egg Deadline` | Egg Deadline | `egg_deadline.ogg` | New |
| `Eggs` | Eggs | `eggs.ogg` | New |
| `First Break` | First Break | `first_break.ogg` | New |
| `First Soak` | First Soak | `first_soak.ogg` | New |
| `Fish Spawn` | Fish Spawn | `fish_spawn.ogg` | New |
| `Frostfire Debuffs` | Frostfire Debuffs | `frostfire_debuffs.ogg` | New |
| `Get Orb` | Get Orb | `get_orb.ogg` | New |
| `Ghosts` | Ghosts | `ghosts.ogg` | New |
| `Infection` | Infection | `infection.ogg` | New |
| `Jump` | Jump | `jump.ogg` | New |
| `Mind Controls` | Mind Controls | `mind_controls.ogg` | New |
| `Next Hit` | Next Hit | `next_hit.ogg` | New |
| `Orb deadline` | Orb deadline | `orb_deadline.ogg` | New |
| `Peace Aura` | Peace Aura | `peace_aura.ogg` | New |
| `PICK UP` | Pick up | `pick_up.ogg` | New |
| `Platform Break` | Platform Break | `platform_break.ogg` | New |
| `Poison Add` | Poison Add | `poison_add.ogg` | New |
| `Second Break` | Second Break | `second_break.ogg` | New |
| `Second Soak` | Second Soak | `second_soak.ogg` | New |
| `Seed-Drop` | Seed Drop | `seed_drop.ogg` | Existing |
| `Set Markers` | Set Markers | `set_markers.ogg` | New |
| `Shield` | Shield | `shield.ogg` | New |
| `Soak {rt1}` | Soak Star | `soak_star.ogg` | Existing |
| `Soak {rt2}` | Soak Circle | `soak_circle.ogg` | Existing |
| `Soak {rt3}` | Soak Diamond | `soak_diamond.ogg` | New |
| `Soak {rt4}` | Soak Triangle | `soak_triangle.ogg` | Existing |
| `Soak {rt5}` | Soak Moon | `soak_moon.ogg` | New |
| `Soak {rt6}` | Soak Square | `soak_square.ogg` | Existing |
| `Soak {rt7}` | Soak Cross | `soak_cross.ogg` | Existing |
| `Soak {rt8}` | Soak Skull | `soak_skull.ogg` | Existing |
| `Soak-Time` | Soak Time | `soak_time.ogg` | New |
| `SWAPPED` | Swapped | `swapped.ogg` | New |
| `Sweep` | Sweep | `sweep.ogg` | New |
| `Tank Combo` | Tank Combo | `tank_combo.ogg` | New |
| `Tank-Hit` | Tank Hit | `tank_hit.ogg` | New |
| `Time to Throw` | Time to Throw | `time_to_throw.ogg` | New |
| `Wind-Debuffs` | Wind Debuffs | `wind_debuffs.ogg` | New |
| `Your Interrupt` | Your Interrupt | `your_interrupt.ogg` | New |

## Newly recorded phrases

| Spoken phrase | Audio file |
| --- | --- |
| Add Soak | `add_soak.ogg` |
| AOE | `aoe.ogg` |
| Aura of Wrath | `aura_of_wrath.ogg` |
| Blink Nova | `blink_nova.ogg` |
| Blood Soak | `blood_soak.ogg` |
| Bomb incoming | `bomb_inc.ogg` |
| Boss Spawn | `boss_spawn.ogg` |
| Damage Amp | `damage_amp.ogg` |
| Damage Amp in | `damage_amp_in.ogg` |
| Defensives | `defensives.ogg` |
| Devotion Aura | `devotion_aura.ogg` |
| Dispels | `dispels.ogg` |
| Drag out | `drag_out.ogg` |
| Egg Deadline | `egg_deadline.ogg` |
| Eggs | `eggs.ogg` |
| First Break | `first_break.ogg` |
| First Soak | `first_soak.ogg` |
| Fish Spawn | `fish_spawn.ogg` |
| Frostfire Debuffs | `frostfire_debuffs.ogg` |
| Get Orb | `get_orb.ogg` |
| Ghosts | `ghosts.ogg` |
| Infection | `infection.ogg` |
| Jump | `jump.ogg` |
| Mind Controls | `mind_controls.ogg` |
| Next Hit | `next_hit.ogg` |
| Orb deadline | `orb_deadline.ogg` |
| Peace Aura | `peace_aura.ogg` |
| Pick up | `pick_up.ogg` |
| Platform Break | `platform_break.ogg` |
| Poison Add | `poison_add.ogg` |
| Second Break | `second_break.ogg` |
| Second Soak | `second_soak.ogg` |
| Set Markers | `set_markers.ogg` |
| Shield | `shield.ogg` |
| Soak Diamond | `soak_diamond.ogg` |
| Soak Moon | `soak_moon.ogg` |
| Soak Time | `soak_time.ogg` |
| Swapped | `swapped.ogg` |
| Sweep | `sweep.ogg` |
| Tank Combo | `tank_combo.ogg` |
| Tank Hit | `tank_hit.ogg` |
| Time to Throw | `time_to_throw.ogg` |
| Wind Debuffs | `wind_debuffs.ogg` |
| Your Interrupt | `your_interrupt.ogg` |
