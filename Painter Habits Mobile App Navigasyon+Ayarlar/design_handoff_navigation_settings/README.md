# Handoff: Painter Habits — Bottom navigation, settings button, Ayarlar screen

## Overview
This package adds three pieces to the HabbitTracker Unity project:
1. **AltNavigasyon**: a bottom tab bar on Atölye, Alışkanlıklar and Galeri. It has three tabs plus a live mood badge at the far right.
2. **AyarlarTusu**: a settings button. It **replaces the "‹" back button** in the top-left header of those three screens, since the tab bar now handles navigation.
3. **Ayarlar**: the settings screen the button opens. It includes a Painter Premium card. There is no paywall in scope.

The rest of the app is already implemented. Only build what is described here, plus the layout adjustments listed in "Changes to existing screens".

## About the Design Files
The `.dc.html` files are **design references built in HTML**, not production code. Rebuild them in the existing Unity project and follow its patterns: `ScreenNavigator`, `ScreenNavButton`, `UiTheme` (incl. `IconFor(mood)`), `MoodPresentation.Label`, `PainterManager` (`CurrentMood`, `MoodChanged`), `UiGradient`, `ButtonPressFeedback`, `Easing`, `PlayerSaveData` / `HabitSaveSystem`, and the editor-built UI in `Editor/UI/PainterHabitsBuilder.cs`. Suggested home: `Scripts/UI/Navigation/` (`BottomNavBar`, `NavTab`, `NavMoodBadge`) and `Scripts/UI/Settings/` (`SettingsButton`, `SettingsScreen`, `SettingsToggleRow`).

To preview, open any `.dc.html` in a browser. `support.js` must sit next to it, and `sprites/` must stay relative.

## Fidelity
**High-fidelity.** Colors, type, spacing and motion are final. The device content area is **366×820 pt** (a 390×844 phone minus the 12 pt bezel used in the mocks). All values below are in pt. The font is **Fredoka** everywhere; use the existing TMP font asset.

---

## 1. AltNavigasyon (bottom tab bar)
**File:** `AltNavigasyon.dc.html`

### Container
- Full width, **90 high**, pinned to the bottom of the screen. It is in-flow: screen content ends above it and does not scroll under it.
- Padding: 10 top, 12 sides, **26 bottom** (home-indicator safe area). Children sit in one row with a gap of 2, stretched vertically.
- Background: vertical gradient `#3E2A1C` at the top to `#2B1B12` at the bottom.
- Top border: 2 `rgba(226,176,74,.28)`.
- Shadow: 0 −8, blur 18, `rgba(0,0,0,.28)`. A soft upward shadow sprite is fine.

### Tabs (×3, equal width, `flex:1`)
| Index | Label | Icon | ScreenNavigator index |
|---|---|---|---|
| 0 | Atölye | `icons/tab_atolye` (easel) | Workshop |
| 1 | Alışkanlıklar | `icons/tab_aliskanliklar` (checklist) | Habits |
| 2 | Galeri | `icons/tab_galeri` (framed picture) | Gallery |

- Each tab is a column, centered, with a gap of 4: icon **24×24**, then the label at **11 pt**.
- **Active tab:** icon and label `#E2B04A`, label weight 600. A **gold diamond indicator** sits on the bar's top border, centered over the tab. It is an 8×8 square rotated 45°, radius 2, `#E2B04A`, with its center on the border line (top −16 relative to the content box). It fades with alpha over 0.2 s.
- **Inactive tab:** icon and label `rgba(243,230,204,.62)`, weight 500. Diamond alpha is 0.
- Tap: `ScreenNavigator.Show(index)`. Use `ScreenNavButton` for the wiring so the listener is serialized (see the note in that file), and add `ButtonPressFeedback.EnsureOn`.
- Active state: subscribe to `ScreenNavigator.ScreenChanged` and highlight the matching tab. Don't track the selection separately.
- Icons are tinted white sprites (`Image.color`). The PNGs are 96×96 (4×); the SVGs are included as source.

### Separator
- 1 wide, `rgba(243,230,204,.12)`, margin 6 top, 6 sides, 2 bottom.

### Mood badge (far right)
- Fixed width 62. A column, centered, with a gap of 3.
- Badge: a **36** circle. A 2 pt gold ring `#E2B04A`, inner circle `#7A1F22`, and the mood sprite filling the inner circle (point filtering). Shadow 0 3 0 `rgba(0,0,0,.35)`.
- Label under the badge: `MoodPresentation.Label(mood)`, uppercase, 10/600, letter-spacing .04em, `#E2B04A`, single line.
- Data: `PainterManager.CurrentMood` → `UiTheme.IconFor(mood)` and the label. Refresh on `PainterManager.MoodChanged`. It is display-only; tapping does nothing for now.
- Sprites: `sprites/mood/Mood_{Starve|Sad|Normal|Happy|Rich|UltraRich}.png`. These are the same assets as the Atölye mood row and are probably already in `UiTheme.moodIcons`.
- The prototype's point → mood thresholds are placeholders. Use `PainterManager.MoodForPoint`.

### Layering
- The tab bar stays visible over **Galeri → Alışkanlık detayı** (the expanded habit card). That view is still inside the Galeri tab.
- It is **covered** by the full-screen painting viewer (Tablo görünümü), the Alışkanlıklar bottom sheet, and Ayarlar.

---

## 2. AyarlarTusu (settings button)
**File:** `AyarlarTusu.dc.html`
- It takes the exact slot and style of the old back button: a **38** circle with a 2 border (42 outer), top-left of the header.
- Background `#7A1F22`, border `rgba(226,176,74,.6)`, shadow 0 3 0 `rgba(0,0,0,.35)`.
- Icon: `icons/settings_gear`, **20×20**, tinted `#F3E6CC`.
- Pressed state: moves down 2, shadow 0 1 0. Use `ButtonPressFeedback`.
- Tap: opens Ayarlar (see below).
- It appears on **Atölye, Alışkanlıklar and Galeri** (top-level header only). The sub-views keep their own back and close buttons: Alışkanlık detayı keeps "‹" and the painting viewer keeps "✕".

---

## 3. Ayarlar (settings screen)
**File:** `Ayarlar.dc.html`

### Presentation
- Full-screen panel **above everything**, including the tab bar. It is not a `ScreenNavigator` tab.
- Open: slides in from the right, `translateX 100% → 0`, **0.28 s**, cubic-bezier(.2,.8,.2,1). That is close to `Easing.OutCubic`.
- Close: the "‹" button slides it back out to the right. Return to whichever screen opened it.
- While it is open, call `ScreenNavigator.SetModalOpen(true)` so the painter ignores taps. Call it with `false` on close.
- Background: vertical gradient, `#3B2618` at 0% to `#2B1B12` at 40%, then solid. This matches Alışkanlıklar and Galeri.

### Header (padding 6 top, 18 sides; below the 50 status bar)
- Left: back button, the same 38 circle style as above, with glyph "‹" 20/600 `#F3E6CC`.
- Center: "Ayarlar" 28/700 `#F3E6CC`, line-height 1.1.
- Right: an empty 42×42 spacer.

### Scroll content
- Padding 22 top, 20 sides, 48 bottom. **22** between blocks.
- **Section label:** 12/600 `#E2B04A`, uppercase, letter-spacing .08em, padding 0 4. 10 below it to the card.
- **Group card:** radius 20, 160° gradient `#5A3D28` to `#3E2A1C`, border 2 `rgba(243,230,204,.08)`, shadow 0 4 0 `rgba(0,0,0,.3)`, clips its children.
- **Row:** min height 56, padding 0 16, label on the left and control on the right, gap 12. Rows after the first get a top divider: 1 `rgba(243,230,204,.08)`. Row label: 15/500 `#F3E6CC`. Row sub-text: 12/400 `rgba(243,230,204,.62)`. Pressed rows: background `rgba(20,10,4,.25)`.
- **Toggle:** a 48×28 track, radius 14. Track color: on `#E2B04A`, off `rgba(243,230,204,.18)`. Knob: 22 circle `#F3E6CC`, top 3, left 3 when off and 23 when on. Both track and knob animate over 0.2 s. The **whole row** is the tap target.

### Blocks, top to bottom

**A. Premium card** (shown when **not** premium)
- Padding 18 top, 18 sides, 16 bottom. Same card gradient and shadow as above, but the **border is 2 `rgba(226,176,74,.55)`** (gold).
- Column, gap 14.
- Title row: "Painter Premium" 20/700 `#F3E6CC`, then a **PREMIUM** tag on the right.
- **PREMIUM tag (outline):** padding 2 top/bottom, 8 sides. Radius pill. Border 1.5 `#E2B04A`. Text 10/600, letter-spacing .06em, `#E2B04A`.
- Benefit list, gap 8. Each item is a 7×7 gold diamond (rotated square, radius 2, 6 from the top), a 10 gap, then text 14/400 line-height 1.35 `rgba(243,230,204,.85)`:
  1. "Galerideki her alışkanlığın detaylı istatistikleri"
  2. "Verilerini dışa aktarma"
  3. "Bağımsız bir geliştiriciye destek"
- Button: "Premium'a geç". Full width, **46** high, radius 14. Gradient `#E9BD5C` to `#D39D38`. Text 15/600 `#2B1B12`. Shadow 0 4 0 `#8A5F1C`; on press it moves down 3 and the shadow becomes 0 1 0.
- In the prototype, the button flips premium on. In the app it should start the store purchase flow, which is out of scope here. Leave a hook such as `PremiumService.Purchase()`.

**A'. Premium active card** (shown when premium; it replaces A)
- Padding 14 top/bottom, 16 sides. Card style, border 2 `rgba(226,176,74,.35)`. Row layout.
- Left column: "Premium aktif" 15/600 `#F3E6CC`. Under it: "Bağımsız bir geliştiriciye destek olduğun için teşekkürler" 12/400 `rgba(243,230,204,.62)`.
- Right: **PREMIUM tag (filled):** background `#E2B04A`, text `#2B1B12`, no border. Otherwise the same as the outline tag.

**B. RESSAM**
- Row "Adı". An inline text field on the right, right-aligned, 15/600 `#E2B04A`, no background or border, max 14 characters.
- The name is shown as the big title in the Atölye header, which currently reads "Vincent". An empty value falls back to "Vincent".

**C. HATIRLATMA**
- Row 1 (min height 62): "Günlük hatırlatma" with sub-text "Tik atılmamış alışkanlık varsa", and a toggle.
- Row 2 (only when row 1 is on): "Saat", with a time value on the right in 15/600 `#E2B04A`. Default **21:00**. Tapping opens a time picker.
- Behavior: schedule one local notification per day at that time, only if that day still has unchecked habits. Mobile notifications plumbing is out of scope; leave a hook.

**D. DENEYİM**
- "Ses efektleri" with a toggle.
- "Titreşim" with a toggle (haptics on check).

**E. TAKVİM**
- Row (min height 60, padding 0 12 0 16): "Hafta başlangıcı".
- Right side is a segmented control: container padding 3, gap 4, pill radius, background `rgba(20,10,4,.35)`. Segments are "Pazartesi" and "Pazar": 30 high, padding 0 12, 12/600.
- Selected segment: background `#E2B04A`, text `#2B1B12`. Unselected: transparent background, text `rgba(243,230,204,.75)`. Background transitions over 0.2 s.
- It affects the day order in the weekly day pickers and dots.

**F. VERİ**
- Row "Verileri dışa aktar". On the right: an **outline PREMIUM tag** (only when not premium), a gap of 8, then "›" 18/600 `#E2B04A`.
  - Not premium: tapping should scroll to or highlight the Premium card. It does not open a paywall.
  - Premium: export the save JSON through the share sheet. That is out of scope; leave a hook.
- Row "İlerlemeyi sıfırla": label and "›" in `#EB8F6E`. It needs a confirm dialog before calling the storage `Clear`, and that dialog is not designed yet.

**Footer:** "Painter Habits · sürüm 0.1", centered, 12/400 `rgba(243,230,204,.55)`. Use `Application.version`.

---

## Changes to existing screens
- **All three tab screens:** replace the back button with `SettingsButton`. Add the tab bar at the bottom and shrink the content area so it ends above the bar.
- **Alışkanlıklar:** the "Yeni alışkanlık" CTA container moves up to sit **on top of the tab bar**: `bottom: 90`, padding 40 top, 20 sides, **14 bottom**. It keeps its fade-up gradient. The list's bottom padding becomes **110**. The bottom sheet still covers the tab bar.
- **Galeri:** Alışkanlık detayı bottom padding becomes **132**, so the last row clears the tab bar. The painting viewer still covers the tab bar.
- **Atölye:** the old back button slot now holds the settings button. The header's mood indicator is the 6-circle mood badge row, which `MoodRow` / `MoodCircle` already cover. Nothing new there.
- Galeri **gating** (detail view only for premium) is **not part of this package**. For now, only the Premium card describes it.

## State
Add a settings block to `PlayerSaveData` and persist it through the existing debounced save:
```
painterName: string = "Vincent"
reminderOn: bool = true
reminderTime: string "HH:mm" = "21:00"
soundOn: bool = true
hapticsOn: bool = true
weekStart: enum { Monday, Sunday } = Monday
isPremium: bool = false   // in production derive from store receipt, not the save file
```
Runtime UI state: settings panel open/closed (it drives `SetModalOpen`) and the current tab (from `ScreenNavigator.Current`).

## Design tokens
- `#2B1B12` base dark · `#3B2618` top gradient · `#3E2A1C` / `#5A3D28` card gradient
- `#7A1F22` bordo (buttons, badge inner) · `#E2B04A` gold · `#E9BD5C` → `#D39D38` gold button · `#8A5F1C` gold button shadow
- `#F3E6CC` cream text · `rgba(243,230,204,.62)` secondary · `rgba(243,230,204,.85)` premium list
- `#EB8F6E` destructive / BIRAK
- Gold alphas: `.28` (nav top border), `.35` (premium-active border), `.55` (premium border), `.6` (circle button border)
- Radii: 20 card · 14 button and toggle · pill tags · 2 diamonds
- Shadows: hard drop shadows `0 3 0` / `0 4 0`, with no blur on buttons and cards

## Assets
- `icons/tab_atolye.png|svg`, `icons/tab_aliskanliklar.png|svg`, `icons/tab_galeri.png|svg`, `icons/settings_gear.png|svg`: white, 96×96 PNG, 24-unit SVG source. Tint them in Unity.
- `sprites/mood/Mood_*.png`: the six mood badges, already in the project.

## Files
- `AltNavigasyon.dc.html`: tab bar. The `active` (0–2) and `mood` (0–12) props can be changed in the preview.
- `AyarlarTusu.dc.html`: settings button.
- `Ayarlar.dc.html`: settings screen with the premium card. It is interactive: the toggles, segment, time and name all work, and "Premium'a geç" flips to the active state.
- `support.js`: runtime needed to open the HTML files.
