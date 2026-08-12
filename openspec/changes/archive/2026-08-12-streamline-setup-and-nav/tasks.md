## 1. Extract the position-plane component

- [x] 1.1 Create `WebApp/wwwroot/js/position-plane.js` and move the pan,
  zoom, floor-plan overlay, world-bounds fitting, anchor marker
  rendering, and anchor drag-persist logic out of the inline script in
  `Views/Sessions/Live.cshtml`. Expose a `PositionPlane.mount(root,
  config)` factory that returns an object with methods the caller uses
  (`setSelectedAnchors(ids)`, `updateTagMarker({tagId, x, y, z})`,
  `dispose()`).
- [x] 1.2 Create `Views/Shared/_PositionPlane.cshtml` partial that
  renders the plane markup (position-plane container, plane content,
  floor-plan `<img>`, axis labels, zoom toolbar, tag marker template)
  and takes a strongly-typed view model with `Anchors`, `Tags`,
  `FloorPlan`, and `Mode` ("live" | "layout").
- [x] 1.3 Move the map CSS out of `Views/Sessions/Live.cshtml` into a
  new `WebApp/wwwroot/css/position-plane.css` and reference it from
  `_Layout.cshtml`.
- [x] 1.4 Rewrite `Views/Sessions/Live.cshtml` to include the partial
  with `Mode = "live"` and to keep only the live-specific glue: the
  activate/deactivate form, the results table, the routing-selected
  anchor calls, and the SignalR-driven tag marker updates that call
  into the returned `PositionPlane` handle.
- [x] 1.5 Manually verify the live view still behaves identically:
  connect to a session, watch a tag update, drag an anchor and confirm
  the PATCH fires and persists across reload, verify the routing
  highlight still appears on selected anchors.

## 2. Slim the top navigation

- [x] 2.1 Edit `Views/Shared/_Layout.cshtml`. Keep only three primary
  nav items — `Sessions`, `Rooms` (label change from "Room layouts",
  same route), and `Chips`. Remove the top-level `Live`, `Trial`,
  `Room layouts`, `Layout chips`, `Position results`, and
  `Raw measurements` links.
- [x] 2.2 Add a right-aligned `Debug ▾` dropdown to the navbar using
  Bootstrap 5's `dropdown` component. Populate it with links to
  `/Home/Trial`, `/RawMeasurements`, and `/PositionResults`.
- [x] 2.3 Confirm every debug page still renders unchanged at its
  original URL.

## 3. Turn Home into a data-aware getting-started wizard

- [x] 3.1 In `HomeController.Index`, inject `AppDbContext` and compute
  four counts: chips, session configs, anchor placements with X and Y
  coordinates, sessions. Also load the most recent active session (if
  any). Expose them via a view model.
- [x] 3.2 Rewrite `Views/Home/Index.cshtml` to render the four-step
  wizard using a native `<details>` element. Server-set `open` when
  any step is still incomplete, closed when all four are complete.
  Mark each step as done/not-done based on the corresponding count.
- [x] 3.3 When all four steps are complete, render the operational
  summary below (or in place of) the collapsed wizard: link to the
  most recent active session if one exists, otherwise a "Start a new
  session" CTA that goes to `Sessions/Create`.
- [x] 3.4 Add minimal CSS in `site.css` to give the `<details>` wizard
  a consistent appearance across browsers.

## 4. Grow the room detail page into the placement cockpit

- [x] 4.1 In `SessionConfigsController.Details`, eager-load
  `SessionConfigChips.Chip` and load the room's `Sessions` list
  (ordered most recent first). Extend the view model to carry the
  data the partial needs (anchors, tags, floor plan).
- [x] 4.2 Rewrite `Views/SessionConfigs/Details.cshtml` to render, in
  order: room metadata (name, description, planned duration, floor
  plan status, Edit link), the `_PositionPlane` partial with
  `Mode = "layout"`, an anchors list, a tags list, and a sessions
  list with links to open live or view results.
- [x] 4.3 Wire drag-persist so that on the room page the same
  `PATCH /api/SessionConfigChips/{id}/coords` endpoint is called on
  drop; verify persistence.
- [x] 4.4 If the room has no floor plan configured, show a small hint
  under the map ("Upload a floor plan on Edit to add context").

## 5. Add anchor / tag from the room page

- [x] 5.1 Create partials `_AddAnchorDialog.cshtml` and
  `_AddTagDialog.cshtml` (Bootstrap modals) with a mode toggle "Use
  existing chip / Register new chip". For the "existing" mode, pull
  a list of chips that are not already in this room. For the "new"
  mode, expose Name + Device identifier fields.
- [x] 5.2 Wire the "Use existing chip" submit path to
  `POST /SessionConfigChips/Create` with `SessionConfigId` prefilled
  and `Role` set to `Anchor` or `Tag`; for anchors, include optional
  X/Y inputs.
- [x] 5.3 Wire the "Register new chip" submit path to `POST
  /Chips/Create` first, then `POST /SessionConfigChips/Create` with
  the returned chip id. Handle validation errors from either step
  gracefully.
- [x] 5.4 Ensure the tag dialog hides coordinate inputs entirely.

## 6. Retire the top-level Layout chips page

- [x] 6.1 Change `SessionConfigChipsController.Index` to
  `RedirectToAction("Index", "SessionConfigs")`.
- [x] 6.2 Delete `Views/SessionConfigChips/Index.cshtml`.
- [x] 6.3 Verify `/SessionConfigChips/Create`,
  `/SessionConfigChips/Edit/{id}`, `/SessionConfigChips/Delete/{id}`,
  and `/SessionConfigChips/Details/{id}` all still render.

## 7. Chips index shows placements

- [x] 7.1 In `ChipsController.Index`, eager-load
  `SessionConfigChips.ThenInclude(SessionConfig)`.
- [x] 7.2 Extend `Views/Chips/Index.cshtml` with a "Placed in" column
  that lists every room the chip is a member of together with its
  role in each. Mark chips with no memberships as unassigned.
- [x] 7.3 Add a simple filter control (query-string driven) to show
  only unassigned chips.

## 8. Documentation

- [x] 8.1 Update `readme.md` scaffolding / walkthrough sections to
  describe the new flow ("create a room, then add anchors/tags to it
  directly from the room page"). Remove references to a standalone
  "Layout chips" page.
- [x] 8.2 Add a short note in the room detail page (or in the wizard's
  step 3 tooltip) explaining that anchors need X and Y coordinates
  before trilateration can run, matching the existing warning shown
  in the live view.

## 9. Validation

- [x] 9.1 Run `dotnet build` and confirm no warnings introduced.
- [x] 9.2 Run `dotnet test` and confirm `App.BLL.Tests` still passes.
- [x] 9.3 Manually walk the wizard from an empty database: register a
  chip, create a room, add an anchor with coordinates from the room
  page, start a session, confirm the wizard collapses.
- [x] 9.4 Manually verify: navbar shows three primary items plus
  Debug dropdown; `/SessionConfigChips` redirects to
  `/SessionConfigs`; every debug URL still renders; anchor drag
  persists from both live view and room page; Chips index shows
  correct "Placed in" values for a chip that lives in two rooms.
