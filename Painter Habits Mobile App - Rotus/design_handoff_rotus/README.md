# Handoff: Painter Habits — Rötuş (burned-piece restore)

## Overview
Rötuş is a consumable item, similar to Duolingo's streak freeze. **One rötuş restores one burned piece** of a finished gallery painting. This package covers the **visuals only**. How rötuş is earned or bought is out of scope; just expose a count.

1. **Stock counter:** a pill in the Galeri header, mirrored opposite the settings/back button.
2. **Glow on burned-piece UI:** the hole badge on gallery work tiles and the "X parça yandı" chip in the painting viewer glow while `rotus > 0`. Both become tappable.
3. **Restore sheet:** a bottom sheet that shows the painting with its burned pieces highlighted, plus a "Bir parçayı onar" button.

## About the Design Files
`Rotus.dc.html` is a **design reference built in HTML**, not production code. Open it in a browser; it needs `support.js` and `sprites/` next to it. It has four sections:
- **01 Simge:** the icon states.
- **02 Sayaç:** the header pill. Tapping it adds stock, for preview only.
- **03 Tetik:** the glowing badge and chip.
- **04 Onarım:** the working restore sheet with its flash.

Follow the project's patterns: `UiTheme`, `ButtonPressFeedback`, `Easing`, `HabbitManager` events, `PainterHabitsBuilder`.

## Fidelity
**High-fidelity.** Colors, sizes and timing are final. Values are in pt. The font is Fredoka, using the existing TMP asset.

---

## Sprites — `sprites/rotus/`
| File | Size | Use |
|---|---|---|
| `Rotus.png` | 128×32, **4 frames of 32×32** | stock > 0, looping sparkle |
| `Rotus_Static.png` | 32×32 | optional, non-animated places |
| `Rotus_Empty.png` | 32×32 | stock = 0 (greyscale, no animation) |

The icon is a Renaissance albarello (apothecary jar). It has an ultramarine paint band and a gold trim, and a brush sits inside. It is ultramarine because that was the most precious Renaissance pigment.

**Import:** copy the files to `Assets/Sprites/UI/Rotus/` using the `UiIconImporter` pattern: Point filter, no compression, no mipmaps, Center pivot. For `Rotus.png`, use Sprite Mode **Multiple**, Grid by Cell Size 32×32.

**Animation:** **0.4 s per frame** (1.6 s per loop), unscaled time. Reuse `FlameSprite` from the streak-flame package, or a generic `SpriteFrameLoop`.

Display sizes: **32 pt** in the header pill and **64 pt** in the sheet. Use whole-number scales only.

---

## 1. Data
**`Assets/Scripts/Data/PlayerSaveData.cs`**, on `PlayerSaveData` (global, not per habit):
```csharp
public int rotusCount;   // missing in old saves → 0; no saveVersion bump (same rule as galleryHoles)
```
**`Assets/Scripts/Paintings/HabitGallery.cs`:** add a pure method.
```csharp
// Removes the first hole of a finished painting. Returns the restored piece index or -1.
public static int RestoreOne(HabitData habit, string paintingId)
```
Remove `pieces[0]`. If the list becomes empty, call `RemoveHoles`.

**`Assets/Scripts/HabbitManager.cs`:**
- `public int RotusCount => save.rotusCount;`
- `public event Action<int> RotusChanged;`
- `public event Action<HabitData, string, int> PieceRestored;` (habit, paintingId, pieceIndex)
- `public bool UseRotus(string habitId, string paintingId)`: if `rotusCount > 0` and `RestoreOne >= 0`, decrement the count, call `BurnedPaintingCache.Forget(habitId)`, save, and raise both events.

**Tests:** `RestoreOne` on an empty or missing painting returns -1. It removes exactly one piece. The entry is gone once it is empty.

---

## 2. Galeri header: stock pill
**File:** `Assets/Scripts/UI/Gallery/GalleryScreen.cs`. Also apply it to the `GalleryDetailView` header, because the pill stays in place when the settings button turns into the back button.

The header is 3 columns. The left and right columns are flexible with equal width, so the title stays centered. The settings/back button is left-aligned; the **pill is right-aligned**.

Pill:
- Height **38**, radius **19**, padding left 2 / right 12, spacing 1.
- Background `#7A1F22`, border **2 pt** `rgba(226,176,74,.6)`, drop shadow 0 3 0 `rgba(0,0,0,.35)`.
- Child 1: `Image` 32×32, `Rotus` (animated). When the count is 0, use `Rotus_Empty`, static.
- Child 2: TMP count, 16 pt semibold. `#F3E6CC`, or `rgba(243,230,204,.5)` when the count is 0.

It is display-only for now. A shop/earn screen can hook the tap later. Subscribe to `RotusChanged`.

New `UiTheme` fields: `rotusPillBg #7A1F22`, `rotusPillBorder #E2B04A @60%`, `rotusBlue #2C4FB8`, `rotusBlueLight #A9C2FF`, `rotusGlow #7EA2F2`, plus the 3 sprites.

---

## 3. Glow + tap on burned-piece UI
Two places, both active **only when `RotusCount > 0` and the work has holes**:

**a) Work tile hole badge** (`Assets/Scripts/UI/Gallery/GalleryWorkFrame.cs`, `holeBadge`):
- Size grows from 22 to **26**: radius 13, padding 0/7, offset −10/−10 (top-right).
- Background `#2B1B12` → **`#2C4FB8`**. Border `rgba(243,230,204,.45)` → **`#A9C2FF`**. Text white, 12 pt semibold.
- Add a `Button` that opens the restore sheet for this work. Tapping the badge must not also open the viewer.

**b) Viewer chip** (`Assets/Scripts/UI/Gallery/GalleryPaintingViewer.cs`, `holesChip`):
- Background transparent → **`rgba(44,79,184,.35)`**. Border → **`#7EA2F2`**.
- Text `"{n} parça yandı"` followed by a second span `" · Onar"` in `#A9C2FF`. Use TMP rich text `<color=#A9C2FF> · Onar</color>`.
- Add a `Button` that opens the restore sheet.

**Glow** (both): a soft outer glow pulse, **1.6 s** loop, ease in-out.
- Glow sprite, `#7EA2F2`. Alpha goes 0.55 → 0.95 → 0.55, and scale goes 1.00 → 1.15 → 1.00 (≈ +4 pt spread).
- Use a sliced soft-glow sprite behind the element, or extend `AttentionPulse`.

When `RotusCount == 0`, show the original styling, with no glow and no tap. Restyle when `RotusChanged` fires.

---

## 4. Restore sheet
**New:** `Assets/Scripts/UI/Gallery/RotusRestoreSheet.cs`. It is an overlay under the Gallery canvas root, above the detail view and viewer and below Ayarlar.

Layout:
- **Scrim:** full screen, `rgba(10,5,2,.62)`. Fades in over 0.2 s. Tapping it closes the sheet.
- **Sheet:** bottom-anchored, top radius 28, vertical gradient `#4A301E → #2B1B12`, top border 2 pt `rgba(226,176,74,.35)`. Padding 10 / 22 / 40, vertical spacing 18, centered.
  1. Grabber: 40×5, radius 3, `rgba(243,230,204,.25)`.
  2. `Rotus` icon at 64×64 (animated, or `Rotus_Empty` when the count is 0).
  3. Eyebrow "RÖTUŞ": 12 pt semibold, `#A9C2FF`, letter-spacing .1em.
  4. Title "Yanan parçaları onar": 24 pt bold, `#F3E6CC`.
  5. Sub line `"{paintingName} · {n} parça yanık"` (or "Yanık parça kalmadı"): 13 pt, `rgba(243,230,204,.62)`.
  6. **Painting box:** padding 14, radius 18, `rgba(20,10,4,.4)`.
     - The painting is scaled by a whole number to fit about 200×170 pt, drawn with `BurnedPaintingCache.SpriteFor(definition, habitId, holes)`.
     - Drop shadow 0 10 20 `rgba(0,0,0,.5)`.
     - Over each burned cell, draw an inset 1 pt outline in `rgba(169,194,255,.9)`, using the `PaintingCoverGrid` cell layout.
  7. Body: "Her rötuş yanan bir parçayı geri getirir. Elinde **{count}** rötuş var." 13 pt, `rgba(243,230,204,.7)`. The count is semibold `#F3E6CC`.
  8. **Primary button:** height 52, radius 16, full width.
     - Enabled: gold gradient `#E9BD5C → #D39D38`, shadow 0 4 0 `#8A5F1C`, text `#2B1B12`, 17 pt semibold. Label "Bir parçayı onar".
     - Disabled: `rgba(226,176,74,.22)`, no shadow, text `rgba(243,230,204,.55)`. Label "Rötuş kalmadı", or "Tablo onarıldı ✓" when no holes are left.
  9. **Secondary:** height 44, text-only, 15 pt, `rgba(243,230,204,.7)`. Label "Vazgeç", which becomes "Kapat" once no holes are left.

Motion:
| Element | Animation | Timing |
|---|---|---|
| Sheet | translateY 100% → 0 | 0.32 s, `cubic-bezier(.2,.8,.2,1)` ≈ `Easing.OutCubic` |
| Scrim | alpha 0 → .62 | 0.2 s |
| **Restore flash** | on the restored cell: a radial burst (white center → `#7EA2F2` 85% at 40% → transparent at 70%), scale 0.4 → 2.4, alpha 1 → 0 | 0.9 s ease-out |
| Painting | swap to the new `SpriteFor(...)` at flash start | — |

Tap "Bir parçayı onar" → `manager.UseRotus(habitId, paintingId)` → on `PieceRestored`, play the flash on that cell and refresh the sheet, the badge, the chip, the header pill and `GalleryDetailView` stats.

**Restore order:** the first hole in `pieces`. For "tap a specific cell" later, the sheet already has per-cell rects.

---

## Touch list
| File | Change |
|---|---|
| `Data/PlayerSaveData.cs` | `rotusCount` |
| `Paintings/HabitGallery.cs` | `RestoreOne` |
| `HabbitManager.cs` | `RotusCount`, `UseRotus`, `RotusChanged`, `PieceRestored` |
| `UI/UiTheme.cs` | rötuş colors + 3 sprites |
| `UI/Gallery/GalleryScreen.cs` + `GalleryDetailView.cs` | header right pill |
| `UI/Gallery/GalleryWorkFrame.cs` | badge restyle + glow + tap |
| `UI/Gallery/GalleryPaintingViewer.cs` | chip restyle + glow + tap |
| `UI/Gallery/RotusRestoreSheet.cs` | **new**: bottom sheet + flash |
| `Editor/UI/PainterHabitsBuilder.cs` | import sprites, build pill/sheet, wire refs |
| `Tests/…` | `RestoreOne` tests |

## Files
```
Rotus.dc.html          design reference
support.js             needed to open the .dc.html
sprites/rotus/         Rotus.png (4-frame sheet), Rotus_Static.png, Rotus_Empty.png
preview/               Rotus_sheet_6x.png (6× preview, not for import)
```
