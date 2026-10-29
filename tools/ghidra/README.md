# Ghidra scripts for The Sims 1

Headless helpers used to produce [`docs/re`](../../docs/re). Run them against your own copy of
`sims.exe`; don't commit decompiled output.

## Setup (macOS, Apple Silicon)

1. Install a JDK 25+ (`brew install openjdk@25`) and Ghidra 12.2+.
2. Set `JAVA_HOME_OVERRIDE` in `<ghidra>/support/launch.properties` to the JDK home.
3. Build Ghidra's native decompiler, which the release doesn't ship for arm64 macOS (without it,
   switch tables aren't recovered):
   ```bash
   cd <ghidra>/support/gradle && ./gradlew buildNatives
   ```

## Usage

```bash
H=<ghidra>/support/analyzeHeadless
# import, analyse, and create the functions auto-analysis misses
$H proj sims -import sims.exe -scriptPath tools/ghidra -postScript MakeFunctions.java
$H proj sims -process sims.exe            # second pass to analyse the new functions
# queries (add -noanalysis -readOnly)
$H proj sims -process sims.exe -noanalysis -readOnly -scriptPath tools/ghidra \
   -postScript CaseAddrs.java 005a3b30
```

| Script | Args | Output |
|---|---|---|
| `MakeFunctions.java` | addresses to force | creates functions after int3 padding across `.text` |
| `ListSwitches.java` | out dir, min, max cases | `switches.txt`: functions by switch size |
| `CaseAddrs.java` | function address | entry address of each switch case |
| `StringRefs.java` | out dir, substrings | `stringrefs.txt`: strings and the functions using them |
| `Decompile.java` | out dir, addresses | one `.c` file per function |
| `ReadMem.java` | `addr:count:i32\|i16\|f32` | values in memory |
| `Stats.java` | — | function count and disassembly coverage |
