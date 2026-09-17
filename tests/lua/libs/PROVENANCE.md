# Vendored libraries

The harness runs the real libraries, not stand-ins, so a change in how LibSharedMedia keys or
callbacks behave shows up here. They are vendored rather than read from a WoW install so the
harness runs on a CI runner.

All three were copied verbatim (only CRLF line endings normalised to LF) from the `libs/` folder of the CurseForge release of
[SharedMedia_Causese](https://www.curseforge.com/wow/addons/sharedmedia-causese), which is the
same bundle WoWVoxPacks ships alongside. Upstream for all three is [WoWAce](https://www.wowace.com). LibSharedMedia-3.0 is exported from
`https://repos.wowace.com/wow/libsharedmedia-3-0/tags/v12.1.0/LibSharedMedia-3.0/LibSharedMedia-3.0.lua`.

| Path | Version | Licence |
| --- | --- | --- |
| `LibStub/LibStub.lua` | LibStub r2 (`$Id: LibStub.lua 103 2014-10-16 03:02:50Z mikk $`) | Public Domain, per the file header |
| `CallbackHandler-1.0/CallbackHandler-1.0.lua` | CallbackHandler-1.0 r6 (`$Id: CallbackHandler-1.0.lua 18 2014-10-16 02:52:20Z mikk $`) | Ships with Ace3 under its permissive Ace3 licence |
| `LibSharedMedia-3.0/LibSharedMedia-3.0.lua` | LibSharedMedia-3.0 12.0.0 v2 (tag v12.1.0, revision 177), by Elkano and funkehdude | LGPL v2.1, per the file header |

To refresh one, replace the file with the newer upstream copy and update the row above. Do not
edit these files: the point is that they behave exactly as they do in the game.

`../support/nsrt.lua` vendors an excerpt of Northern Sky Raid Tools rather than the whole addon;
its own header records the version and source files.
