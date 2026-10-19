# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity 6 (`6000.0.65f1`) 2D mobile habit tracker. Completing a habit reveals one piece of a
pixel-art painting; finishing a painting adds it to that habit's gallery and a new one is
assigned. A separate painter character reacts to overall consistency with a mood that swaps its
animator. URP 2D renderer, new Input System only (`activeInputHandler: 2`), uGUI.

Version control is Plastic SCM (`.plastic/`, `ignore.conf`) — not git.

## Commands

Unity editor: `C:\Program Files\Unity\Hub\Editor\6000.0.65f1\Editor\Unity.exe`

Run all EditMode tests (close the project in the Editor first — Unity holds a single-instance lock):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.0.65f1\Editor\Unity.exe" -runTests -batchmode `
  -projectPath . -testPlatform EditMode -testResults .\Logs\results.xml
```

Run one test: add `-testFilter HabitDataLogicTests.DailyReset_MissedScheduledDay_BreaksStreak`.

**Compile-check without closing the Editor.** Batch mode needs the lock, but a plain `dotnet build`
does not. Copy `HabitTracker.Editor.csproj`, strip its `<Compile Include>`/`<None Include>`/
`<ProjectReference>` lines, and add:

```xml
<ItemGroup>
  <Compile Include="Assets/Scripts/**/*.cs" />
  <Compile Include="Assets/Editor/**/*.cs" />
  <Compile Include="Assets/Tests/**/*.cs" />
  <Reference Include="Unity.InputSystem">
    <HintPath>Library/ScriptAssemblies/Unity.InputSystem.dll</HintPath>
    <Private>False</Private>
  </Reference>
</ItemGroup>
```

Compiles all three assemblies together in ~2s; catches syntax/API errors but **not** asmdef-reference
mistakes (boundaries disappear in one assembly), and cannot run tests.

The root-level copy `CompileCheck.csproj` doesn't define `HABIT_NOTIFICATIONS`, so it only exercises
`ReminderService.cs`'s fallback (`#else`) branch — add the define plus `Unity.Notifications.Unified.dll`
(and `Unity.Notifications.dll` for the Editor-only call in `HabitTrackerMenu.cs`) to a second copy to
check the real branch without reopening the Editor.

In-editor: **Window > General > Test Runner > EditMode**. There are no PlayMode tests. Tests live in
~20 files under `Assets/Tests/`, one per subsystem (habit data/streaks/scheduling, painting assignment,
painting choice, habit editing/resize, UI logic, responsive layout, settings, easing, dev tools, missed
pieces, painting holes, burned-art rendering, streak flame, rotus, archiving, save transfer, native
file bridge, daily reps). The file name matches the subsystem it covers under `Scripts/`; grep
the test file before writing new logic in that area. `Tests/ResponsiveLayoutTests.cs` runs
`LayoutMetrics`/`WorkshopBands`/`BottomNavBands` over a
12-device matrix (phones, tablets, square/landscape windows) — **any change to a layout constant
belongs in that matrix first**, so tablet breakage is caught here instead of device by device.

Editor menu items (`Editor/*.cs`):
- **Build Painting Library From Sprites** — scans `Assets/Sprites/Paintings/*.png`, fixes import
  settings, writes `PaintingDefinition`s, rebuilds `PaintingLibrary.asset`. Re-runnable: preserves
  hand-edited fields (piece count, reveal order, display name) — overwriting `revealOrder` would
  invalidate saved progress. Forces `isReadable = true` (needed by `BurnedPaintingArt.Build` at
  runtime); without a re-run on old imports, missed-piece art silently falls back to a flat cover.
- **Delete Saved Player Data** — clears the PlayerPrefs save.
- **Add Dev Tools** — adds `DevTools` to `HabbitManager`'s GameObject. Run once per scene. See **Dev mode** below.
- **Build UI Assets** — generates/imports every UI sprite sheet (icons, flame tiers, rotus icons,
  studio art, Fredoka font) and fills `Assets/UI/UiTheme.asset`. The flame and rotus sheets must use
  `ISpriteEditorDataProvider` to cut sub-sprites — `TextureImporter.spritesheet` is removed in Unity 6
  and fails silently, importing as one frame; `Validate` methods log an error per sheet that doesn't
  cut cleanly. `theme.moodIcons` is the one field the builder never writes a sprite into — those are
  hand-assigned user art and must survive re-runs.
- **Build Painter Habits UI** — the one-click screen builder: runs the asset step, rebuilds every
  prefab, then rebuilds the scene roots it owns (`Studio`, `Panel_Workshop`, `Panel_Habits`,
  `Panel_Gallery`, `Panel_Settings`, `UI Root`), wiring every `[SerializeField]` through
  `SerializedObject`. Logs a mismatch if the scene doesn't end up with the expected counts of nav
  buttons, settings buttons, rotus UI, archive UI (each a "silent failure" class — missing wiring
  compiles fine and warns nowhere else). **To change how the UI looks, edit `UiTheme.asset` and
  re-run — never hand-edit generated prefabs; they're rebuilt from scratch every run.**

There is no build script, linter, or CI. The `.csproj`/`.slnx` files are Unity-generated; never edit them.

## Assemblies

Three asmdefs:

- `HabitTracker.Runtime` (`Assets/Scripts/`) — uGUI, TMP, Input System, `Unity.Notifications.Unified`
  (only `ReminderService.cs` calls into it).
- `HabitTracker.Editor` (`Assets/Editor/`) — Editor-only; references Runtime + uGUI/TMP explicitly
  (asmdef references aren't transitive) + `Unity.Notifications` (Editor-only settings API).
- `HabitTracker.Tests` (`Assets/Tests/`) — Editor only, `UNITY_INCLUDE_TESTS`, NUnit.

Runtime and Editor both carry `versionDefines`: `{ "com.unity.mobile.notifications", "2.3.0",
"HABIT_NOTIFICATIONS" }` instead of a hard reference — keeps the real/fallback branches of
`ReminderService.cs` separately compilable and testable, keeps the project compiling if the package
isn't resolved, and keeps the package's whole footprint inside that one file.

`Assets/Plugins/` belongs to **no** asmdef — it holds the two native halves of the file picker
(`Android/NativeFileBridge.java`, `iOS/NativeFileBridge.mm`). Neither is compiled by
`dotnet build CompileCheck.csproj`, and neither is compiled by the Editor: the `.java` only at
Android build time (Gradle), the `.mm` only by Xcode. A typo in either is invisible until then.
The C# side's `#if UNITY_ANDROID` / `#if UNITY_IOS` branches are also invisible to CompileCheck
(`UNITY_EDITOR` wins there) — compile-check them with a throwaway one-file csproj that compiles
`Settings/NativeFileBridge.cs` alone against `UnityEngine.dll`/`CoreModule`/`AndroidJNIModule`
with `UNITY_ANDROID`, then `UNITY_IOS`, then neither.

Everything is in the global namespace.

## Architecture

Deliberately layered so the interesting logic is testable without a scene. **Pure static logic takes
time as a parameter and never reads `DateTime.Now`.**

**Habit scheduling & lifecycle** (`Habits/`):
- `HabitScheduling.cs` — date keys (`yyyy-MM-dd`, local time). `IsDueOn` = not archived AND
  active-on-date AND scheduled-on-date. `TryGetPreviousDueDay` walks back capped at `MaxCatchUpDays =
  400`, and is deliberately archive-**blind** (answers "when was this scheduled", used by
  `HabitArchive.Unarchive`'s re-anchor). Empty `frequency` = every day.
- `HabitArchive.cs` — `Archive` just flips `isArchived` (everything else freezes for free since
  `IsDueOn` already excludes it everywhere). `Unarchive` flips it back **and** re-anchors
  `lastCompletedDate` via `TryGetPreviousDueDay`, or the streak breaks on the next rollover anyway.
- `DailyResetService.cs` — rollover, clears today's ticks, breaks missed streaks, advances/rolls back
  streaks. Clock-moved-backwards grants nothing (anti-cheat). `longestStreak` never lowers.
  `BreakMissedStreaks` skips archived habits explicitly (the one streak loop not routed through `IsDueOn`).
- `StreakFlame.cs` — visual tier only, no streak rules. 9 tiers over `Thresholds = {0,1,7,14,30,50,
  100,200,365}`: `Tier`, `Progress`, `DaysToNext`, `IsPending`. Also owns the celebration decision
  (`ShouldCelebrate`/`StampCelebrated`) — see the celebration invariant below. Tier names from the
  design handoff never appear in the UI.
- `MoodService.cs` — `HasAnyDueHabit`, `AreAllDueHabitsCompleted`, `SettleClosedDays` (walks
  `lastLoginDate`→now counting penalties; same anti-cheat line as above).
- `RotusCycle.cs` — per-habit 7-tick earn cycle (`Advance`/`RollBack`, exact inverses, including "0
  means it just wrapped, claw the reward back too"). Deliberately not tied to `WeekStart` — the save
  format stays Monday-indexed regardless of display preference.
- `HabitReps.cs` — daily rep target: how many ticks close a habit's day (`targetPerDay`) and how
  many were tapped today (`todayReps`). `Target` is the one read gate (an old/hand-edited save's 0
  reads as 1), `Advance` returns "this tick closed the day", `ApplyTarget` is the edit rule,
  `Repair` restores the invariant for `RepairHabits`/`SaveTransfer.Sanitize`. Owns no completion
  logic — see the authority invariant below.
- `WeekDays.cs` — the only Monday-first-index ↔ `DayOfWeek` converter, plus Turkish labels, plus
  `MondayIndexForSlot`/`SlotForMondayIndex` for `WeekStart` display reordering (save stays Monday-indexed).
- `HabitCategories.cs` — 12 built-in categories; `Mind`/`Productivity` are legacy (kept as consts,
  excluded from `All`, never shown in the picker but still display correctly for old habits that use
  them — a stored category is never rewritten except via explicit rename/delete, see below).
  `NormalizeCustom` is the one gate a typed category name passes through. `Collect` combines
  built-ins + custom for both the form picker and the settings filter sheet.
- `HabitFilters.cs` — `HabitFilter{Kind, Category}` + `Token`/`TryParse` (the one struct↔string
  converter, same shape as `WeekDays`). `Collect` is the one gate enforcing `MaxSlots = 5` (hard UI
  layout ceiling, not a round number — see the responsive invariant) and always forcing `Kind.All`
  into slot 0. `Matches` excludes archived habits from every filter, `Kind.All` included.
- `HabitForm.cs`, `DailyProgress.cs`, `CanvasSizes.cs`, `DateLabels.cs` — form validation, today's
  done/total (feeds header + easel from one source), canvas-size option list, date-key→label formatting.

**Paintings** (`Paintings/`):
- `PaintingProgress.cs` — next piece, undo, completion, burn. `OpenNextPiece`/`MissPiece` share one
  private selector (`PickNextPiece`) so a tick and a missed day can never pick conflicting pieces.
- `MissedPieceService.cs` — `CountMissedDueDays`/`Settle`/`SettleAll`: burns pieces for missed due
  days, one habit at a time. No separate "already settled" stamp — it only runs inside an actual
  day-rollover, which can't double-fire.
- `HabitGallery.cs` — gallery membership + holes. `Record` adds an id once and snapshots unpainted
  pieces into `galleryHoles`. `RestoreOne` (rotus's only write path) closes the first hole and deletes
  the entry once empty.
- `PaintingAssignment.cs` — which painting: assign/resize/repair. A finished painting does **not** get
  reassigned automatically — see the `awaitingPaintingChoice` invariant below. `Resize` carries opened
  count first, then holes into whatever room is left (painted pixels outrank holes in the clamp).
- `PaintingChoices.cs` / `PaintingChoicePrompt.cs` — choice-sheet candidates (gallery-exclusion, "pool
  exhausted, reopen it"), and the single `IsPending` check shared by tap-routing and the attention pulse.
- `PaintingPieceState.cs` — per-piece Closed/Opened/Missed view. `For`/`ForFinished` split matters —
  see the invariant below; both are read by `HabitCard`, `HabitTile`, `PaintingCoverGrid`, `PaintingView`.
- `PaintingGrid.cs` — 8-column cell↔piece mapping, identity by design (see invariant below).
- `GalleryWorks.cs` — 03 Galeri's habit work list, in-progress first then finished newest-to-oldest,
  skipping the in-progress slot while awaiting a choice.

**Utilities** — `ListLookup.cs` (allocation-free `Contains` for hot paths), `HapticFeedback.cs` (one
entry point, `AppSettings.HapticsOn`-gated, no call sites yet — infrastructure), `AppClock.cs` (the
**one** place `DateTime.Now` is read; shifts whole days for dev-mode time travel, offset 0 in
production — see the invariant below).

**Save** — `Save/HabitSaveSystem.cs` (JSON round-trip, dirty/flush policy over `IHabitStorage`;
`ReplaceWith` resets to empty before overwriting, for import). `Settings/SaveTransfer.cs` (pure
export/import envelope logic: `Build` strips `isPremium` without mutating the source; `TryRead`
catches well-formed-but-foreign JSON; `Sanitize` is `RepairHabits`'s sibling for checks only an
externally-sourced save needs — out-of-range/duplicate piece indices, overflow, future dates, illegal
streak/frequency values).

**UI support** (`UI/`) — `MoodScale.cs` (the only enum-order ↔ on-screen-order converter, same
"one converter" shape as `WeekDays`), `LayoutMetrics.cs` (the one scale rule, see responsive invariant),
`WorkshopBands.cs` (screen 01's vertical composition constants + `Solve`), `FlowLayout.cs`/
`FlowLayoutGroup.cs` (wrap-and-center layout for the gallery wall, uGUI has no flex-wrap),
`BottomNavBands.cs` (nav bar constants + `BarHeight`/`TabWidth`), `GalleryBands.cs` (gallery layout
constants + `ViewerScale`/`ThumbFit`).

**Orchestrator** — `HabbitManager.cs` (misspelling is intentional and load-bearing). The only
MonoBehaviour touching save data; sequences the pure services rather than reimplementing them. Raises
`HabitChanged`, `HabitCompletedToday`, `PaintingFinished`, `DayRolledOver`, `StreakTierReached`,
`HabitListChanged`. `CreateHabit`/`UpdateHabit`/`DeleteHabit` are the write surface. Split into
`partial` siblings by concern, each under its own file for the 500-line budget:
`HabbitManager.Rotus.cs` (`RotusCount`, `UseRotus`, award/rollback mirroring the mood reward pattern —
except a *spent* rotus can't be clawed back), `HabbitManager.Archive.cs` (`SetArchived`, refuses
archived habits in `CompleteHabit`/`UncompleteHabit`), `HabbitManager.Import.cs` (`TryImportSave`),
`Dev/HabbitManager.Dev.cs` (five `internal` doors for dev tools — see **Dev mode** below).

**Painter** — `Painter/PainterManager.cs` (`DontDestroyOnLoad` singleton; `moodPoint` → `MoodTable` →
6 `PainterMoods`; `MaxMoodPoint` derived from the table's length so they can't drift apart).
`Painter/Painter.cs` (scene character; registers with the manager in `Start`, swaps its whole
`RuntimeAnimatorController` per mood; `pendingPainting` defers an animation requested with no painter
in the scene).

**View** — `View/PaintingView.cs` (`PaintPixels` + its own camera/RenderTexture shown via `RawImage`)
+ `PaintingViewPool.cs` (fixed pool, views parked 1000 units apart so cameras never cross-see). `SceneLoader.cs` wraps `LoadScene`.

**Screens** (`Scripts/UI/`) — `ScreenNavigator` (SetActive-based tab switch, sole writer of
`Painter.SetClickEnabled`). `Workshop/WorkshopScreen.cs` (01, only `HabbitManager` subscriber there —
everything else listens to `PainterManager`/`AppSettings`/static mirrors instead, see below).
`Habits/HabitsScreen.cs` (02, habit CRUD, `HabitSheet`, `ArchiveSheet`). `Gallery/GalleryScreen.cs`
(03, its own independent `HabbitManager` subscriber — see **Gallery** below).
`Workshop/PaintingChoiceSheet.cs` (opens when a tile's painting is finished; requires select + confirm,
never ticks the habit itself). `UI/AttentionPulse.cs` drives the breathing cue on a tile awaiting a
choice (via a separate `choiceGlow` overlay, not the static gold frame).

Screen 01's header is two rows: title row (painter name + settings badge + daily counter) and
`Workshop/MoodRow.cs` underneath (one circle per `PainterMoods`, count driven by `MoodScale.Count`;
active state signals on three channels — border, opacity, scale — since color alone isn't enough; subscribes
to `PainterManager`, not `HabbitManager`). Shared pieces: `UiGradient`, `PaintingCoverGrid` (uGUI cover,
no reveal API), `OptionButton`, `GridCellFitter`, `SafeAreaFitter`, `MaxHeightFitter`, `FullBleedFitter`,
`UiRect`. `UI/UiTheme.cs` holds every colour/sprite/font. `UI/WorkshopLayout.cs` owns screen 01's
runtime vertical composition (see responsive invariant below).

**`UI/Navigation/*`** — the bottom tab bar (replaced the old corner-badge chain nav). `BottomNavBar.cs`
measures `Screen.safeArea`+`canvas.scaleFactor` itself and writes only `sizeDelta.y`; reads
`ScreenNavigator.Current` directly plus subscribes to `ScreenChanged`. One **instance per panel**
(three total) — see the panel-ownership invariant below for why a shared instance can't work. All
three panels' corner badge is now the Ayarlar (settings) button, not a back/menu button.

**`UI/Settings/*`** — the sliding full-screen Ayarlar panel. `Settings/AppSettings.cs` is `AppClock`'s
read-only-mirror twin (`WeekStart`, `SoundOn`, `HapticsOn`, `PainterName`, `CustomCategories`,
`FilterTokens` + `Changed` event), written only by `HabbitManager.ApplySettingsChange`; list properties
return copies, never live references. `SettingsScreen.cs` is not a `ScreenNavigator` tab — it's drawn
over all three panels and slides in independently. `Settings/TimeOfDay.cs` is the one `"HH:mm"`
parser/wrap-arithmetic shared by the time field, `ReminderSchedule.cs`, and save-repair.
`ReminderService.cs` schedules a single fixed-id (1001) daily notification, always rescheduled rather
than left to drift — see the reminder invariant below. `SaveExportService.cs` writes an export to both
the clipboard and a rotating 5-slot local backup folder, and exposes that file's full path
(`ExportResult.FilePath`) plus the single-file read half (`TryReadFile`, `ImportStagingPath`).
`Settings/SaveTransfer.cs` is the pure validate/sanitize half for import — see the import invariant
below. `Settings/NativeFileBridge.cs` is the one C# surface over the system file picker (Save-As on
export, Pick on import), with `Plugins/Android/NativeFileBridge.java` and
`Plugins/iOS/NativeFileBridge.mm` behind it — see the native-file invariants below.
`UI/Settings/SettingsScreen.Data.cs` is the partial holding both data flows; the clipboard and
picker paths merge at its one `BeginImport` so validation, summary and confirmation exist once.
`Settings/ReminderSchedule.cs` is the pure half of reminder scheduling (`TryNextFireTime`, always
strictly in the future). `UI/Settings/CategorySettingsSheet.cs` is the free "Kategoriler ve filtreler"
page: toggling a filter writes immediately; renaming a custom category writes immediately; **deleting**
one only raises `DeleteRequested` and lets `SettingsScreen`'s own `ConfirmSheet` confirm and call
`RemoveCustomCategory` — destructive UI belongs to whoever owns the confirmation, not the page that
detects intent.

`UI/Habits/CategoryPickerSheet.cs` — the habit form's category row, select-then-confirm, with an
inline "+ Kategori oluştur" row inside the same sheet rather than stacking a second popup.

**Studio** (`Scripts/Studio/`) — background/painter/easel in **world space** under one root
(`StudioStage`, which defines design-pixel coordinates with origin at the content column's top-left);
UI chrome sits above in a Screen Space - Overlay canvas. `StudioPixelSprite` places a child from
`(left, top, zoom, pixelsPerUnit)`. `EaselCover` is the easel's own fading cover, separate from `PaintPixels`.

**Rendering module** — `Scripts/PaintPixels.cs` (~870 lines, self-contained, knows nothing about
habits/saves — a piece is just an index to open or lock). `PaintingSetup.ForRenderTexture` is the app
factory; saved progress travels inside the setup struct, not a separate post-build call, because
`SetActive(true)` doesn't run `Start` synchronously. `View/PaintPixelsSandbox.cs` is the tap-to-reveal
dev harness, deliberately separate — the app itself never opens pieces by touch.

**Gallery** (`UI/Gallery/*`) — 03 Galeri, three acyclically-owned levels: `GalleryScreen` (level 1,
pooled `GalleryHabitCard` sections, only `HabbitManager` subscriber) → `GalleryDetailView` (level 2,
rect grows to full screen, raises `WorkSelected`/`Closing`) → `GalleryPaintingViewer` (level 3, opened/
closed by `GalleryScreen` off those two events — never talks to `GalleryDetailView` directly, see the
acyclic invariant below). `GalleryWorkFrame` is the shared sprite+cover+hole-badge renderer all three
levels bind through. **Rotus** adds a fourth layer without touching that shape: `RotusStockPill` /
hole badges / holes chip all read a static mirror (`Rotus/RotusState.cs`, same pattern as
`AppSettings`/`AppClock`); tapping an active one raises `RepairRequested` that bubbles up to
`GalleryScreen`, which opens `RotusRestoreSheet` (spends one rotus via `HabbitManager.UseRotus`, plays
a restore flash using `PaintingCoverGrid.CellAnchors`).

**Dev mode** (`Scripts/Dev/` + `Editor/Dev/`) — manual test surface behind one Inspector toggle on
`DevTools`. Three rules:
- **The dev layer owns no rules.** Every command routes through the production path (`CompleteHabit`/
  `UncompleteHabit`, `RefreshDayIfNeeded`, `ApplyMood`, `PaintingProgress.OpenNextPiece`) — never
  writes state directly.
- `Dev/HabbitManager.Dev.cs` is a `partial` reaching five `internal` doors
  (`DevNotifyHabitChanged`/`ListChanged`, `DevStepMood`, `DevJumpMood`, `DevCompleteActivePainting`)
  onto otherwise-private members, so the whole `Dev` folder can be deleted without touching production code.
- **Pure core, thin wrapper.** Each dev tool has a static data-only method plus a manager-facing
  overload (e.g. `DevPaintingTools.SetOpenedPieces(library, habit, count, buffer)` and
  `(manager, habit, count)`) — the pure halves are what `Tests/DevToolsTests.cs` exercises.

`DevTools` carries `[DefaultExecutionOrder(-1000)]` — the clock must shift before `HabbitManager.Awake`
runs `Load()`. With the switch off, every command is refused and the clock resets to real time.
`DevSampleHabits` backdates seeded habits by 60 days so their first tick doesn't reset the streak to 1.

## Invariants that will bite you

**Progress is owned by `HabbitManager`, never a view.** A habit can be bound to two views at once
(list card + detail). The manager picks the piece index once (`PaintingProgress.OpenNextPiece`) and
pushes the same index to every bound view. Never call `PaintPixels.RevealNext()` from app code — in
Random order each view would diverge.

**Renaming a `[SerializeField]` breaks the scene unless you add `[FormerlySerializedAs]`.** Unity
serializes by field name. Live example: `Painter.cs`'s six mood animator controller fields.

**Never call `onClick.AddListener` from Editor code — it isn't saved to the scene.** `UnityEvent`
serializes only `m_PersistentCalls`; a runtime `AddListener` dies on the next domain reload with no
compile error. Editor-time wiring must go through `SerializedObject` (`UiWiring`). `AddListener`
inside *runtime* code (e.g. `HabitCard.Awake`) is fine — the trap is Editor-time calls only.

**A sprite's pivot comes from its import settings, not a typed field.** `StudioPixelSprite.
ResolvePivot()` reads `sprite.pivot / sprite.rect.size`; a serialized field is only a fallback. A
sprite sheet's per-sprite `pivot` in the `.meta` is only honored when `alignment` is 9 (Custom) —
otherwise it's a lie.

**App code reads `AppClock.Now`, never `DateTime.Now`.** Only `HabbitManager`, `WorkshopScreen`, and
`ReminderService.Schedule` produce "now"; everything downstream takes a date parameter. A stray
`DateTime.Now` isn't a compile error — it silently ignores dev-mode time travel. `AppClock` shifts
whole days only (time-of-day untouched, so midnight wake stays real), clamped to `MaxCatchUpDays`.
Shifting the dev clock does **not** retroactively move an already-scheduled OS notification alarm —
that's the platform's own wall-clock repeat once scheduled.

**The reminder notification has a fixed id (1001) and is always rescheduled, never left to drift.**
Every `Schedule` call cancels then reschedules, so toggling/renaming never stacks a second alarm.
`TryNextFireTime`'s fire time is always strictly in the future (equal counts as past — an alarm
scheduled at/before now fires immediately on Android). Rescheduled on every app launch (not just on
setting change) because "Reschedule on Device Restart" is off. Permission is requested only when the
user flips the toggle on, from a coroutine on `HabbitManager` (not `SettingsScreen`, which would die
with the panel on close while the OS dialog is still open).

**The streak celebration fires only from `CompleteHabit`, only on a tier *rise*.**
`lastCelebratedTier` + `lastCelebratedDate` together stop double-firing — storing only the tier would
block it forever on a break-then-recover-to-tier-1 case; the date scopes suppression to that day only.
`UncompleteHabit` never clears the stamp (that's what makes undo-then-retick-same-day silent). The
decision lives in `StreakFlame.ShouldCelebrate`, not the manager.

**Every flame sprite draws against the bottom of its frame and needs a per-tier lift correction**
(`UiTheme.FlameTier.iconLift`/`largeLift`, applied by `FlameSprite.ApplyLift`) or it reads as
misaligned next to the streak number at low tiers. The lifted `Image` must not be a direct child of a
layout group (it would overwrite the lift every pass) — hence the `FlameSlot` → `Image` indirection.
Re-measure the lift fractions if the art is ever redrawn.

**`StreakCelebration` doesn't subscribe to `HabbitManager` — `WorkshopScreen` forwards to it**, for
the same "one `HabbitManager` subscriber per screen" reason `MoodRow`/`NavMoodBadge` listen to
`PainterManager` instead. It queues (`Enqueue`) rather than stacking, stores the habit's numbers (not
the reference — the habit may change before the 0.7s delay plays), and deactivates only once the
**queue** empties (not per item, which would fire `OnDisable` and clear the queue).

**Mood must be settled before the daily reset.** `MoodService.SettleClosedDays` reads
`isCompletedToday`; `DailyResetService.ApplyDailyReset` clears exactly that field. `Load()` and
`RefreshDayIfNeeded()` both call mood first on purpose — swapping the order turns every closed day
into a penalty.

**Mood is applied in `Start`, not `Awake`** — `PainterManager.Instance` is set in its own `Awake` with
no cross-object ordering guarantee. `Load()` stashes the penalty in `pendingMoodSteps`; `Start()` spends it.

**`lastMoodSettledDate` is the double-count guard** — one reward/penalty per day, whichever path gets
there first. `CreateHabit`/`UpdateHabit`/`DeleteHabit`/`SetArchived` all touch mood only through the
existing `TryAwardTodayMood`/`RollBackTodayMood`, never by writing the stamp directly. `SetArchived`'s
unarchive path has one extra condition the others don't need: only roll back the reward if the habit
isn't already ticked today.

**Archiving stops at `IsDueOn`, not `IsActiveOn`, and un-archiving must re-anchor the streak.**
`IsActiveOn` stays a pure date question on purpose — folding archiving in would make
`TryGetPreviousDueDay` archive-aware by accident in some call sites and not others.
`DailyResetService.BreakMissedStreaks` needs its own explicit `isArchived` skip (the one streak loop
not routed through `IsDueOn`); `HabitArchive.Unarchive` must mirror it with the re-anchor, or a
survived-the-archive streak breaks on its own the moment it comes back out. Archiving otherwise
touches nothing about the painting (progress/holes/rotus/pooled view all freeze for free). Accepted
loophole: archiving before ticking a due day retroactively forgives that day — same class of loophole
`frequency`/`endDate` edits already have; don't "fix" it by adding a timestamp nothing else reads.

**`isCompletedToday` is the sole authority on "this habit's day is closed" — the daily rep counter
never becomes a second one.** A habit needing several ticks a day (`targetPerDay > 1`) still closes
its day in exactly one place: `HabbitManager.CompleteHabit` runs `HabitReps.Advance` and only runs
the completion tail (streak + piece + mood + rotus + celebration) when that returns true. Every
downstream subsystem (`MoodService`, `DailyProgress`, `HabitFilters`, `StreakFlame`,
`MissedPieceService`, the gallery) therefore needed no change at all. The invariant that keeps it
honest: `isCompletedToday` ⇒ `todayReps == Target`, and `!isCompletedToday` ⇒ `todayReps < Target`
— enforced by `Advance`/`Reset`/`ApplyTarget`/`Repair`, and by nothing else writing those two fields.
`UncompleteHabit` resets the counter to 0 rather than stepping back one, because its rollback is
atomic; that is also why tapping a finished tile is the one route back to zero. Editing the target
deliberately does **not** close or reopen today (same accepted-loophole class as a `frequency` edit).

**`HabitListChanged` is structure, `HabitChanged` is content.** `DeleteHabit` must never raise
`HabitChanged` with the removed habit — subscribers would look the id up and fail.

**`canvasSize` only changes through `PaintingAssignment`.** Size change = new painting; `Resize`
carries `min(previousOpened, newSize)` across and clamps twice (requested size, then again after
`AssignNext`'s possible fallback). Opened pieces win the clamp before holes get any room. Assigning
the field directly breaks `canvasSize == PieceCount`.

**A finished painting stays on screen until the user picks its replacement —
`HabitData.awaitingPaintingChoice` holds it there.** `TryCompleteActive` sets the flag instead of
reassigning; nothing else resets, so the finished painting keeps rendering at 100%. Only `AssignNext`/
`Assign` (via `ChooseNext` or `TryReleaseHold` on undo) clears it. `CompleteHabit` refuses to open a
piece while set; `UncompleteHabit` must call `TryReleaseHold` before undoing the piece. `Resize` forces
carried progress to 0 while the flag is set — the old progress belongs to a painting already in the gallery.

**A `GalleryWorkFrame`-bound rect is sized from the definition's native sprite, never the sprite
actually bound to the `Image`** — a holed composite is 2× upscaled and would double the apparent size
if used for sizing.

**A finished painting's piece view comes from `PaintingPieceState.ForFinished`, never `.For`.** `For`
reads the *active* painting's live progress fields, meaningless once a painting has left
`activePaintingId` for `habitGallery`/`galleryHoles`.

**`BurnedPaintingCache` holds two differently-keyed dictionaries — don't confuse them.** One keyed by
`habitId` alone (the active painting), one by `habitId + "::" + paintingId` (each finished painting in
the gallery, which can show several at once with independent holes). `Forget(habitId)` clears both.

**03 Galeri's three levels own each other acyclically** (`GalleryScreen` → `GalleryDetailView` →
`GalleryScreen` → `GalleryPaintingViewer`). Wiring the viewer to listen to the detail view directly
would work today but creates a cycle the moment either needs to react to the other's state.

**Rotus only repairs a *finished* painting's holes, never the active painting's `missedPixels`.**
`UseRotus` only ever calls `HabitGallery.RestoreOne`. `RotusCycle.RollBack`'s "claw back the reward if
the counter just wrapped" case must stay reversible on a same-day undo, exactly like
`lastCelebratedTier`/`Date` — but spending is one-way: `RollBackRotus` never goes below zero, because
by the time a tick is undone the rotus it funded may have already repaired a different habit entirely.

**A missed due day burns a piece, permanently.** "Consumed" = opened + missed; every completion check
uses that sum, not just opened. In Sequential mode, pieces are always consumed in index order whether
opened or missed, so the consumed set is always the contiguous prefix
`[0, totalPixelsPainted + missedPixels.Count)` — no separate index list needed, and `PaintingProgress`
owns this arithmetic in exactly one place (`PickNextPiece`) so a tick and a missed day can never
disagree about what's next. Burning only happens inside an actual day-rollover in
`HabbitManager.Load()`/`RefreshDayIfNeeded()`, in the same slot mood settlement occupies — no separate
"already settled" stamp is needed because nothing can double-burn the same gap.

**A missed piece's look is baked into the painting's own pixels, not drawn as a cover** — alpha
blending can't produce `grayscale()`. `BurnedPaintingArt.Build` composites a 2×-upscaled copy with
every missed piece desaturated/hatched; `BurnedPaintingCache` builds it at most once per habit per
change to `missedPixels` (keyed by habit id, invalidated by an index signature). The old flat
`missedColor`/`LockedColor` cover is the fallback only for an unreadable texture or no habit context.
`PaintPixels.PieceBounds` (shared with `TextureRowForPiece`) is what lets the composite and the live
veil agree on where a piece's texel block starts — see the piece-index-to-geometry invariant below.

**One scale rule, and `UI/LayoutMetrics.cs` is the only place it lives.**
`scale = min(Screen.width / 366, Screen.height / 650)` (Unity's `Expand` match mode, reference
366×650). Threshold aspect 0.5631, a hair above 16:9: at or below it, width is picked and the canvas is
exactly 366 wide (byte-identical to the old phone behaviour); above it (tablets, square/landscape),
height is picked, the canvas is ≥650 tall and wider than 366, with the surplus becoming side margins
around a centered 366-unit column. `1 unit == 1 design pixel` holds horizontally everywhere; not
vertically. Three components consume this and must not re-derive it: `CanvasScalerDriver`,
`SafeAreaFitter`, `StudioStage`. The pillarbox margin is the **Main Camera's clear colour**
(`theme.background`) — an Overlay canvas always draws above world space, so a uGUI backdrop can't sit
behind the studio.

`WorkshopLayout.cs`/`WorkshopBands.Solve` distribute screen 01's vertical budget: shortens the studio
band first (clamped to `[MinSceneHeight, SceneHeight]`, never below `MinGridHeight`), then hands the
shortfall to `StudioStage.SetTopShift` — **the studio is shifted up, never scaled** (scaling would
blur pixel art and misalign the painter's feet). Since the bottom nav bar + two-row header both grew
(AltNavigasyon+Ayarlar teslimi), the shift is no longer always 0 — short devices (iPhone SE, every
tablet/square/landscape row clamped to exactly 650) now shift up to `SceneHeight - MinSceneHeight`.
The invariant that matters and is tested (`Header_PainterHeadClearsHeader`) is that the painter's head
never rises above the header's bottom edge by less than `PainterHeaderClearance`, whatever the shift.
`WorkshopBands.HeaderHeight` and `MinSceneHeight` are *derived* (from `TitleBlockHeight`/
`MoodRowGap`/`MoodCircleSize`, and from `HeaderHeight` + clearance respectively) so a header-height
change can't silently leave the clamp floor stale; `MinGridHeight` stays hand-typed on purpose so the
layout test has something non-tautological to check against.

**The bottom nav bar's `BarHeight(safeBottom)` must not add the safe-area inset it's already inside
of.** `UI Root` is already clamped to the safe area, so the design's 26-unit bottom padding and the
OS inset overlap rather than stack: `BarHeight = ContentHeight + max(0, DesignBottomPadding -
safeBottom)`. The bar's background still visually bleeds to the literal screen edge
(`FullBleedFitter.bleedBottom`) — only the *logical* height follows this rule.

**The bottom nav bar is one instance per panel (three total), inserted at a specific point in each
panel's draw order**, because what must occlude it differs per screen (Habits' bottom sheet and
Workshop's choice sheet occlude it; Gallery's full-bleed detail view must not, but its deeper painting
viewer must occlude it again). All three read `ScreenNavigator.Current` directly, so a mid-crossfade
moment showing two overlapping bars is cosmetic, not a bug.

**`ProjectSettings` is portrait-only via `defaultScreenOrientation: 0` — not the `allowedAutorotate*`
flags**, which Unity never reads under a fixed orientation. The settings that actually matter on a
real device: that field, iOS `uIRequiresFullScreen: 1` (iPad multitasking otherwise ignores the lock),
and Android `androidResizeableActivity: 0` (otherwise split-screen/freeform hands the app a landscape
window regardless). Don't flip `androidResizeableActivity` back to 1 without re-reading this.

**Landscape must still not collapse even though it can't ship** — at API 36+, Android ignores the
orientation lock on screens wider than 600dp unless a manifest opt-out exists (this project has none),
so the landscape/square/tablet rows stay in `ResponsiveLayoutTests.cs` on purpose.

**A fitter that writes its own rect must measure something other than its own rect, and the write-back
must be idempotent.** Writing `offsetMin`/`offsetMax`/`sizeDelta` triggers
`OnRectTransformDimensionsChange` synchronously inside the setter — if the measurement reads fields
the component itself just wrote, the recursion never converges (`FullBleedFitter` once did this and
produced a `StackOverflowException` + a 214MB log). It now derives the overflow from the *parent's*
corners, with an `applying` guard cutting the chain at the first step. A second trap:
`RectTransform.offsetMin`'s setter recomputes `sizeDelta` and fires the callback **before** applying
`anchoredPosition` — so a component must write only a single directly-owned field (`sizeDelta.y`,
`cellSize`, never a `UiRect.Stretch*` pair) from inside that callback. `MaxHeightFitter` learned this
the hard way once `HabitSheet`'s close animation started leaving a nonzero `anchoredPosition.y` behind.

**Piece index 0 is the TOP-LEFT piece; indices run left to right, then down.** The one conversion
point is `PaintPixels.PieceBounds`/`TextureRowForPiece` (a `Texture2D`'s row 0 is the bottom row, so
the piece row has to flip) — `WritePiece` and `BurnedPaintingArt.Compose` both call it, so the live
veil and the baked composite can never disagree about geometry. uGUI cell index and piece index are
the same number today (`PaintingGrid.PieceIndexForCell`/`CellIndexForPiece` are identity functions,
kept as the one seam to change if the fill order ever stops being left-to-right-then-down).

**`PaintingViewBinder.OnDisable` must release every view.** The pool holds nine; leaking them starves
it permanently.

**UI day indices are Monday-first (0 = Pt); `DayOfWeek` is Sunday-first.** `WeekDays` is the only converter.

**`HabitFilters.MaxSlots` (5) is the exact diamond count the filter row has been laid out for — not a
round number.** `HabitFilters.Collect` is the one gate enforcing it (every writer of
`filterTokens` routes through it); `CategorySettingsSheet` grays out chips past the cap as a courtesy,
not the enforcement point.

**Deleting or renaming a custom category is the one deliberate exception to "a stored category is
never rewritten."** `RenameCustomCategory` rewrites `categoryGroup` on every habit using the old name;
`RemoveCustomCategory` reassigns them to `HabitCategories.Other` and prunes dangling filter tokens.
Both end in `ApplySettingsChange`, which is what lets `CategorySettingsSheet` refresh itself for free.

**`AppSettingsData` and other fields (`missedPixels`, `galleryHoles`, `lastCelebratedTier/Date`,
`rotusCount`/`rotusProgress`, `isArchived`, `targetPerDay`/`todayReps`) all extended the save
format without bumping `saveVersion`.** An old save has no key for any of them; `JsonUtility` leaves the C# default, which
already reads as "never used this feature" — not a migration. `customCategories`/`filterTokens` are
the one pair where *empty* is also treated as missing (unlike every other list field), because
`Kind.All` must always be showing and an empty filter list is never a state the UI could produce on
its own.

**The ASCII-only Turkish rule is for *comments* only.** User-facing strings (category names, UI
labels, notification text) keep full diacritics. Source files containing them are saved UTF-8 with
BOM; a file whose only Turkish text is in comments stays ASCII, no BOM.

**`HabitData.canvasSize` is a piece count, not a side length.** Grid is fixed 8 columns; 16→8×2,
32→8×4, 64→8×8. `PaintingLibrary.PickNext` may fall back to a different bucket — the caller must
resync `canvasSize` to the returned definition's `PieceCount` or "finished" is never reached.

**Which save field holds progress depends on the painting's `revealOrder`, not the habit.** Sequential
→ `totalPixelsPainted` (int); Random → `randomOpenedPixels` (index list). Flipping `revealOrder` on an
existing definition silently invalidates saved progress. Unresolvable definition defaults to Random
(the index list can represent both; a bare int can't).

**`PaintingDefinition.id` is part of the save format** (galleries and `activePaintingId` store it),
derived from the PNG filename — renaming/moving a source image orphans user data.

**Instant reveal mode fires `Completed` synchronously inside the reveal loop.** `revealingViews`/
`deferredCompletion` defer the reassignment, or views mid-loop would open pieces on the *new* painting.

**Writes are debounced** — `MarkDirtyDeferred()` only sets a flag; `FlushNow()` does the synchronous
I/O. Call `Flush` only at safe points (pause, focus loss, quit, painting finished); no per-tick saves.
Import is the deliberate exception — `TryImportSave` calls `SaveNow()` directly, since a replace this
large must not sit in the debounce window.

**An import must be validated and sanitized *before* `ReplaceWith` touches anything, and the
mood-before-reset ordering must not be reimplemented a second time.** `SaveTransfer.TryRead` catches
well-formed-but-foreign JSON (a stray `"{}"`) that `FromJsonOverwrite`'s own try/catch can't; `Sanitize`
runs next on the still-untouched incoming data. Only after both pass does `TryImportSave` release
existing views, clear `BurnedPaintingCache`, call `ReplaceWith`, then call the same private
`ApplyLoadedSave(now)` that `Load()` itself calls — split out specifically so the mood-settles-first
rule lives in one place. Unlike `Load()`, it must also spend `pendingMoodSteps` immediately and raise
`SettingsChanged`/`RotusChanged`/`HabitListChanged` itself (structure last).

**The native file surface carries file *paths*, never the save JSON.** Export hands the picker the
path of the recovery copy `SaveExportService.Export` already wrote (`ExportResult.FilePath`) and the
native side copies that file to the chosen destination; import copies the chosen file to
`ImportStagingPath()` and returns that local path, which C# reads and feeds to the **existing**
`SaveTransfer.TryRead` → `Sanitize` → `TryImportSave` chain. So a 40–80 KB string never crosses JNI,
there is no second serialize, and none of the Phase 1–6 validation was duplicated. `NativeFileResult.
Path` is a readable local file **only on the import side** — on export it is Android's `content://`
URI, which `File` cannot open; the export flow shows `DisplayName` and never reads it.

**The picker's result arrives by `UnitySendMessage`, so the receiver GameObject must be named
exactly `NativeFileBridge` and must be `DontDestroyOnLoad`.** The name is a hard-coded string in all
three files (C#, `.java`, `.mm`) — change one and the message vanishes **silently**: the picker
closes and nothing happens. `DontDestroyOnLoad` is required because the picker is a foreground
activity/controller: the app takes `OnApplicationPause`, and the Ayarlar panel may be closed before
the callback lands. Same reasoning as `HabbitManager.RequestReminderPermission` running its coroutine
on itself. `UnitySendMessage` is also what keeps the callback on the main thread — an
`AndroidJavaProxy` callback would arrive on a Java thread where no Unity API is legal.
`SettingsScreen`'s two callbacks therefore start with `this == null || !gameObject.activeInHierarchy`
and skip the ConfirmSheet rather than touching a dead panel.

**The native side never sends a Turkish sentence — it sends short ASCII tokens**
(`write`/`read`/`nopicker`) that `NativeFileBridge.TranslateError` turns into user-facing Turkish.
Every user-visible string stays in C# (the project's rule), and the `.java`/`.mm` files never need a
guaranteed source encoding. `TranslateError` passes unknown text through unchanged, because C#'s own
failure paths already produce full sentences.

**Save-As/Pick were chosen over a share sheet precisely to avoid a manifest.**
`ACTION_CREATE_DOCUMENT`/`ACTION_OPEN_DOCUMENT` (API 19+, minSdk is 23) need **no permission and no
`FileProvider`** — the picker supplies the URI — and the Android result is collected by an invisible
runtime-added `android.app.Fragment` (no manifest entry; plain JNI cannot receive
`onActivityResult`). `ACTION_SEND` would have required `FileProvider` + `provider_paths.xml` + a
custom `AndroidManifest.xml`, which this project still does not have. iOS
`UIDocumentPickerViewController` needs neither `UIFileSharingEnabled` nor
`LSSupportsOpeningDocumentsInPlace`. **So this feature changed no `ProjectSettings` field** — and
`androidResizeableActivity: 0` / `defaultScreenOrientation: 0` must stay untouched (see the
orientation invariant). The pick intent uses `*/*` plus `EXTRA_MIME_TYPES` on purpose: many file
managers hide `.json` under a strict `application/json`.

**Cancelling the picker is not an export failure.** The clipboard write and the recovery copy both
happened *before* the picker opened, so the data really did leave the app — the message says
"panoya kopyalandı", not an error. Only `Unavailable`/`Error` fall back to the pre-picker wording,
and `Unavailable` on the *import* side falls through to the clipboard path so a restore route always
exists.

**The `Paintings` layer (index 8) must exist and stay out of the Main Camera culling mask.** Anything
under `Studio` that lands on layer 8 becomes invisible to the Main Camera and visible inside every
painting thumbnail instead — the builder forces that whole subtree to layer 0.

**`PaintingView.OverrideTextureSize` only works before `Awake`.** The pool creates the GameObject
inactive, adds the component, sets the size, then activates it.

## Save format

One PlayerPrefs string key, `habit_save_v1`, holding `JsonUtility.ToJson(PlayerSaveData)`. Load is one
read + `FromJsonOverwrite` into a reused instance. `HabitSaveSystem.RepairHabits()` re-creates list
fields `JsonUtility` leaves null on write — add new `List<>` fields to that repair method.

**Export/import wraps this same `PlayerSaveData` in a `SaveTransferPackage` envelope** — a transport,
not part of the save format. The envelope carries its own `format`/`formatVersion`, independent of
`data.saveVersion`. `SaveTransfer.Build` strips `settings.isPremium` from the exported copy only
(never the source) — a shared backup must never become a free-premium channel once real IAP lands;
`TryImportSave` restores the **local** `isPremium` back onto the incoming data after `Sanitize`.
Import replaces entirely — no merge — and `ReplaceWith` resets to empty first, since `JsonUtility`
omits empty lists on write and would otherwise leave stale habits behind.

Mood lives in the same blob: `painterMoodPoint`, `lastMoodSettledDate`. Settings:
`PlayerSaveData.settings` (`AppSettingsData` — painter name, reminder on/time, sound/haptics,
`WeekStart`, `isPremium`, `customCategories`, `filterTokens`). Per-habit: `lastCelebratedTier`/`Date`,
`missedPixels` (burned indices of the *active* painting), `galleryHoles` (holes on *finished*
paintings), `rotusProgress`, `isArchived`, `targetPerDay`/`todayReps`.
`PlayerSaveData.rotusCount` is global. All added without a `saveVersion` bump — see the
invariant above for why that's safe.

## Scenes

Both scenes are in build settings.

- `Assets/Scenes/SampleScene.unity` — **the app.** `HabitSystem` carries `HabbitManager` + real
  `PaintingLibrary.asset` + `PaintingViewPool`. The builder adds `Studio`, `PainterManager`, `UI Root`.
  Plus Main Camera (layer 8 excluded), Global Light 2D, `SceneLoader`, EventSystem.
- `Assets/Scenes/PaintersScene.unity` — the original painter-only scene. **Left untouched**; the
  builder copies the `Painter` out of it (`Instantiate`d additively, scene closed without saving) so
  the serialized `AnimatorOverrideController` references and hand-tuned `CapsuleCollider2D` survive
  byte-for-byte. Its Main Camera still includes layer 8, so it's not set up for `PaintingView`.

`PainterManager` now also lives in `SampleScene`. Keep mood code null-tolerant on `Instance` anyway —
`PaintersScene` has none, and UI reading it subscribes in both `OnEnable` and `Start` (no cross-object
`Awake` ordering guarantee), reading `MoodPoint` directly rather than waiting for an event.

**Screens that read `HabbitManager` do their first refresh in `Start`, not `OnEnable`** — the manager
loads the save in its own `Awake`; reading `manager.Habits` earlier can throw.

## Conventions

- Code comments are written in Turkish using ASCII only (no diacritics: "aliskanlik", not
  "alışkanlık"). Match this when editing existing files. Comments explain *why*, especially ordering
  and allocation constraints — keep that habit.
- Hot paths avoid allocation and LINQ on purpose: `for` loops over lists, `StringComparer.Ordinal`
  dictionaries, reusable scratch buffers as fields, `in struct` parameters. Do not "simplify" these into LINQ.
- No per-frame polling for day rollover: a single coroutine sleeps until midnight, and cached
  `DateTime` comparison (not string formatting) is the fast path in `RefreshDayIfNeeded`.
- Input goes through `Pointer.current` (covers mouse and touch in one path) with an
  `EventSystem.IsPointerOverGameObject` guard, not `Input.*` and not per-frame raycasts —
  `Painter.cs` uses a single `Collider2D.OverlapPoint` and tracks press/release on the same collider
  so a drag across the character is not a tap.
