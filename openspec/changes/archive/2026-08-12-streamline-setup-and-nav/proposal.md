## Why

The webapp exposes every domain entity as its own top-level nav item and its
own scaffolded CRUD page. New users see nine nav links (Home, Live, Trial,
Chips, Room layouts, Layout chips, Sessions, Position results, Raw
measurements) and a five-step setup wizard whose middle step points at a
join-table list ("Layout chips") that has no natural home in the mental model
"a room has these anchors placed at these positions". Debug surfaces (Trial,
Raw measurements, global Position results) sit next to operator surfaces with
equal weight, and the drag-to-place anchor map — the single most useful piece
of UI in the app — is only reachable from inside an active live session.

The prior `cleanup-webapp-coherence` change removed dead code and consolidated
overlapping routes but explicitly deferred these UX changes. This change picks
them up.

## What Changes

Operator-facing UX reshape. No schema changes, no API removals, no changes to
positioning math or ingestion.

- **Slim the top nav** to three operator items (`Sessions`, `Rooms`, `Chips`)
  plus a right-aligned `Debug ▾` dropdown that groups the still-needed
  debugging surfaces (`Trial`, `Raw measurements`, `Position results`). The
  standalone "Live", "Room layouts", "Layout chips", "Trial", "Raw
  measurements", and "Position results" top-level links are removed. Existing
  URLs continue to resolve; only the nav is reorganised.
- **Rename "Room layouts" → "Rooms"** in the nav and page titles (route stays
  `/SessionConfigs`).
- **Home becomes a shrinking getting-started wizard.** Each step shows a
  live check-mark based on data counts (chips registered, rooms created,
  anchors placed with coordinates, sessions started). Once all four are
  satisfied the wizard auto-collapses to a one-line "Setup complete — show
  steps" link, and the main body of Home surfaces the most recent active
  session (if any) or a "Start a session" call to action. No `localStorage`
  opt-out in this iteration; the auto-collapse is sufficient.
- **Room detail page becomes the placement cockpit.** The interactive
  drag-to-place map currently only rendered in `Sessions/Live/{id}` is
  extracted into a shared Razor partial + JS module and mounted on
  `SessionConfigs/Details/{id}`. From the room page an operator can:
  - see the floor plan (if uploaded) with anchors placed on it,
  - drag anchors to reposition them (persisted via the existing
    `PATCH /api/SessionConfigChips/{id}/coords` endpoint — no backend
    change),
  - add a new anchor or tag via an inline dialog that either picks an
    existing unassigned chip or registers a new chip in one step,
  - see the list of sessions run against that room with links to open live
    or view results,
  - start a new session against that room.
- **"Layout chips" top-level page is removed from the nav and its Index
  action is replaced by a redirect to `SessionConfigs/Index`.** The Create,
  Edit, Delete, Details actions remain (they are reused by the room-detail
  dialogs and by the drag-coords API). Direct URLs continue to work but
  the entry point is now the room, not the join table.
- **Chips index gains a "Placed in" column** listing every room the chip
  currently belongs to (the many-to-many relationship stays as-is), plus a
  filter for "unassigned" chips. No changes to Create/Edit/Delete.
- **Sessions live view continues to work unchanged**, but its map, anchor
  markers, drag logic, floor-plan rendering, and pan/zoom are refactored
  into the same shared partial + JS module the room-detail page uses. The
  live view keeps its extras (tag marker, results table, routing highlight,
  activate/deactivate button).

Explicitly **out of scope**:
- Deleting Trial, Raw measurements, or global Position results — they are
  still needed for debugging and stay reachable via the Debug dropdown.
- Any changes to `Chip`, `SessionConfig`, `SessionConfigChip`, `Session`,
  `RawMeasurement`, or `PositionResult` schemas or public APIs.
- A `localStorage`-based "don't show wizard again" preference.
- Auth, roles, or hiding Debug behind a permission — Debug is visible to
  every user, just deprioritised in the nav.

## Capabilities

### New Capabilities

- `setup-and-navigation`: Rules for the top-level nav structure (operator
  items vs. debug dropdown), the Home getting-started wizard visibility
  logic, the Room detail cockpit (map placement + inline chip
  registration + session list), and the removal of the top-level Layout
  chips entry point. Covers the observable behavior an operator relies
  on when moving through the app; does not cover positioning math,
  ingestion, or persistence.

### Modified Capabilities

_None. No existing specs live under `openspec/specs/` yet._

## Impact

- **Views**: `Views/Shared/_Layout.cshtml` (nav restructure), `Views/Home/Index.cshtml`
  (wizard becomes data-aware and collapsible), `Views/SessionConfigs/Index.cshtml`
  and `Details.cshtml` (rename + cockpit), `Views/SessionConfigChips/Index.cshtml`
  (removed or replaced by redirect), `Views/Sessions/Live.cshtml` (refactored to
  consume shared partial), plus a new `Views/Shared/_PositionPlane.cshtml`
  partial.
- **JS/CSS**: New `wwwroot/js/position-plane.js` extracted from the inline script
  in `Sessions/Live.cshtml`; associated CSS moved into `wwwroot/css/site.css` or a
  dedicated file. `wwwroot/js/positioning-live.js` unchanged.
- **Controllers**: `HomeController.Index` gains counts for the wizard state.
  `SessionConfigsController.Details` eager-loads `SessionConfigChips` (+ `Chip`)
  and the room's Sessions. `SessionConfigChipsController.Index` becomes a
  redirect. `ChipsController.Index` eager-loads placements for the "Placed in"
  column. No new controllers.
- **Routing**: All existing URLs remain valid. Only the nav bar and inbound
  links from Home/Room pages change. `/SessionConfigChips` (Index) redirects to
  `/SessionConfigs`.
- **APIs**: Unchanged. `PATCH /api/SessionConfigChips/{id}/coords` continues to
  serve drag-persist from both the live view and the room detail page.
- **Database**: Unchanged. Chip ↔ Room stays many-to-many via
  `SessionConfigChip`.
- **Tests**: `App.BLL.Tests` should be unaffected. Any new controller logic
  (wizard counts, redirect) is thin enough for view-level verification during
  apply; no new test project introduced by this change.
- **Docs**: `readme.md` may need a note updating the described flow ("register
  chips → create room → assign chips" becomes "register a room, then add
  anchors/tags to it directly").
