## Context

See `proposal.md` — Why, and `specs/setup-and-navigation/spec.md` for the
observable requirements.

The webapp is an ASP.NET Core MVC project (Razor Views, Bootstrap 5,
vanilla JS, SignalR for the live channel). Server-rendered pages call a
handful of thin API endpoints for progressive updates. All state lives in
`AppDbContext` (EF Core). The single non-trivial piece of client JS today
is the pan/zoom/drag map inside `Views/Sessions/Live.cshtml` (~300 lines
of inline `<script>`).

Relevant existing endpoints reused by this change:
- `PATCH /api/SessionConfigChips/{id}/coords` — anchor drag persistence
  (already implemented; used by the live view).
- `POST /SessionConfigChips/Create`, `Edit`, `Delete` — CRUD on the join
  table (kept, wrapped by new UI).
- Standard MVC CRUD on `Chips`, `SessionConfigs`, `Sessions`.

The prior `cleanup-webapp-coherence` change has archived. `Home/Live` is
gone; `Sessions/Index` is the "Live" landing. No area boundary exists any
more. That leaves this change touching only Views, one shared partial,
one JS module, and three controllers.

## Goals / Non-Goals

**Goals:**

- Extract the drag-map so it renders identically in the live session view
  and on the room detail page, with a single source of truth for pan,
  zoom, floor-plan overlay, anchor markers, and drag-persist.
- Make the room detail page the primary place an operator configures a
  room, so the top-level "Layout chips" list becomes unnecessary.
- Keep the wizard on Home cheap: reactively derived from four `COUNT`
  queries, no per-user persistence.
- Do not change any URL, controller signature, API contract, or database
  column that an external caller might depend on.

**Non-Goals:**

- No visual redesign of the whole app. Bootstrap 5 stays. Colors stay.
  Typography stays.
- No SignalR changes, no new hubs, no changes to the ingestion pipeline.
- No new NuGet or npm dependencies.
- No auth, no per-user preferences, no i18n.
- No deletion of any controller, action, view partial, or route that this
  change does not itself introduce. Debug pages stay reachable.

## Decisions

### 1. Extract the map into a shared Razor partial + external JS module

**Choice:** Introduce `Views/Shared/_PositionPlane.cshtml` and
`wwwroot/js/position-plane.js`. The partial takes a view model with:
`anchors`, `tags`, `floorPlan`, `worldBounds` hints, a `mode` flag
(`"live"` vs `"layout"`), and the session/room ids needed for the API
calls. The JS module exposes a small factory (`PositionPlane.mount(rootEl,
config)`) that both the live view and the room detail view call.

**Alternatives considered:**

- *Keep the map inline in the live view, duplicate it into the room
  detail view.* Rejected: two copies of ~300 lines of interaction code
  drift instantly, and the drag-persist behavior is exactly the piece
  most likely to diverge.
- *Rewrite the map as a small ES module with imports.* The project has
  no bundler; a plain-old script tag with a global namespace matches the
  existing `positioning-live.js` pattern and keeps the change small.
- *Introduce a client-side framework (Vue/Alpine) just for this.*
  Overkill for one component; would violate the "no new dependencies"
  non-goal.

### 2. `mode` flag on the shared partial rather than two partials

The live view needs a moving tag marker, a results table, a
routing-selected anchor highlight, and an activate/deactivate button.
The room detail view does not. Rather than two partials, the shared
partial focuses only on the map — floor plan, anchors, world grid, pan,
zoom, drag-persist. Everything else (tag marker updates, results table,
routing calls, activate button) lives *outside* the partial in the live
view. This keeps the shared surface minimal and the coupling one-way:
the live view knows about the partial, not the other way round.

### 3. Home wizard state derived from four counts, no persistence

`HomeController.Index` runs four lightweight `CountAsync` queries:
- `Chips`
- `SessionConfigs`
- `SessionConfigChips` where `Role == Anchor && XCoord != null && YCoord
  != null`
- `Sessions`

The view uses these to mark steps and to decide the collapsed/expanded
default. The `<details>` element (native HTML) is used for the collapse
so no JS is needed for the interaction, and the `open` attribute is set
server-side based on whether all four counts are positive. No
localStorage, no cookies, no per-user preferences — the auto-collapse
is enough for a single-tenant tool.

**Alternatives considered:**

- *Store dismissal in `localStorage`.* Deferred; explicitly out of scope
  per the proposal.
- *A dedicated `SetupState` table.* Way too heavy for four counts.

### 4. "Add anchor / add tag" dialog reuses the existing Create endpoint

The inline flow on the room detail page posts to
`POST /SessionConfigChips/Create` with `SessionConfigId` prefilled and
`Role` chosen by the dialog. When the operator elects to register a new
chip inline, the dialog first posts to `POST /Chips/Create`, takes the
new chip id from the response (or from a redirect target), then posts
the `SessionConfigChip`. Two round-trips, but no new endpoint and no
transactional coupling between chips and their placements. A single
"create chip and place it" endpoint would be nicer but is out of scope
for this UX change.

**Alternatives considered:**

- *Introduce a single composite endpoint that creates both in one
  transaction.* Deferred — it is a backend concern that the user's
  request did not ask for, and the two-step form remains reliable.

### 5. `/SessionConfigChips` (Index) redirects, other actions stay

`SessionConfigChipsController.Index` returns
`RedirectToAction("Index", "SessionConfigs")`. The View file
`Views/SessionConfigChips/Index.cshtml` is deleted. The rest of the
controller stays exactly as it is. Rationale: preserves every deep link,
avoids breaking anyone who has bookmarked a per-row edit URL, and cleanly
removes only what the nav change actually removes.

### 6. Chips index "Placed in" column is eager-loaded, not lazy

`ChipsController.Index` becomes:
```csharp
var chips = await _context.Chips
    .Include(c => c.SessionConfigChips)
        .ThenInclude(scc => scc.SessionConfig)
    .OrderBy(c => c.Name)
    .ToListAsync();
```
With expected dataset sizes (dozens of chips, a handful of rooms), this
is fine. If the join grows past ~1000 rows the query can be projected to
a DTO in a follow-up change without touching the view.

### 7. Debug dropdown is Bootstrap, not custom

The existing layout already loads `bootstrap.bundle.min.js`, so the
navbar dropdown uses Bootstrap's `dropdown` component with no extra JS.
The three debug links (`Trial`, `Raw measurements`, `Position results`)
sit inside `.dropdown-menu`. No changes to CSS beyond right-aligning the
dropdown container.

## Risks / Trade-offs

- **Extracting the map introduces churn in the live view.** →
  Mitigation: extract map first, verify the live view still renders and
  behaves identically (drag persists, tag marker moves, results table
  populates), *then* mount the same partial on the room detail page.
  Two commits, first is a pure refactor.
- **The room detail page becomes the most complex Razor view in the
  project.** → Mitigation: keep the "list of sessions" and "add
  anchor/tag" dialog as separate small partials so the main view stays
  readable.
- **Two-step chip-then-placement creation can leave orphan chips if the
  user closes the dialog after step one.** → Mitigation: an orphan chip
  is just an unassigned chip and is visible in the chips index; no data
  corruption, no rollback needed. If it becomes annoying we introduce a
  composite endpoint in a follow-up.
- **The wizard's four counts add four queries to every home load.** →
  Mitigation: they are indexed primary-key counts; measured cost is
  negligible for this app's scale. Combine into one `Select` if needed
  later.
- **`<details>` element's default styling differs between browsers.** →
  Mitigation: a small amount of CSS in `site.css` gives it a consistent
  look; no JS fallback is needed for supported browsers (Chromium,
  Firefox, Safari all support it).
- **Anchor drag over an unstyled empty grid (no floor plan yet) may feel
  disorienting.** → Mitigation: the world grid + axis labels the live
  view already renders give enough reference; a hint line "Upload a
  floor plan on Edit to add context" is added to the room detail page
  when `FloorPlanImagePath` is null.

## Migration Plan

This is a UI-only change. No data migration. Deployment is a single
build + publish. Rollback is a redeploy of the previous build; no
schema state to unwind.

Suggested rollout order inside the change (also reflected in
`tasks.md`):

1. Extract the map partial and JS module; wire the live view through
   it, verify parity.
2. Slim the navbar and add the Debug dropdown.
3. Turn Home into the data-aware wizard.
4. Grow the room detail page (map + sessions list + inline add
   dialogs).
5. Retire the Layout chips top-level page (Index → redirect, delete
   view).
6. Extend the Chips index with the "Placed in" column.

Each step is independently deployable.
