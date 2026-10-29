![image](https://user-images.githubusercontent.com/6294155/29047328-2bb60b54-7bc3-11e7-8d88-cee5309495ed.png)

Alternative C# frontend for The Sims 1, based off of [FreeSO](http://freeso.org).
(***REQUIRES*** a legitimate copy of The Sims: Complete Collection or Legacy Collection)

> **This is the `strixmcs-labs` fork** of [riperiperi/Simitone](https://github.com/riperiperi/Simitone). It adds a
> native macOS build (Apple Silicon and Intel, Retina), support for The Sims Legacy Collection's files, and
> reverse-engineering notes for the original game's native code. See [What this fork adds](#what-this-fork-adds).
> Upstream releases: [Latest Pre-release](https://github.com/riperiperi/Simitone/releases/latest/) |
> [Windows download](https://github.com/riperiperi/Simitone/releases/latest/download/SimitoneWindows.zip)

![image](https://user-images.githubusercontent.com/6294155/68995217-112b2680-0883-11ea-9f92-1acc839a7ec0.png)

NOTE! Simitone does not yet support everything the original game does: saving on vacation and a few other
important things are missing, and while all objects run, many of them have bugs that can make certain lot types
unplayable. For upstream development progress, see [this issue.](https://github.com/riperiperi/Simitone/issues/8)

# Purpose

On modern operating systems, The Sims has a few nagging issues that make it less than playable. Simitone can be seen
as a tool to improve the playability of your existing installation of The Sims. Here are some features:

- Playable in 3D - using a combination of models generated from the 2D sprites and created by the community.
  - [Download the mesh pack here!](http://forum.freeso.org/forums/3d-remeshing.40/)
- Custom user interface that works at modern resolutions. Working on a more desktop oriented interface.
- Improved graphical performance, support for high resolutions and refresh rates.
- Custom lighting - directional lights with smooth falloffs and shadows using generated 3D meshes.
- *Volcanic*, a program which allows you to examine, modify and create new game objects. (from FreeSO, Windows only)

# What this fork adds

All of this lives on the `re/generic-sims-call` branch, with engine changes in the
[strixmcs-labs/FreeSO](https://github.com/strixmcs-labs/FreeSO/tree/ts1-fame-calls) submodule (branch `ts1-fame-calls`).

### Native macOS build

A new launcher, [`Client/Simitone/Simitone.Mac`](Client/Simitone/Simitone.Mac), runs Simitone directly on macOS with
MonoGame's OpenGL backend. No Wine, Rosetta or Direct3D translation.

- **Retina rendering**: draws at the display's full pixel density with the UI at its normal size
  (`-noretina` turns it off).
- **App bundle**: [`build-app.sh`](Client/Simitone/Simitone.Mac/build-app.sh) produces a self-contained `Simitone.app`.
- **Finds your game** in `~/Documents`, `~/Desktop` or `/Applications` ("The Sims Legacy Collection",
  "The Sims Complete Collection" or "The Sims").
- **Input fixes**: trackpad taps and quick clicks register first time; clicks land where you point on Retina, in live
  mode and buy mode.
- **Image loading**: decodes the game's RLE-compressed BMPs, which MonoGame's own decoder rejects.

Tested on an M3 Max MacBook Pro running macOS 27 with the Legacy Collection.

### The Sims Legacy Collection

- **Saved lots load cleanly**: Legacy Collection saves contain script state from older versions of some objects
  (doors in particular). Objects whose saved state doesn't match the installed code now start fresh instead of
  raising SimAntics exceptions.
- **Object patches are checked**: Simitone's built-in fixes for Sims 1 objects are byte patches against specific
  file versions. A patch whose target doesn't match is now skipped rather than corrupting the object; this keeps the
  Legacy Collection's pet gym intact. The saxophone player, open mic and picnic basket fixes still apply.

### Game logic

- **Fame (Superstar)**: `PromoteFameIfNeeded` and `DemoteFameIfNeeded` are implemented, reading the star requirements
  from the game's own `fame.iff`. Not yet tested in game.
- **Reverse-engineering notes**: [`docs/re/generic-sims-call.md`](docs/re/generic-sims-call.md) describes, in plain
  language, what each of the original game's 44 "Generic Sims Call" native functions does, including the ones
  Simitone doesn't implement yet. [`tools/ghidra`](tools/ghidra) has the headless Ghidra scripts used. No original
  code is reproduced.

# Building

Clone with submodules:

```bash
git clone --recurse-submodules -b re/generic-sims-call https://github.com/strixmcs-labs/Simitone.git
```

### macOS

Requires the .NET SDK (9 or later).

```bash
cd Simitone/Client/Simitone
dotnet build Simitone.Mac/Simitone.Mac.csproj -c Release
cd Simitone.Mac/bin/Release/net9.0 && dotnet Simitone.dll
```

To build `Simitone.app` instead (defaults to this Mac's architecture; pass `x64` for Intel):

```bash
cd Simitone/Client/Simitone
Simitone.Mac/build-app.sh
```

The app is ad-hoc signed, not notarised, so it runs on the Mac that built it; other Macs need a Gatekeeper override.

Launcher options:

| Option | Effect |
|---|---|
| `-path<folder>` | use the game at this folder (also `SIMITONE_TS1_PATH`, or `~/Documents/Simitone/ts1path.txt`) |
| `-fullscreen` / `-windowed` | switch mode; saved for later launches |
| `-noretina` | render at 1x |
| `-3d` | 3D mode |
| `-nosound` | no audio |

Settings, saves and `crash.log` live in `~/Documents/Simitone/`.

### Windows

Open `Client/Simitone/Simitone.sln` in Visual Studio and build `Simitone.Windows`. This fork's changes are designed
to leave Windows behaviour unchanged, but the Windows build hasn't been retested here.

# Why is it called Simitone?

Simitone -> Semitone -> musical term -> C# -> a note

Further questions can be directed at my PR manager, uh, ... burglar cop.
