# Handoff: Painter Habits — Atölye + Alışkanlıklar ekranları

## Overview
Two mobile screens for the HabbitTracker Unity project:
1. **01 Atölye** — top: pixel-art Renaissance studio with the animated painter sprite (mood-driven) and his own easel painting; bottom: filter row + habit tiles (tap = complete today).
2. **02 Alışkanlıklar** — habit management: grouped list, "Yeni alışkanlık" button, bottom-sheet form for add / edit / delete.

## About the Design Files
`Painter Habits.dc.html` is a **design reference built in HTML**, not production code. Recreate it in the existing Unity project (uGUI or UI Toolkit, whichever the project uses) following its patterns: `PainterManager`, `MoodService`, `HabitScheduling`, `PlayerSaveData`. Open the HTML in a browser to see the behavior (`support.js` must sit next to it).

## Fidelity
**High-fidelity.** Final colors, type, spacing and interactions. Reference frame: 390×844 pt (iPhone 14/15); content area inside the bezel is 366×820.

## Screen 01 — Atölye
**Top scene (height 452)**
- Background: `sprites/studio_bg.png` (92×114 px pixel art), drawn at **4×** (368×456), point filtering, anchored top-left. Bottom 70 px fade to `#2B1B12`.
- Header (top 56, side padding 18): round buttons 38×38, bg `#7A1F22`, 2 px border `rgba(226,176,74,.6)`, shadow 0 3 0 `rgba(0,0,0,.35)`. Left "‹" back; right `done/total` counter (13 px/600). Center: "Atölye" 14/500 at 90% opacity, "Vincent" 32/700, text-shadow 0 3 0 `rgba(30,15,6,.7)`; color `#F3E6CC`.
- Mood pill (top 128, centered): bg `rgba(30,16,8,.72)`, border 1 px `rgba(226,176,74,.3)`, radius full, padding 5/12. Label uppercase 12/600 `#E2B04A`, letter-spacing .06em. 12 segments 8×10, gap 2, radius 2: filled `#E2B04A`, empty `rgba(243,230,204,.18)`.
- Painter: sprite sheets 33×36 px per frame, drawn at **5×** (165×180), point filtering, position left 36 / top 202. Idle = 10 frames, 1.1 s loop. Painting = 19 frames, 1.9 s, plays once when a habit is completed, then back to Idle. Shadow ellipse 175×18 under feet (left 30, top 370).
- Mood → sprite set (same as PainterManager): ≥11 UltraRich, ≥9 Rich, ≥7 Happy, ≥4 Normal, ≥2 Sad, else Starve. Labels: Çok zengin / Zengin / Mutlu / Normal / Üzgün / Aç.
- Easel painting: `sprites/easel_painting.png` (24×26) at 4× = 96×104, left 231 / top 200. Covered by a 6×6 grid of `#E9DCBF` cells; cells reveal (transparent, 0.5 s fade) in proportion to today's done / total habits. This painting is the painter's own, not a habit painting.
- Toast (top 168): bg `#E2B04A`, text `#2B1B12` 13/600, radius 14, pop-in 0.3 s. Messages: "Hepsi tamam! Ruh hali +1", "Tablo bitti! Galeriye eklendi".

**Filter row** (overlaps scene by −58, height 92): 5 diamonds (rotated 45° squares, radius 12), columns 62 wide, gap 10: Tümü / Yap / Bırak / Sağlık / Zihin, count inside (16/700).
- Selected: 50×50, bg gradient `#9E2B2C→#7A1F22`, border 2 `#E2B04A`, label `#E2B04A`.
- Unselected: 40×40, bg `#5A3D28→#3E2A1C`, border `rgba(243,230,204,.14)`, label `rgba(243,230,204,.62)`.
- Shadow 0 4 0 `rgba(0,0,0,.28)`. Label 11/500 under the diamond.

**Habit grid** (padding 8/20/40, bg gradient `#2B1B12→#3B2618`): 3 columns, gap 16 × 14.
- Tile: square, radius 22, bg gradient 160° `#5A3D28→#3E2A1C`, border 3 (done `#E2B04A`, else `rgba(243,230,204,.1)`), shadow 0 5 0 `rgba(0,0,0,.3)`, press scale .96.
- Inside: habit painting (`tablo_inner.png`, no frame) at 62% of the tile, covered by an 8-column grid (16/32/64 cells → 2/4/8 rows); unopened cells `rgba(34,21,13,.9)`.
- Done badge: 24 circle top-right (−8, −8), `#E2B04A`, "✓" `#2B1B12`.
- Type label bottom-left 10/600: YAP `#B5CC86`, BIRAK `#EB8F6E`.
- Under the tile: title 13/500 `#F3E6CC`; "{n} gün seri" 11/400 `rgba(243,230,204,.62)`.
- (Alternative list layout also in the HTML: `layout` prop = "list".)

## Screen 02 — Alışkanlıklar
- Background: gradient `#3B2618` (top) → `#2B1B12` (40%).
- Header: same back button; center "{n} aktif" 14/500 `rgba(243,230,204,.7)` + "Alışkanlıklar" 28/700 `#F3E6CC`.
- List (padding 18/20/120, section gap 22): group header uppercase category name 13/600 `#E2B04A`, letter-spacing .08em, count on the right.
- Card: radius 20, padding 12/14/12/12, gap 14, same brown gradient, border 2 `rgba(243,230,204,.08)`, shadow 0 4 0 `rgba(0,0,0,.3)`, press scale .98. Tap opens edit.
  - Thumbnail 60×60, radius 6, painting + 8-column reveal grid.
  - Row 1: title 16/500 (ellipsis) + type pill (bg `rgba(20,10,4,.35)`, 10/600, YAP/BIRAK colors).
  - Row 2: 7 day chips 18×18, radius 5, first letter of Pt Sa Ça Pe Cu Ct Pz. Active: bg `rgba(226,176,74,.22)`, text `#E2B04A`; inactive: bg `rgba(20,10,4,.3)`, text `rgba(243,230,204,.3)`. Empty `days` = every day active.
  - Row 3: progress bar 6 high (track `rgba(20,10,4,.45)`, fill `#E2B04A`, % = opened/size) + "{streak} gün · en iyi {longest}" 11/400.
- Primary button (fixed bottom, 20 side / 30 bottom, fade-to-bg behind): 56 high, radius 18, gradient `#E9BD5C→#D39D38`, shadow 0 5 0 `#8A5F1C`, "+ Yeni alışkanlık" 17/600 `#2B1B12`. Pressed: translateY 3, shadow 0 2 0.

**Bottom sheet (add / edit)**
- Scrim `rgba(15,8,4,.6)` fades in 0.2 s; tap closes. Sheet height 700, radius 30 top, bg `#3B2618`, top border 2 `rgba(226,176,74,.35)`, slides up 0.28 s cubic-bezier(.2,.8,.2,1). Grabber 40×5.
- Top bar: "Vazgeç" (left, closes) · title "Yeni alışkanlık" / "Düzenle" 18/600.
- Section labels: uppercase 12/600 `#E2B04A`, letter-spacing .08em; field gap 22.
- **Başlık**: text input 50 high, radius 14, bg `#2B1B12`, border 2 `rgba(243,230,204,.12)` (focus `#E2B04A`), placeholder "Örn. Su iç".
- **Tür**: 2 cards — "Yap / Her gün yapınca tik at" (`#B5CC86`), "Bırak / Yapmadığın gün tik at" (`#EB8F6E`). Selected: bg `rgba(226,176,74,.12)`, border `#E2B04A`.
- **Kategori**: pills 36 high: Sağlık, Zihin, Beslenme, Üretkenlik. Selected: fill `#E2B04A`, text `#2B1B12`.
- **Günler**: 7 toggle buttons 44 high, radius 12. Hint on the right: "Seçilmezse her gün" / "{n} gün seçili".
- **Tuval**: 3 cards 16 / 32 / 64 parça (Kısa / Orta / Uzun), each with a mini 8-column grid preview.
- **Bitiş tarihi**: toggle 48×28 (on `#E2B04A`); when on, shows a date input.
- Footer: in edit mode "Sil" (bg `#7A1F22`, shadow 0 4 0 `#4A1012`) + "Kaydet"; in add mode only "Ekle". Save is disabled (`rgba(226,176,74,.35)`) while the title is empty.

## Interactions & State
- Tap a tile on 01 → toggle `done` for today; opened ±1 (clamped to size), streak ±1; play the Painting animation.
- All habits done → mood +1 once per day (`MoodService` logic); undo → −1.
- Last cell of a canvas opened → "Tablo bitti" toast (move it to the gallery).
- The easel reveal on 01 = done count / total, for today.
- 02 add / edit / delete writes to the same habit list that 01 reads. Edit keeps `opened = min(opened, newSize)`.
- Habit fields used: id, title, type (Build/Quit), category, days (0=Mon…6=Sun, empty = daily), canvasSize (16/32/64), opened, streak, longestStreak, doneToday, endDate (optional). Map these to `PlayerSaveData`.

## Design Tokens
- Colors: bg `#2B1B12`, surface `#3B2618`, card `#5A3D28→#3E2A1C`, burgundy `#7A1F22` / `#9E2B2C`, gold `#E2B04A` (button `#E9BD5C→#D39D38`, shadow `#8A5F1C`), cream text `#F3E6CC`, muted text `rgba(243,230,204,.62)`, Build `#B5CC86`, Quit `#EB8F6E`, unopened cell `rgba(34,21,13,.9)`, easel cover `#E9DCBF`.
- Font: **Fredoka** (Google Fonts), weights 400/500/600/700.
- Radius: 5, 6, 12, 14, 18, 20, 22, 30, full.
- Shadows: hard, offset-only ("0 4 0 rgba(0,0,0,.3)"), no blur, for a game-UI feel.
- All pixel art uses point / nearest filtering with integer scaling.

## Assets
- `sprites/studio_bg.png` — new, studio background (92×114).
- `sprites/easel_painting.png` — new, the painter's own portrait (24×26).
- `sprites/tablo_inner.png` — `TabloDisiInciKupe.png` with the frame cropped (42×42).
- Painter sprite sheets are the project's own (`Assets/Sprites/PainterSprites/*`), not duplicated here.

## Files
- `Painter Habits.dc.html` — both screens (open in a browser)
- `support.js` — runtime for the HTML
- `sprites/` — the new assets above