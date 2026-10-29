# Generic Sims Call (primitive 1): behaviour notes

Clean-room notes for completing `VMGenericTS1Call.cs`. They describe what the original game does,
in plain language, so the C# can be written from them. No original code is reproduced here.

## Source binary

| | |
|---|---|
| Build | The Sims Legacy Collection (2025), `sims.exe`, PE32 i386 |
| SHA-256 of the copy analysed | `faf7516f509c95788c3078528a806f16049c77b1f5844d195ae3f86ce264cbf0` |
| Tooling | Ghidra 12.2, scripts in [`tools/ghidra`](../../tools/ghidra) |

Note: the copy analysed has a third-party `.anadius` section added to prevent Insert Disc" prompt. Game logic is unaffected, but
addresses should be re-checked against an unmodified executable.

## Locating the handler

- The handler is the function at `0x005a3b30`. It switches on the call number with exactly 44 cases
  (0–43), matching `VMGenericTS1CallMode`.
- Confirmed by three independent matches: call 7 (deprecated `MakeNewNeighbor`) has no case;
  calls 10/11 (Disable/Enable Build Buy) are single calls; call 29 reads the stack object as a Sim
  and reads a suit field.
- Neighbouring code (`0x5c4000`–`0x5dffff`) holds the other primitives, e.g. the tutorial
  primitive at `0x005c47e0`. Ghidra's default analysis misses much of this region; run
  `MakeFunctions.java` first.

### Conventions inside the handler

| Original | Simitone equivalent |
|---|---|
| executing object, short at `+0x3a` | `Thread.TempRegisters[0]` |
| executing object, short at `+0x3c` | `Thread.TempRegisters[1]` (Temp1…Temp7 follow at 2-byte steps) |
| stack frame `+4` | stack object ID |
| stack frame `+8` | per-frame state used by multi-tick calls |
| return 0 / 1 / 2 | `GOTO_FALSE` / `GOTO_TRUE` / `CONTINUE_NEXT_TICK` |
| error 0x1c thrown | object was required to be a Sim and is not |
| error 0x0a thrown | stack object missing |

Person data is an array of shorts: byte offset = 2 × `VMPersonDataVariable` index.
Neighbour records keep person data at `+0x74`; live Sims at `+0x58c`.

## Case entry addresses

| Call | Name | Entry | Simitone status before this note |
|---:|---|---|---|
| 0 | HouseTutorialComplete | 005a3b82 | missing |
| 1 | SwapMyAndStackObjectsSlots | 005a3bbd | done |
| 2 | SetActionIconToStackObject | 005a3f0e | done |
| 3 | PullDownTaxiDialog | 005a3efe | missing |
| 4 | AddToFamily | 005a3f9b | done |
| 5 | CombineAssetsOfFamilyInTemp0 | 005a40af | done |
| 6 | RemoveFromFamily | 005a4019 | done |
| 7 | MakeNewNeighbor (deprecated) | 005a578c (no-op exit) | n/a |
| 8 | FamilyTutorialComplete | 005a3bb5 | not checked |
| 9 | ArchitectureTutorialComplete | 005a3bb9 | not checked |
| 10 | DisableBuildBuy | 005a40e9 | done |
| 11 | EnableBuildBuy | 005a40fa | done |
| 12 | GetDistanceToCameraInTemp0 | 005a410b | not checked |
| 13 | AbortInteractions | 005a4190 | missing |
| 14 | HouseRadioStationEqualsTemp0 | 005a41cd | done |
| 15 | MyRoutingFootprintEqualsTemp0 | 005a41e7 | stub |
| 16 | ChangeNormalOutfit | 005a4254 | missing |
| 17 | ChangeToLotInTemp0 | 005a485f | done |
| 18 | BuildTheDowntownSimAndPlaceObjIDInTemp0 | 005a4905 | done |
| 19 | SpawnDowntownDateOfPersonInTemp0 | 005a499c | done |
| 20 | SpawnTakeBackHomeDataOfPersonInTemp0 | 005a4ad8 | done |
| 21 | SpawnInventorySimDataEffects | 005a4b5a | done |
| 22 | SelectDowntownLot | 005a578c (no-op exit) | TODO → **no-op** |
| 23 | GetDowntownTimeFromSOInventory | 005a4d99 | done |
| 24 | HotDateChangeSuitsPermanentlyCall | 005a4325 | done |
| 25 | SaveSimPersistentData | 005a4ea1 | done |
| 26 | BuildVacationFamilyPutFamilyNumInTemp0 | 005a493e | done |
| 27 | ReturnNumberOfAvaiableVacationLotsInTemp0 | 005a4eea | partial |
| 28 | ReturnZoningTypeOfLotInTemp0 | 005a4f75 | done |
| 29 | SetStackObjectsSuit | 005a4535 | done |
| 30 | GetStackObjectsSuit | 005a4723 | done |
| 31 | CountStackObjectSuits | 005a461c | done |
| 32 | CreatePurchasedPetsNearOwner | 005a4fcd | done |
| 33 | AddToFamilyInTemp0 | 005a52e2 | done |
| 34 | PromoteFameIfNeeded | 005a5372 | implemented (FreeSO `TS1FameProvider`), untested in game |
| 35 | TakeTaxiHook | 005a5483 | stub |
| 36 | DemoteFameIfNeeded | 005a5405 | implemented, untested in game |
| 37 | CancelPieMenu | 005a54e3 | missing |
| 38 | GetTokensFromString | 005a5540 | missing |
| 39 | ChildToAdult | 005a5667 (shared with 40) | missing |
| 40 | PetToAdult | 005a5667 (shared with 39) | missing |
| 41 | HeadFlush | 005a3b92 | missing |
| 42 | MakeTemp0SelectedSim | 005a570c | done |
| 43 | FamilySpellsIntoController | 005a576f | missing |

## Behaviour of the missing calls

### 34 PromoteFameIfNeeded

1. The stack object must be a Sim, otherwise error 0x1c.
2. If the fame system isn't loaded (no Superstar data), return **false**.
3. Let `L` = the Sim's `TS1FameStarPower` (81). If `L` ≥ 10, return **false**.
4. Promotion needs all three of:
   - **Fame score:** `TS1FameScore` (80) ≥ `FameScoreNeeded[L]`.
   - **Skills:** for each of Cooking (10), Mechanical (12), Charisma (11), Body (17),
     Logic (18), Creativity (15): `clamp(skill / 100, 0, 10)` ≥ `SkillNeeded[skill][L]`.
     A requirement already met in raw points (`needed × 100 ≤ skill`) counts as 0.
   - **Famous friends:** `0.5 × FriendStarPower` ≥ `FriendStarsNeeded[L]`, where
     `FriendStarPower` is the sum of `TS1FameStarPower` over every neighbour who
     - is not in the Sim's family (`TS1FamilyNumber`, 61, differs and is non-zero),
     - has a relationship entry with the Sim *and* the Sim has one with them, and
     - has daily relationship ≥ 25 towards the Sim, and the Sim towards them (the 25 is a global
       whose static default is 25; it may be overwritten by a tuning constant at load).
5. On success: `StarPower = L + 1`; if `TS1FameStarHighWatermark` (82) < `L + 1`, raise it to
   `L + 1`. Return **true**. Otherwise return **false**.
6. Either way, the Sim's state is refreshed afterwards (UI/motive update).

### 36 DemoteFameIfNeeded

1. Stack object must be a Sim (error 0x1c), fame system must be loaded (else **false**).
2. Let `L` = `StarPower`. If `L` > 0 and `FameScore` < `FameScoreNeeded[L − 1]`, set
   `StarPower = L − 1` and return **true**. Otherwise **false**.

### Fame tables (all read from the user's `fame.iff`)

| STR# in fame.iff | Contents (11 strings, one per star level 0–10) |
|---:|---|
| 5 | `FameScoreNeeded[L]`, an integer |
| 4 | `FriendStarsNeeded[L]`, an integer |
| 6 | six integers `"cooking,mechanical,charisma,body,logic,creativity"` = `SkillNeeded[*][L]` |
| 1 | star titles (used by the UI) |

Values in the Legacy Collection's `fame.iff`, for reference:

| Star level | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Fame score | 2 | 9 | 21 | 35 | 52 | 78 | 156 | 300 | 550 | 900 | 0 |
| Friend stars | 0 | 0 | 0 | 0 | 2 | 4 | 7 | 11 | 14 | 18 | 0 |
| Charisma | 0 | 1 | 2 | 3 | 4 | 6 | 6 | 7 | 8 | 10 | 0 |
| Body | 0 | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 0 |
| Creativity | 0 | 1 | 2 | 3 | 4 | 4 | 6 | 7 | 8 | 9 | 0 |

Cooking, Mechanical and Logic are 0 at every level.

One check in the friend loop (`0x00548570`, called per neighbour with no visible arguments) is not
yet identified; it likely filters out invalid or deleted neighbours.

### 35 TakeTaxiHook

The executing object must be a Sim whose object type field equals 2 (otherwise error 0x1c).
Records that Sim as the "taxi passenger" on the global UI controller. No VM state changes.

### 0 HouseTutorialComplete

Calls the tutorial controller with argument 0 (marks the house tutorial finished). UI only.

### 3 PullDownTaxiDialog

Opens the taxi destination dialog. UI only; returns **true**.

### 13 AbortInteractions

Requires a valid stack object (error 0x0a otherwise). Aborts every interaction associated with that
object, then returns **true**.

### 15 MyRoutingFootprintEqualsTemp0

The executing object must be a Sim. Stores Temp0 as the Sim's footprint mode and clears a companion
field. If Temp0 is 0, the Sim's routing bounds are reset to the default. Returns **true**.

### 16 ChangeNormalOutfit

The executing object must be a Sim. Counts the Sim's normal (body) outfits; if there's at least
one, switches to the next one, wrapping round (`(current + 1) % count`). Returns **true** if an
outfit exists, otherwise **false**.

### 22 SelectDowntownLot

No-op in this build (jumps straight to the shared exit). Return **true**.

### 27 ReturnNumberOfAvaiableVacationLotsInTemp0

Start from 9. For each vacation lot number 40–48, if any family in the neighbourhood currently lives
on (is saved at) that lot, subtract 1. Temp0 = the result. Returns **true**.

### 37 CancelPieMenu

Validates that the stack object is a Sim (error 0x1c otherwise) and otherwise does nothing in the
VM. Returns **true**. (The pie menu is UI-side.)

### 38 GetTokensFromString

- Temp0 = STR# resource ID in the calling object's own file; Temp1 = 0-based string index.
- If Temp0 is 0: Temp0 = 0, return **true**.
- If the string isn't found: Temp0 = −1, return **false**.
- Otherwise: parse up to 7 unsigned decimal integers from the string (any non-digit is a
  separator), store them in Temp1…Temp7, set Temp0 = the number found, return **true**.

### 39 ChildToAdult / 40 PetToAdult

Multi-tick. On the first tick, post a "grow up" request to the UI with the executing object's ID,
Temp0, and a mode (0 = child, 1 = pet), mark the frame as waiting, and return
`CONTINUE_NEXT_TICK`. On later ticks, poll the request; while it's pending keep returning
`CONTINUE_NEXT_TICK`. When it completes, set Temp0 from the result and set Temp1 from the result's
second field if the result status is 2, otherwise Temp1 = 0. Return **true**.

### 41 HeadFlush

No-op. Return **true**.

### 43 FamilySpellsIntoController

If the Magic Town spellbook UI exists, tell it to refresh from the family's spells. UI only;
return **true**.

## Reproducing

See [`tools/ghidra/README.md`](../../tools/ghidra/README.md).
