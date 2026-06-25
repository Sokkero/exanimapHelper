# Multi-Path Recording, POI Markers & Live Position Dot

## Goal
Rework trail recording so a single session can hold multiple independent paths,
add point-of-interest (POI) markers, and always show the player's live position
on the graph. Recording becomes a three-state cycle (Start → Stop → Resume →
Clear → Start). The trail data model changes from one flat point list to a list
of paths plus a separate POI list, and the text format gains path separators and
a POI section so all of this round-trips through export/import.

## Caveats
1) The model restructure (flat point list → list of paths + POI list) is the
   load-bearing change. It ripples through all five source files
   (`TrailModel`, `GraphRenderer`, `ExportService`, `MainWindow.xaml`,
   `MainWindow.xaml.cs`). Everything else is additive.
2) Polling already runs continuously even when not recording (for the live
   readout), so the live position dot needs no new polling plumbing — it just
   consumes the existing `OnValueRead` callback.
3) The live position dot must **not** appear in the exported PNG. POIs and the
   trail must.
4) Graph bounds must be computed from recorded points + POIs only — never the
   live dot — otherwise the map would zoom/shift every time the player moves, and
   PNG framing would depend on transient live position.
5) Because bounds exclude the live dot, the live dot can fall outside the canvas
   when the player roams far from the recorded trail. Decision in §Design (clamp
   to edge).
6) Empty paths must never reach export. Entering recording always appends a new
   path, so a Start-record-nothing-Stop-Resume sequence would otherwise produce
   spurious blank lines.
7) Import must stay backward compatible: legacy files (no blank lines, no POI
   section) load as a single path with no POIs.
8) The on-screen data list display order may differ from the export file order
   (POIs are shown inline where marked for UX, but exported in a trailing
   section). This is intentional, not a bug.

## Design decisions made during planning
1) **Recording is a three-state machine** (`Idle` / `Recording` / `Paused`).
   The button and the F8 hotkey share one transition method. Label is
   `Start` / `Stop` / `Resume`. Clear from `Paused` returns to `Idle`.
2) **Every entry into Recording appends a new path.** This is what makes
   "resume = new path" fall out naturally; the initial Start also just creates
   path #1.
3) **Import honors blank-line groups** (chosen by user). Each blank-line-separated
   block becomes its own path; import **replaces** current data; legacy files
   with no blank lines load as a single path.
4) **POIs are stored in a trailing `--- POI ---` section** (chosen by user),
   separate from the path data. This loses temporal interleaving in the file but
   keeps the format clean.
5) **All recorded points share one uniform color; only the single globally-latest
   recorded point is red.** The previous green-start / blue-body / red-end scheme
   is dropped. "Latest" = last point of the last non-empty path.
6) **POI marker = hollow red circle at 10× the normal point diameter, no fill.**
   Mark POI is only enabled while Recording and marks the cached latest live
   position.
7) **Live position dot is a small red dot, drawn always** (recording or not),
   excluded from PNG export.
8) **Off-canvas live dot is clamped to the nearest canvas edge** so it stays
   visible; when there are no recorded points yet, it is centered. (User was
   offered "just clip/disappear" as the alternative; clamp was chosen as default.)

## Steps
1) Restructure `models/TrailModel.cs` to hold paths + POIs
    - Replace the flat `ObservableCollection<TrailPoint>` with
      `List<List<TrailPoint>> Paths` and `List<TrailPoint> Pois`.
    - Add `StartNewPath()`, `Add(x, y)` (keep the existing MinDistance filter but
      compare against the **current path's** last point only), `AddPoi(x, y)`,
      `PruneEmptyCurrentPath()` (called on Stop), `Clear()`, and
      `Load(paths, pois)`.
    - Expose `HasData` (any non-empty path or any POI) and `LatestPoint` (last
      point of the last non-empty path) for the renderer's red marker.

2) Rework `rendering/GraphRenderer.cs` for multi-path + markers
    - `Render(paths, pois)`: compute bounds from all path points **and** POIs;
      draw one polyline + interior dots per path; uniform color for all points;
      draw only `LatestPoint` in red.
    - Draw POIs as hollow red ellipses at 10× point diameter (no fill).
    - Add `UpdateLiveMarker(x, y)` that positions a separately-tracked red dot
      using the transform cached from the last `Render`, so the live dot updates
      without a full re-render. Clamp off-canvas; center when no transform exists.
    - Add `SetLiveMarkerVisible(bool)` so PNG export can hide it.

3) Update `services/ExportService.cs` for the new text format
    - Export: `x, y` per line, a blank line between paths, then `--- POI ---`
      followed by POI `x, y` lines if any POIs exist.
    - Import: blank line = path break; `--- POI ---` switches to POI-collection
      mode; prune empty paths; return `(paths, pois)`. Legacy no-blank files →
      single path, no POIs.
    - `ExportPng`: hide the live marker around the render (alongside the existing
      background-drop logic).

4) Update `views/MainWindow.xaml` + `.xaml.cs` for state machine, POI button, list
    - Replace the two-state toggle with the `Idle / Recording / Paused` machine;
      button + F8 call one transition method; set label per state; entering
      Recording appends a new path; Clear from Paused → Idle.
    - Add a **Mark POI** button enabled only while Recording; it marks the cached
      latest live position.
    - In `OnValueRead`, always call `UpdateLiveMarker`; while recording, also add
      to the current path and re-render.
    - Rebind `DataList` to an incrementally-updated display collection that
      includes `— new path —` separator rows, preserving scroll-to-latest.
    - Key `SetDataActionsEnabled` off `TrailModel.HasData`.
