# Plan: better-looking UI + build hotkeys that leave WASD to the camera

Branch: `ui` (from `mice`). **Read this file first when resuming**, continue with the first unchecked step,
tick + log + commit + push per step.

## Token-saving loop (every step)
1. Put what the UI shows (texts, numbers, colours as moods, which icons) in pure C# under `Scripts/UI/`
   and test it in `tests/.../UiTests.cs`: `task test -- <Area>` (a few tokens per run).
2. View code only draws that model. Find anchors with `grep -n`, never read whole files; edit with the
   Edit tool or Python with `newline=""`.
3. `task build` (View compiles), `task check` before commit.
4. Visual check: ONE headless screenshot per step (`shot.sh <name> 20` in the scratchpad), and only read a
   **cropped** region of it (PIL crop of the part that changed) instead of the whole 1200x800 frame.
5. Commit + push. Log one line below.

## Design (confirmed by the user: letters Z X C F G T, default font + outline, polished semi-transparent panels, compact)
- **Hotkeys**: first a number 1–7 for the category, then a letter that is not a camera/other key:
  **Z X C F G T** (slots 1–6, no category has more). WASD (pan), Q/E (zoom), R (rotate) and V (power
  overlay) stay with their jobs, so the camera blocking in `BuildHotkeys`/`CameraController` goes away.
- **Look**: one theme for every UI layer, in the game's night palette (from tools/art/restyle.py):
  dark navy panels (#161528 / #1f2240, a bit see-through), 2 px lighter border (#3b4a7a), rounded
  "plastic toy" corners, warm yellow accent (#f2b64a) for selected / hover, red (#b8384a) for missing,
  green (#5f9a45) for good. **Keep Godot's default font** (user) with a dark outline for readability.
  User: panels should look like a **polished game** (bevel/inner highlight, soft drop shadow, frame),
  **semi-transparent where reasonable**, **small icons**, and **much less text** — numbers next to icons,
  the explanations go into tooltips / the hover panel.
- **Resource bar** (top left): one panel; item icon + count/capacity with a thin fill bar (red when full);
  food and power as small in/out gauges with the mood colour instead of long text lines; both toyboxes'
  health as bars; builders/farmers with icons.
- **Build menu** (bottom): number tabs with a category icon; building cards with icon, name, cost as item
  icons + numbers (red when you can't afford that item), the hotkey letter as a corner badge, a yellow
  frame when selected; a short hint line.
- **Info panel**: themed panel, title row with the building icon, item rows with icons, styled
  progress bars, good/missing colours from the theme.
- **Minimap**: framed with the theme border; **status messages** as a small fading toast above the menu.

## Progress
- [x] 1. Hotkeys: letters Z X C F G T, no camera blocking (BuildHotkeys + tests, CameraController back to plain, menu labels/hint).
- [x] 2. UI models (pure, tested): `ResourceBarModel` (entries: item, count, cap, full; food/power gauges; core health), `BuildCardModel` (name, cost stacks with affordable flag per item, hotkey letter, selected), `Toasts` (message queue with timeouts).
- [x] 3. Theme: `View/UiTheme.cs` (Godot Theme: panel/button/tab/progress styleboxes with frame, inner highlight and shadow, default font + outline, sizes) applied to every CanvasLayer root. Screenshot (crop).
- [x] 4. Resource bar redesign on ResourceBarModel. Screenshot (crop top-left).
- [x] 5. Build menu redesign on BuildCardModel (tabs, cards, badges, cost icons). Screenshot (crop bottom).
- [x] 6. Info panel, minimap frame, toasts. Screenshot (crop).
- [ ] 7. CLAUDE.md (UI section), `task check`, commit, push, PR link.

## Log
- Step 1: BuildHotkeys letters ZXCFGT, Release/BlocksCamera removed (nothing clashes), CameraController plain again, menu labels/hint/status updated. 239/239.
- Step 2: Scripts/UI/Hud.cs: ResourceBarModel (StockEntry raw+food always, others when >0; Food/Power Gauge short+mood+tooltip; core Bars; WorkerCount builders/farmers/scouts), BuildCardModel (CostEntry affordable per item, hotkey letter, tooltip), Toasts (3 s, fade 0.6 s). 243/243.
- Step 3: View/UiTheme.cs (navy semi-transparent framed panels, rounded, shadow; buttons with yellow hover/pressed; outlined labels/tooltips; progress bars). Window theme does not reach controls under CanvasLayers: UiTheme.ApplyTo sets it on every Control root (Game._Ready end) + window for tooltips. Screenshot ok.
- Step 4: ResourceBar rewritten on ResourceBarModel: row 1 item chips (22 px icon, count, 3 px fill bar red when full) + food/power gauges (mood colour, long text as tooltip); row 2 core health bars + builder/farmer/scout counts. Refresh 4 Hz. Screenshot ok (about half the old size).
- Step 5: BuildMenu rewritten on BuildCardModel: number tabs (text, 12 pt), 92x92 cards (40 px icon, 11 pt name, cost = 14 px item icons + amounts red when short, hotkey badge), short hint, status as a fading toast above the bar (UI.Toasts). Tab icons dropped (squashed to nothing). Screenshot ok.
- Step 6: InfoPanel: theme colours, 18 px item icons, title row = building icon + accent name + separator. Minimap frame comes from the theme (step 3), toasts from step 5. VisualProbe warps the mouse to the screen centre to show the info panel. Screenshot ok.
