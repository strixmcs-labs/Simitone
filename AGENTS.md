# AGENTS.md: handoff for continuing this work

Context for an AI agent (or person) picking up the macOS / Legacy Collection work on this fork.

## Goal

Run **The Sims 1 Legacy Collection** natively on an Apple Silicon Mac (M3 Max, macOS 27) through
Simitone, a C#/MonoGame reimplementation of the game engine that loads the original game files.
No Wine, no Rosetta.

## Repositories and branches

| Repo | Branch | Notes |
|---|---|---|
| `strixmcs-labs/Simitone` (this repo) | `re/generic-sims-call` | all work; based on upstream `riperiperi/Simitone` |
| `strixmcs-labs/FreeSO` | `ts1-fame-calls` | submodule at `FreeSO/`; `.gitmodules` points here |

- Commit as `Dr. Matt Booth <matt@strixmcs.com>` (pass `-c user.name=... -c user.email=...`; the machine
  has no global git identity). Push with `git -c credential.helper= -c 'credential.helper=!gh auth git-credential' push ...`
  (gh is logged in as `thatosxguy`, admin of `strixmcs-labs`).
- Commit the FreeSO submodule first, push it, then commit the new pointer in Simitone.
- Ask the user before committing or pushing.

## Machine layout

- Game: `~/Documents/The Sims Legacy Collection/` (a repack with an `anadius` crack; the user owns the game).
- Simitone user data / saves: `~/Documents/Simitone/` (`config.ini`, `UserData/`, `crash.log`, `input.log`).
- Reverse-engineering workspace: `~/Games/SimsRE/` (Ghidra 12.2 with natives built for arm64, JDK 25 at
  `/opt/homebrew/opt/openjdk@25`, analysed project of `sims.exe`, scripts in `scripts/`).
- .NET 10 SDK only (projects target net9.0 and roll forward).
- **This checkout lives in a temporary session folder** that is deleted with the Claude session. Push
  before relying on it.

## Build and run

```bash
cd Client/Simitone
dotnet build Simitone.Mac/Simitone.Mac.csproj -c Release      # don't pass -r: NU1605 errors from server libs
cd Simitone.Mac/bin/Release/net9.0 && dotnet Simitone.dll    # finds the game automatically
Simitone.Mac/build-app.sh                                     # -> Simitone.Mac/dist/Simitone.app (self-contained, ad-hoc signed)
```

Launcher switches: `-path<dir>`, `-fullscreen`, `-windowed`, `-noretina`, `-3d`, `-nosound`.
Diagnostics (env vars): `SIMITONE_DISPLAY_DIAG=1` (sizes, then quit), `=2` (programmatic resize/move),
`=3` (input log to `~/Documents/Simitone/input.log`), `SIMITONE_PICK_DIAG=1` (object picking log, see below).
Suppressed SimAntics exceptions now print full stack traces to stderr.

## Done (all pushed unless noted)

1. **Reverse engineering** (`docs/re/generic-sims-call.md`, `tools/ghidra/`): Generic Sims Call handler in
   `sims.exe` is `0x005a3b30` (44-case switch). All missing calls documented in plain language (clean room,
   no EA code). Fame promotion rules and `fame.iff` tables fully described.
2. **Fame calls 34/36** implemented (`FreeSO: tso.content/TS1/TS1FameProvider.cs`, `VMGenericTS1Call.cs`).
   Loader verified against the real `fame.iff`; **not yet tested in game**.
3. **Native Mac launcher** `Client/Simitone/Simitone.Mac/`:
   - `MacGameLocator` (finds the game), `MacImageReader` (BMP incl. RLE4/RLE8, StbImageSharp otherwise),
   - `MacMouseLatch` (SDL event watch so taps/fast clicks register),
   - `MacRetina` + `MacSdl` (Retina: enables `wantsBestResolutionOpenGLSurface` on SDL's NSWindow via the
     ObjC runtime, reports pixel size from `backingScaleFactor`, scales mouse to pixels).
   - `SimitoneGame.PixelSizeProvider` / `ApplyPixelBackBuffer()` keep back buffer + viewport in pixels and set
     `FSOEnvironment.DPIScaleFactor` (2 on Retina); runs at start, on resize, and every frame.
4. **Unit fixes for DPIScaleFactor > 1**: FreeSO `UILayer.MouseOverWindow`; Simitone `UIObjectHolder`,
   `UILotControl` (pie-menu tile, cutaway room), `TS1GameScreen` camera centre. Placement confirmed working.
5. **Stale saved stacks** (FreeSO `VMTS1ActivatorNew.cs`): Legacy Collection saves contain door script state
   from an older `Doors.iff` (main frame at node 1 calling global 259; installed main is 1 instruction calling
   global 280 "idle"). Frames not matching installed code -> object starts fresh. Confirmed: door/lamp/phone
   SimAntics errors gone ("started 16 object(s) fresh").

## Units convention (important)

- Mouse positions and world picking (`World.EstTileAtPosWithScroll`, `GetObjectIDAtScreenPos`) use
  **back buffer pixels**.
- UI layout (`GlobalSettings.GraphicsWidth/Height`, `UIScreen.ScreenWidth/Height`, UI element positions) uses
  **UI units = points**. Multiply by `FSOEnvironment.DPIScaleFactor` to get pixels.
- At scale 1 (Windows) these coincide, so bugs only show on Retina.

6. **Live-mode clicks** (Simitone `UILotControlTouchHelper.cs`): live mode opens pie menus through a tap/drag
   helper that converted the tap point with the screen middle in UI units while the tap was in pixels, so
   `ShowPieMenu` looked in the wrong place and no action registered. Now uses `PixelScreenMiddle()`; the tap/drag
   threshold also scales with DPI. **Confirmed working** by the user (pie menus and actions).
   - Diagnostics: `SIMITONE_PICK_DIAG=1` logs each object pick (FreeSO `WorldPlatform2D.GetObjectIDAtScreenPos`)
     and live-mode state once a second (`UILotControl.LiveModeUpdate`). Picking itself was fine (object IDs come
     back correctly; `id=0` is open ground).
   - Note: a buy-mode report of "clicked the phone, the counter got selected" came from an earlier build and
     was not reproduced after this fix; re-check if it comes back.

7. **Object patches vs the Legacy Collection** (FreeSO `IffFile.Patch`, `PIFF.ExpectedSourceSize`): in TS1 mode
   Simitone loads objects only from the game install (FreeSO's `Content/Objects` and `Content/Patch` are TSO-only)
   and applies the 4 patches in `Content/TS1Patch`. Checked each against the Legacy files: sax player (EP5),
   open mic (EP6) and picnic basket (EP4) fit exactly and decode cleanly; **pet gym (EP5, BHAV 4100) does not**
   (patch expects 107 bytes, Legacy has 96) and applying it silently corrupted the routine. Patches whose target
   chunk isn't the expected size are now skipped with a log line, so the pet gym keeps its original routine.
   Re-authoring the pet gym performance fix for the Legacy encoding is a possible follow-up.
   `Content/MeshReplace` (3D meshes for 4 objects, `-3d` mode only) has no Legacy counterpart.

(Everything above is committed as of FreeSO 1e64885b.)

## Later

- Test fame promotion in game (Studio Town).
- Implement remaining Generic Sims Calls from `docs/re/generic-sims-call.md` (38, 27, 16, 13, 15, 39/40, no-ops).
- Verify RE addresses against an unmodified `sims.exe`; identify `0x00548570` (friend-loop check).
- Upstream PRs to riperiperi (point the submodule back at upstream FreeSO first). The stale-stack fix helps
  Windows Legacy Collection players too.
