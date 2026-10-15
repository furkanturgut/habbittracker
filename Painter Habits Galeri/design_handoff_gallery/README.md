# Handoff: Painter Habits — 03 Galeri

## Overview
The gallery screen for the HabbitTracker Unity project. When a habit's painting is finished, it goes into that habit's gallery (`HabitData.habitGallery`). This screen shows those paintings, grouped by habit. It has three levels:
1. **Galeri**: one card per habit with its finished paintings hung on a wall.
2. **Alışkanlık detayı**: tapping a card expands it to full screen and shows that habit's stats and all of its paintings.
3. **Tablo görünümü**: tapping a painting opens it large, with prev/next and thumbnails.

## About the Design Files
`Galeri.dc.html` is a **design reference built in HTML**, not production code. Rebuild it in the existing Unity project and follow the patterns it already uses: `ScreenNavigator`, `UiTheme`, `PaintingCoverGrid`, `PaintingLibrary`, `HabitGallery`, `UiGradient`, `ButtonPressFeedback`, `Easing`, and the editor-built UI in `Editor/UI/PainterHabitsBuilder.cs`. A reasonable home is `Scripts/UI/Gallery/` (`GalleryScreen`, `GalleryHabitCard`, `HabitDetailView`, `PaintingViewer`). To see the behavior, open the HTML in a browser; `support.js` must sit next to it.

## Fidelity
**High-fidelity.** Colors, type, spacing and motion are final. The screen is **390×844 pt**. All values below are in pt at that size.

---

## Level 1 — Galeri
- **Background:** vertical gradient, `#3B2618` at 0% to `#2B1B12` at 40%, then solid.
- **Status bar:** 50 high, the same as the other screens.
- **Header** (padding 6 top, 18 sides):
  - Left: a 38 circle back button. Background `#7A1F22`, border 2 `rgba(226,176,74,.6)`, shadow 0 3 0 `rgba(0,0,0,.35)`, glyph "‹" 20/600 `#F3E6CC`.
  - Center: "{toplam} eser" 14/500 `rgba(243,230,204,.7)`, and under it "Galeri" 28/700 `#F3E6CC`.
  - Right: an empty 38×38 spacer.
- **Scroll list:** padding 20 top, 44 bottom. 26 between habit sections.

### Habit section (the whole section is one tap target; press scale .985)
- **Header row** (padding 0 24, gap 10):
  - Left: habit title 17/600 `#F3E6CC`, single line with ellipsis, then a type pill. The pill has padding 2/8, background `rgba(20,10,4,.35)`, 10/600, letter-spacing .05em. It reads **YAP** in `#B5CC86` or **BIRAK** in `#EB8F6E`.
  - Right: "{n} eser" (or "Henüz eser yok") 12/400 `rgba(243,230,204,.62)`, then "›" 16/600 `#E2B04A`.
- **Gap to card:** 10.
- **Card:** side margins 20 (width 350), radius 20.
  - Background: 160° gradient `#5A3D28` to `#3E2A1C`.
  - Border 2 `rgba(243,230,204,.08)`, shadow 0 4 0 `rgba(0,0,0,.3)`. This is the same card style as the Alışkanlıklar screen.
  - **Picture rail** (top): 6 high, `#3B2618`, bottom border 1 `rgba(226,176,74,.35)`.
  - **Paintings area:** a wrapping row, centered horizontally. Gap 12 vertical and 16 horizontal, padding 16 top, 12 sides, 14 bottom. The card grows **downward**; it never scrolls sideways.
  - **Wainscot strip** (bottom): 12 high, gradient `#3E2A1C` to `#2B1B12`, top border 2 `rgba(226,176,74,.25)`.
- **Painting item** (column, gap 8):
  - **Slot:** 112 high, min width 84, painting centered in it.
  - **Painting:** the `PaintingDefinition.sprite` at **2× native size**, point filtering. Shadow 0 6 blur 10 `rgba(0,0,0,.45)`; a soft shadow sprite is fine.
  - **Cover grid:** see "Piece states" below.
  - **Hole badge:** shown only if the painting has holes. Top-right, offset −8/−8. Min 22×22, radius 11, background `#2B1B12`, border 2 `rgba(243,230,204,.45)`. It shows the hole count, 11/600 `#F3E6CC`.
  - **Plaque:** padding 2/8, radius 4, border 1, shadow 0 2 0 `rgba(0,0,0,.3)`, 11/500, single line.
    - Finished: background and border `#E9DCBF`, text `#2B1B12`, text = `displayName`.
    - In progress: background `rgba(20,10,4,.45)`, border `rgba(226,176,74,.55)`, text `#E2B04A`, "Sürüyor · {opened}/{size}".
- **Order:** the in-progress painting comes first, then finished paintings from newest to oldest. `habitGallery` is stored oldest-first, so reverse it.

## Level 2 — Alışkanlık detayı (full screen)

**Open animation.** The card grows from its own rect (radius 20) to the full screen in **0.38 s, cubic-bezier(.2,.8,.2,1)**, animating position, size and radius.
- The base layer has the screen gradient. A card-gradient layer on top of it fades from 1 to 0 over 0.38 s.
- The content (a fixed 390×844 layout, so it does not reflow while growing) fades from 0 to 1 over 0.22 s, starting after a 0.14 s delay.

**Close animation.** Back "‹" plays the open animation in reverse. The content fades out right away, with no delay. Remove the view after 0.40 s. Closing also closes the painting viewer.

**Layout, top to bottom:**
- **Status bar:** 50 high.
- **Header** (padding 6/18/0): back button, then a centered column: "{kategori} · {Yap|Bırak}" 14/500 `rgba(243,230,204,.7)`, and the habit title 28/700 `#F3E6CC`, balanced wrap.
- **Scroll body:** padding 24 top, 20 sides, 44 bottom. Gap 24 between blocks.
- **Hero** (centered, gap 4):
  - Total: 64/700, line-height 1, `#E2B04A`, text-shadow 0 4 0 `rgba(0,0,0,.3)`.
  - Label: 16/500 `#F3E6CC`. "gün yapıldı" for Build, "temiz gün" for Quit.
  - Note: 12/400 `rgba(243,230,204,.62)`, "{missed} gün kaçırıldı · {d MMM yyyy} tarihinden beri". Leave out the missed part when missed = 0. Months: Oca Şub Mar Nis May Haz Tem Ağu Eyl Eki Kas Ara.
- **Stats:** 3 equal columns, gap 10.
  - Tile: padding 12/6, radius 16, card style.
  - Value 22/700 `#F3E6CC`, label 11/400 muted.
  - Tiles: "Şu anki seri" = `currentStreak`, "En iyi seri" = `longestStreak`, "Eser" = `habitGallery.Count`.
- **Plan:**
  - Header row (padding 0 4): "PLAN" uppercase 12/600 `#E2B04A`, letter-spacing .08em. On the right, "Her gün" or "Haftada {n} gün", 12/400 muted.
  - 7 equal chips, gap 6, 36 high, radius 10, 12/600: Pt Sa Ça Pe Cu Ct Pz.
  - Active day: background `rgba(226,176,74,.22)`, text `#E2B04A`. Inactive: background `rgba(20,10,4,.3)`, text `rgba(243,230,204,.3)`.
  - An empty `frequency` means every day is active.
- **Eserler:**
  - Header "ESERLER", with "{n} tamamlandı" or "Henüz eser yok" on the right.
  - Grid of 2 equal columns, gap 12.
  - Tile: radius 18, card style, padding 12/10, gap 8, press scale .97.
    - Slot 116 high: the painting at 2×, with its cover grid and hole badge, same as Level 1.
    - Name: 14/500 `#F3E6CC`.
    - Sub-line, 11/400. Finished: "{size} parça", plus " · {holes} yandı" when there are holes, muted. In progress: "Sürüyor · {opened}/{size}" in `#E2B04A`.
  - Tapping a tile opens Level 3 at that painting.

## Level 3 — Tablo görünümü
- **Overlay:** full screen, above Level 2, fades in over 0.2 s. Background is the screen gradient. Status bar on top.
- **Header:** a ✕ button (38 circle, same style as the back button, glyph 16/600). Centered next to it: habit title 14/500 muted, and "{i} / {n}" 15/600 `#F3E6CC`.
- **Painting:** centered in the remaining space, with its cover grid.
  - Integer scale: `k = max(1, floor(min(260 / w, 290 / h)))`, where w and h are the sprite's native size.
  - Shadow 0 16 blur 34 `rgba(0,0,0,.55)`.
- **Prev / next:** 40 circles, 12 from the left and right edges, vertically centered.
  - Background `rgba(20,10,4,.55)`, border 2 `rgba(226,176,74,.45)`, "‹" / "›" 20/600.
  - At either end of the list: opacity .3 and not tappable.
- **Info** (centered column, gap 8, padding 0 28):
  - Name: 28/700 `#F3E6CC`.
  - Meta: 14/400 `rgba(243,230,204,.7)`. "{size} parça · {n}. eser", or "{size} parça · şu an tuvalde" for the in-progress painting.
  - **Chips** (gap 8, 4 above):
    - Status chip: 28 high, padding 0 12, border 2 `#E2B04A`, 12/600. "Tamamlandı" has background `#E2B04A` and text `#2B1B12`. "Sürüyor · {o}/{s}" has a transparent background and gold text.
    - Holes chip (only when there are holes): border 2 `rgba(243,230,204,.25)`, text `rgba(243,230,204,.85)`, "{n} parça yandı". It starts with a 12×12 swatch, radius 3, showing the hatch pattern.
- **Thumbnails:** a centered row, gap 10, padding 26/20/40, scrolls sideways if needed.
  - Each thumbnail: 52×52, radius 12, background `rgba(20,10,4,.4)`, border 2 (`#E2B04A` when selected, otherwise `rgba(243,230,204,.12)`).
  - The painting is fit inside 36×36. The in-progress painting is drawn at opacity .55.
  - Tapping a thumbnail jumps to that painting.

## Piece states (cover grid)
The grid has 8 columns and `PieceCount / 8` rows (16 → 2, 32 → 4, 64 → 8), laid over the **whole sprite**. Index order is row-major, starting top-left. This matches `PaintingCoverGrid`.
- **Opened:** transparent.
- **Closed** (in-progress painting only): `rgba(34,21,13,.9)`. This is the current `closedColor` `#22150D` with alpha E6.
- **Missed / burned — NEW look**, replacing the flat `missedColor` fill:
  - The piece still shows the painting, but **desaturated**: grayscale(1), then sepia(.35), then brightness(.62).
  - Over it, a 45° hatch: 1.5 pt lines in `rgba(43,27,18,.45)`, repeating every 4 pt.
  - One way to build it: for each missed cell, add a `RawImage` with the same texture and a `uvRect` cropped to that cell, using a UI material that applies desaturate + sepia + darken. Then lay a tiled hatch sprite on top.

## Data mapping
- **Sections:** `PlayerSaveData.habits`, in list order. Every habit is shown, even one with no finished paintings yet; its in-progress painting fills the wall.
- **Finished works:** `habit.habitGallery` (ids). Resolve each id through `PaintingLibrary` to get `displayName`, `sprite` and `PieceCount`. "{n}. eser" is the 1-based index in `habitGallery`.
- **Holes:** `HabitGallery.TryGetHoles(habit, id, out pieces)`.
- **In-progress painting:** `activePaintingId`.
  - Opened pieces: `OpenedPieceCount(def.IsSequential)`. For Sequential, the first n indices are open. For Random, use the indices in `randomOpenedPixels`.
  - Burned pieces: `missedPixels`.
  - The easiest way to draw it is to reuse the existing `PaintingPieceState` / `PaintingCoverGrid`.
- **Skip the in-progress slot** when `awaitingPaintingChoice` is true. In that state, `activePaintingId` still points at the painting that just finished, and it is already in the gallery. Also skip it when `activePaintingId` is empty.
- **Total days:** Σ over the gallery of (`PieceCount` − holes), plus the in-progress opened count. Each completed day opens one piece.
- **Missed days:** Σ gallery holes + `missedPixels.Count`.
- **Start date:** `habit.startDate` ("yyyy-MM-dd").
- **Total on Level 1:** Σ `habitGallery.Count` across all habits.

## Design Tokens
- **Colors:**
  - Background `#2B1B12`, surface `#3B2618`.
  - Card gradient `#5A3D28` to `#3E2A1C`.
  - Burgundy button `#7A1F22`, gold `#E2B04A`, cream text `#F3E6CC`.
  - Muted text `rgba(243,230,204,.62)` and `.7`.
  - Plaque `#E9DCBF`, Build `#B5CC86`, Quit `#EB8F6E`.
  - Closed cell `rgba(34,21,13,.9)`, hatch `rgba(43,27,18,.45)`.
- **Font:** Fredoka 400/500/600/700, the same as the other screens.
- **Radius:** 4 (plaque), 10, 11, 12, 16, 18, 20 (card), full.
- **Shadows:** hard and offset-only on UI (`0 4 0 rgba(0,0,0,.3)`). Soft shadows only under paintings.
- **Pixel art:** point filtering, integer scale (2× on walls and tiles, `k`× in the viewer).

## Assets
- `sprites/paintings/p{id}.png` are copies of `Assets/Sprites/Paintings/Painting ({id}).png`; use the project originals. Native sizes:
  - 108 → 42×40
  - 126 → 44×37
  - 133 → 60×46
  - 19 → 42×55
  - 20 → 43×42
  - 214 → 40×41
  - 216 → 40×39
  - 36 → 42×51
  - 48 → 46×46
  - 62 → 45×43
- New sprite needed: a small tileable 45° hatch, 4×4 px.

## Files
- `Galeri.dc.html`: the gallery screen only. The mock data is in the logic class (`PAINT` and `habits`).
- `support.js`: the runtime the HTML needs.
- The `showActive` prop shows or hides the in-progress slot; it is for preview only.
