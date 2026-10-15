# Handoff: Painter Habits — Streak flame (Seri alevi)

## Overview
This package adds a Duolingo-style streak flame to the HabbitTracker Unity project:
1. **Flame icon on the Atölye habit tile.** It replaces the "12 gün seri" text.
2. **Flame icon + progress bar in Galeri → Alışkanlık detayı.** It goes in the "Şu anki seri" stat cell.
3. **Milestone celebration overlay.** It opens when a tick moves the streak into a new flame tier.

The flame has 9 tiers. As the streak grows, the flame gets bigger and changes color. If a scheduled habit is not done yet today, its flame is grey ("pending"). The tick lights it up.

**Tier names are internal only. They never appear in the UI.** The user sees only the flame, the day count, and how many days are left until the next flame.

Everything else in the app is already implemented. Only build what is described here.

## About the Design Files
`Alev Serisi.dc.html` is a **design reference built in HTML**, not production code. It contains:
- **01 Basamaklar:** every tier.
- **02 Simülatör:** a day slider, the "Bugün tamamla" toggle, and a celebration preview.
- **Uygulamada:** a tile chip and a gallery stat cell, both driven by the simulator.

Open it in a browser. `support.js` must sit next to it, and `sprites/` must stay relative.

Follow the project's existing patterns: `UiTheme`, `UiGradient`, `ButtonPressFeedback`, `Easing`, `AttentionPulse`, `HabbitManager` events, and the editor-built UI in `Editor/UI/PainterHabitsBuilder.cs`.

## Fidelity
**High-fidelity.** Colors, sizes, timing and thresholds are final. All values are in pt. The font is **Fredoka**, using the existing TMP asset.

---

## 1. Tier logic (pure — new file)
**New file:** `Assets/Scripts/Habits/StreakFlame.cs`. Keep it pure, like `DailyResetService` and `HabitScheduling`: no MonoBehaviour, and dates are passed in.

| Tier | Internal name | `currentStreak` | Number color | Celebration glow |
|---|---|---|---|---|
| T0 | Sönük | 0 | `#A89888` | — |
| T1 | Kıvılcım | 1–6 | `#F7A12E` | `#8A3A12` |
| T2 | Mum | 7–13 | `#F7A12E` | `#8A3A12` |
| T3 | Kandil | 14–29 | `#F7A12E` | `#9A3D10` |
| T4 | Meşale | 30–49 | `#F58A26` | `#9A2A0E` |
| T5 | Ocak | 50–99 | `#F26A1F` | `#8A1410` |
| T6 | Mavi alev | 100–199 | `#5FB6FF` | `#1F4FAE` |
| T7 | Mor alev | 200–364 | `#B685FF` | `#5A2AA6` |
| T8 | Altın alev | 365+ | `#FFD34D` | `#B07414` |

```csharp
public static class StreakFlame
{
    public const int TierCount = 9;
    public static readonly int[] Thresholds = { 0, 1, 7, 14, 30, 50, 100, 200, 365 };

    public static int Tier(int streak)
    {
        int t = 0;
        for (int i = 0; i < Thresholds.Length; i++) if (streak >= Thresholds[i]) t = i;
        return t;
    }

    // Grey flame: streak alive, habit due today, not ticked yet.
    public static bool IsPending(HabitData h, DateTime now) =>
        h.currentStreak > 0 && HabitScheduling.IsDueOn(h, now) && !h.isCompletedToday;

    // 0..1 toward the next tier; 1 at T8.
    public static float Progress(int streak)
    {
        int t = Tier(streak);
        if (t == TierCount - 1) return 1f;
        return (streak - Thresholds[t]) / (float)(Thresholds[t + 1] - Thresholds[t]);
    }

    // Days left to the next tier; 0 at T8.
    public static int DaysToNext(int streak)
    {
        int t = Tier(streak);
        return t == TierCount - 1 ? 0 : Thresholds[t + 1] - streak;
    }
}
```

States:
- **Lit:** `currentStreak > 0` and not pending → lit sprite. The number uses the tier color.
- **Pending:** `IsPending` → `_Pending` sprite, at half animation speed. The number is `rgba(243,230,204,.62)`.
- **Out:** `currentStreak == 0` → `T0` sprite (it has no pending variant). The number is `rgba(243,230,204,.62)`.
- **Not scheduled today:** the habit is not pending, so it stays lit and the streak is untouched.
- **Missed day:** no extra save field is needed. When `DailyResetService.BreakMissedStreaks` sets the streak to 0, the tier drops to T0 on its own.

**Tests:** add them to `Assets/Tests/HabitUiLogicTests.cs`, or to a new `StreakFlameTests.cs` if that file goes over the 500-line budget.
- Boundary values: 0, 1, 6, 7, 13, 14, 364, 365, 1000.
- `Progress` stays within 0..1 and is 1 at T8.
- `DaysToNext` gives the right count, and 0 at T8.
- `IsPending` covers four cases: due and not done, due and done, not due, streak 0.

---

## 2. Theme + sprite import
**`Assets/Scripts/UI/UiTheme.cs`:** add the following.
```csharp
public Sprite[] flameIcon;          // 9 sheets, T0..T8 (FlameIcon_T{n})
public Sprite[] flameIconPending;   // 9 entries, [0] = null (FlameIcon_T{n}_Pending)
public Sprite[] flameLarge;         // 9 sheets, T0..T8 (Flame_T{n}) — celebration
public Color[]  flameNumber;        // table above, "Number color"
public Color[]  flameGlow;          // table above, "Celebration glow"
public Sprite FlameIconFor(int tier, bool pending) => pending && tier > 0 ? flameIconPending[tier] : flameIcon[tier];
```
Each sheet slices into 4 frames, so the arrays can be either `Sprite[][]` or a small `FlameFrames` ScriptableObject. Pick whichever matches the `moodIcons` pattern.

**`Assets/Editor/UI/PainterHabitsBuilder.cs`:** copy `sprites/flame/*` into `Assets/Sprites/UI/Flame/`, using the `UiIconImporter` pattern: Point filter, uncompressed, no mipmaps, Center pivot. One change from that pattern: the sprite mode is **Multiple** with Grid by Cell Size (**16×16** for `FlameIcon_*`, **32×32** for `Flame_*`), so each sheet gives 4 frames. Then fill the new theme arrays. The colors from the table can be written directly.

**New component:** `Assets/Scripts/UI/Common/FlameSprite.cs`. It goes on a `UnityEngine.UI.Image` and plays the 4 frames in a loop.
- `SetTier(int tier, bool pending, bool large)`
- Frame time: **0.15 s** lit (0.6 s per loop), **0.3 s** pending (1.2 s per loop). Use `Time.unscaledDeltaTime`.
- Restart at frame 0 only when the tier or the pending state changes. Don't restart on every `Bind`.
- Use whole-number scales (1×, 2×, 3×…). The 1.5× icon size in the mocks is fine on 3× devices. If it shimmers, use 32 pt.

---

## 3. Atölye habit tile
**File:** `Assets/Scripts/UI/Workshop/HabitTile.cs`. In the prefab, built by `PainterHabitsBuilder`, the `streak` TMP text becomes the number inside a chip.

Chip, placed below `title`, with a 3 pt gap:
- Horizontal layout: padding 4 left, 9 right, 2 top/bottom, spacing 3, children centered.
- Background: rounded 12 (pill), `rgba(20,10,4,.42)`. Use the existing rounded 9-slice sprite.
- Child 1: `Image` + `FlameSprite`, **24×24**, using `FlameIcon_T{n}`.
- Child 2: the `streak` TMP. The text is **just the number** (`habit.currentStreak.ToString()`), 12 pt semibold, line height 1.

In `Bind` (currently `streak.text = habit.currentStreak + " gün seri";`), replace that line:
```csharp
int tier = StreakFlame.Tier(habit.currentStreak);
bool pending = StreakFlame.IsPending(habit, AppClock.Now);
streak.text = habit.currentStreak.ToString();
streak.color = (pending || habit.currentStreak == 0) ? theme.textMuted : theme.flameNumber[tier];
flame.SetTier(tier, pending, large: false);
```
Add `[SerializeField] private FlameSprite flame;` and wire it in the builder.

The Alışkanlıklar list card (`HabitCard.streakLabel`) is **unchanged**.

---

## 4. Galeri → Alışkanlık detayı ("Şu anki seri" cell)
**File:** `Assets/Scripts/UI/Gallery/GalleryDetailView.cs`, stat cell 0. The other five cells are unchanged.

Cell 0 layout, a vertical column, centered:
1. A row with spacing 4: `Image` + `FlameSprite` **24×24** (icon sheet), then `statValues[0]` (22 pt bold, `#F3E6CC`, unchanged).
2. `statLabels[0]` "Şu anki seri" (unchanged).
3. A progress bar: 70% of the cell width, height 4, radius 2, 4 pt top margin. Track `rgba(20,10,4,.45)`. The fill uses the tier number color, or `#8F7D70` when pending.
4. A caption, 10 pt, `rgba(243,230,204,.5)`: `"Yeni alev: {DaysToNext} gün"`, or `"En büyük alev"` at T8.

All six cells sit in one grid row, so their heights line up. Center the content of cells 1–5 vertically.

In `Bind`, after `statLabels[0].text = "Şu anki seri";`:
```csharp
int tier = StreakFlame.Tier(habit.currentStreak);
bool pending = StreakFlame.IsPending(habit, AppClock.Now);
streakFlame.SetTier(tier, pending, large: false);
streakBar.fillAmount = StreakFlame.Progress(habit.currentStreak);   // Image.Type = Filled, Horizontal
streakBar.color = pending ? theme.flameBarPending : theme.flameNumber[tier];
streakNext.text = tier == StreakFlame.TierCount - 1 ? "En büyük alev" : "Yeni alev: " + StreakFlame.DaysToNext(habit.currentStreak) + " gün";
```
Add the serialized fields `streakFlame`, `streakBar` and `streakNext`, plus `flameBarPending = #8F7D70` on the theme.

---

## 5. Milestone celebration
**Trigger:** `Assets/Scripts/HabbitManager.cs`, in `CompleteHabit`.
1. Read `int before = StreakFlame.Tier(habit.currentStreak);` before `DailyResetService.AdvanceStreak(habit, now);`.
2. Read `after` the same way once it returns.
3. If `after > before`, raise a new event `public event Action<HabitData, int> StreakTierReached;` (habit, new tier).

`UncompleteHabit` / `RollBackStreak` never raise it. In the prototype, undo followed by a re-tick on the same day shows the celebration again. To block that, store `lastCelebratedTier` + date on `HabitData` (default 0) and raise the event only when `after > lastCelebratedTier`.

**View:** new `Assets/Scripts/UI/Workshop/StreakCelebration.cs`, with a full-screen overlay as the last child of the Atölye screen root.
- It covers the Atölye screen *and* the bottom nav, so the nav is hidden. It sits above everything except Ayarlar.
- It subscribes to `StreakTierReached` and opens **0.7 s** after the tick, so the painter's tick reaction plays first.

Layout, centered column, gap 16, padding 24:
- **Background:** a radial gradient centered at (50%, 40%), going from the tier's glow color in the middle to `#2B1B12` at 70%. A generated radial sprite tinted at runtime is fine.
- **Ray ring:** about 260×260, behind the flame. Alternating wedges: 12° filled, 18° empty, filled with the tier number color at 20% alpha. The edge fades out with a radial mask. It rotates once every **14 s** and fades in over 0.6 s.
- **Flame:** `Flame_T{n}` (32×32 sheet) at **5× = 160×160**, lit, animated.
- **Eyebrow:** "YENİ ALEV", 13 pt semibold, `#E2B04A`, letter-spacing .1em.
- **Title:** `"{currentStreak} gün seri!"`, 32 pt bold, `#F3E6CC`.
- **Line:** `"Sonraki alev {Thresholds[n+1]}. günde"`, or at T8 `"En büyük alevine ulaştın"`. 14 pt, `rgba(243,230,204,.75)`.
- **Button:** "Devam". Padding 12/44, radius 16, gold gradient `#E9BD5C → #D39D38`, bottom shadow 0 4 0 `#8A5F1C`. Text 16 pt semibold `#2B1B12`. Add `ButtonPressFeedback.EnsureOn`. It closes the overlay with a 0.2 s fade.

Motion:
| Element | Animation | Duration / delay |
|---|---|---|
| Overlay | alpha 0 → 1 | 0.25 s |
| Flame | scale 0.2 → 1.18 (55%) → 0.94 (75%) → 1, alpha 0 → 1 over the first 55% | 0.7 s, `Easing.OutCubic` on each leg |
| Text block | translateY +14 → 0, alpha 0 → 1 | 0.4 s, 0.35 s delay |
| Button | same as text | 0.4 s, 0.5 s delay |
| Rays | rotate 360° loop, alpha fade-in | 14 s/turn, 0.6 s fade |

If two habits cross a tier at once (rare), queue the events and show them one after another.

---

## Files
```
Alev Serisi.dc.html            design reference (tiers, simulator, celebration, in-app mocks)
support.js                     needed to open the .dc.html
sprites/flame/
  FlameIcon_T0..T8.png         64×16, 4 frames of 16×16 — tile + gallery cell  (USED IN APP)
  FlameIcon_T1..T8_Pending.png 64×16, grey variant                            (USED IN APP)
  Flame_T1..T8.png             128×32, 4 frames of 32×32 — celebration        (USED IN APP)
  Flame_T0.png, Flame_T1..T8_Pending.png   large out/grey — reference page only, optional in Unity
```
The large sprites differ in **size** inside the 32×32 frame, so the flame grows tier by tier. The icons fill their 16×16 frame and differ in **color and number of side tongues**, so they stay readable at small sizes.

## Touch list
| File | Change |
|---|---|
| `Scripts/Habits/StreakFlame.cs` | **new**: tiers, pending, progress |
| `Scripts/UI/Common/FlameSprite.cs` | **new**: 4-frame sprite player |
| `Scripts/UI/Workshop/StreakCelebration.cs` | **new**: milestone overlay |
| `Scripts/UI/UiTheme.cs` | flame sprite arrays + colors |
| `Scripts/HabbitManager.cs` | `StreakTierReached` event in `CompleteHabit` |
| `Scripts/UI/Workshop/HabitTile.cs` | streak text → flame chip |
| `Scripts/UI/Gallery/GalleryDetailView.cs` | stat cell 0: flame + bar + caption |
| `Editor/UI/PainterHabitsBuilder.cs` | import flame sprites, build chip/cell/overlay, fill theme |
| `Tests/…StreakFlameTests.cs` | **new**: tier/progress/pending tests |
