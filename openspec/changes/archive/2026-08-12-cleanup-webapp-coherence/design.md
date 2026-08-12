## Context

See `proposal.md` for motivation. Investigation (in explore mode) confirmed:

- `WebApp/Areas/Admin/Controllers/*.cs` compile in the `WebApp.Areas.Admin.Controllers`
  namespace, but no `[Area("Admin")]` attribute exists, `Program.cs` has no
  `MapAreaControllerRoute`, all `_Layout.cshtml` links use `asp-area=""`, and
  the views live at `WebApp/Views/*` (not `WebApp/Areas/Admin/Views/*`). MVC
  discovers them by convention regardless of physical folder. The folder name
  is decorative.
- `App.BLL/SessionConfigService.cs` has exactly one external reference: a
  comment in `App.BLL/Positioning/AnchorPositionProvider.cs:52` that describes
  behavior that does not run. The class is not registered in DI.
- `HomeController.Privacy` returns the stock template view.
- `Home/Live` (71 lines) predates `Sessions/Live/{id}` (801 lines with floor
  plans and per-session context). Both include `~/js/positioning-live.js`.
  The layout's top-nav "Live" link still points at `Home/Live`.
- `Session.Status` defaults to `ESessionStatus.Created`, but
  `SessionsController.Create` -> `StartSession` overwrites it to `Active`
  before the row is saved. No code path reads or writes `Created` after that.
- `Views/Sessions/Live.cshtml` imports `WebApp.Areas.Admin.Controllers` to
  read a public constant `MinAnchorsForTrilateration` from the sessions
  controller — a view-to-controller reverse coupling that will break the
  moment the controller namespace or folder moves (which this change does).

Constraints:

- Do not change public URLs. All existing bookmarks/hyperlinks must continue
  to resolve.
- Do not require an EF migration. Postgres schema and stored data are
  untouched.
- `App.BLL.Tests` must continue to pass without test modifications, unless a
  test references something being removed (verify during tasks).

## Goals / Non-Goals

**Goals:**
- Repo layout matches runtime reality (folder names, namespaces, and DI
  registration all tell the same story).
- Zero dead C# types, zero dead Razor views, zero orphan assets in the
  WebApp project.
- The one view→controller reverse dependency is removed.
- The `readme.md` scaffolding recipes match where controllers actually live.

**Non-Goals:**
- Introducing authentication / authorisation.
- Turning `Admin` into a real ASP.NET area (would require adding a real auth
  boundary; addressed by a later proposal if desired).
- Wiring `SessionConfigService.ValidateSessionConfig` into save paths
  (behavior change; deferred).
- Any of the UX items in the archived `next-steps.txt`.
- Restructuring the two ingestion services or the positioning pipeline.

## Decisions

### D1. Flatten `Areas/Admin/Controllers/` rather than making Admin a real area

The controllers already behave as top-level controllers (routing, `_Layout`
links, view lookup all confirm it). Making Admin a real area would require:
adding an area route in `Program.cs`, adding `[Area("Admin")]` on six
controllers, moving all views under `Areas/Admin/Views/`, updating every
`asp-controller`/`RedirectToAction` link, and — for it to mean anything —
adding an auth boundary. Flattening is a one-way rename with a mechanical
find/replace of the namespace. Chosen: **flatten**.

Alternative considered: keep the folder, add `[Area("Admin")]` +
`MapAreaControllerRoute`. Rejected because it introduces a URL prefix and
breaks every existing link, i.e. it is a behavior change dressed as a
cleanup.

### D2. Remove `ESessionStatus.Created` rather than mark `[Obsolete]`

Enums serialize as integer values in EF Core by default. If removing
`Created` shifts the ordinal of `Active`/`Finished`/`Cancelled`, previously
persisted rows would silently point at the wrong member.

**Verification step in tasks**: open `App.Domain/Enums/ESessionStatus.cs`
and confirm `Created` is the last member and/or that removing it does not
change any other explicit integer assignment. If ordinals would shift, fall
back to keeping the member and annotating it `[Obsolete("Never observed;
Session.Create immediately sets Active", true)]` while switching the
default initializer on `Session.Status` to `Active`. That still removes it
from human view without touching the DB.

### D3. Put the shared `MinAnchorsForTrilateration` constant in `App.BLL.Positioning`

Two candidates:
- On `LeastSquaresTrilaterationSolver` as `public const int MinAnchors2D = 3`
  / `MinAnchors3D = 4` (matches the actual solver contract).
- On a new `PositioningConstants` static class.

Chosen: **add on the solver class** (or its interface). The magic number *is*
a property of the solver, and the solver is already the natural home for
mode-specific requirements (`Solver:Mode` in `appsettings.json`). Views and
controllers reference it via `LeastSquaresTrilaterationSolver.MinAnchors2D`.
This is a pure move; the numeric value does not change.

### D4. Repoint the "Live" nav link to `Sessions/Index`, not to a specific session

`_Layout.cshtml` has no session id to substitute, and picking one at layout
render time would be wrong (there may be zero, one, or many active sessions).
Sending users to `Sessions/Index` matches the entry point that the setup
wizard step 5 becomes, and `SessionsController.Index` already highlights
active sessions. Alternative — a lightweight redirector action on
`HomeController` that picks "the" active session — is rejected as extra
runtime logic solely to preserve a nav link.

### D5. Keep `package.json` at the repo root

It exists only to install `@earendil-works/pi-coding-agent` (the pi tooling)
locally. Removing it would force every contributor to install the tool
globally. Decision: keep, but ensure `node_modules/` is gitignored so that
tooling install does not leak into commits.

### D6. Docs relocation

- `docs/mosquitto.md` — one-line broker command, formatted as a fenced code
  block with two lines of context.
- `docs/archive/next-steps.md` — verbatim contents of `next-steps.txt` with a
  bold header noting it is historical and not authoritative. Left verbatim
  because several UX items in it may still turn into future proposals.
- `docs/floorplans/ICO_3.korrus.pdf` — source artefact for the PNG served
  from `wwwroot/maps/`. Not linked into any doc; it just leaves the
  `WebApp/Maps` path (which is confusable with a served asset) and lands in
  the same tree as `docs/db-setup.md`.

## Risks / Trade-offs

- **[Risk] Renaming the Admin namespace breaks a Razor view we did not find.** →
  Mitigation: `grep -rn "Areas.Admin"` across the whole repo (including
  `*.cshtml`) after the rename and confirm zero hits. Then `dotnet build`.
- **[Risk] Removing `ESessionStatus.Created` shifts enum ordinals.** →
  Mitigation: task-list step to inspect ordinals and either delete or
  `[Obsolete]` per D2.
- **[Risk] A URL somewhere links to `/Home/Live` or `/Home/Privacy` that
  we didn't find.** → Mitigation: `grep` across views and controllers;
  document the intended replacement in the release notes / commit message.
- **[Risk] `HomeController.Live` was the "global firehose without a
  session" and someone relies on it.** → Mitigation: mentioned in
  `docs/archive/next-steps.md`; if a stakeholder objects, the view can be
  restored in isolation without reversing the rest of the change (it has no
  server-side logic).
- **[Trade-off] Flattening `Areas/Admin` closes the door on making Admin a
  real area later without a rename.** → Accepted; if/when auth arrives, a
  new proposal will introduce a real area at that point with all the
  consequential URL changes stated up front.

## Migration Plan

Single-commit refactor, no runtime migration:

1. Verify `dotnet build` and `dotnet test App.BLL.Tests` are green on `main`
   before starting (baseline).
2. Apply tasks in order (see `tasks.md`). Build and run tests after each
   numbered group.
3. Smoke test in the browser after step group 3 (controllers/views moved):
   `Home/Index`, `Chips/Index`, `SessionConfigs/Index`, `Sessions/Index`,
   `Sessions/Live/{id}` for an existing session, top-nav "Live" link,
   `/api/Chips`, `/api/Sessions`, `/api/anchor-routing/...`.
4. Rollback: single `git revert` of the cleanup commit. No DB rollback
   needed because no migration is added.

## Open Questions

None that block implementation. If the stakeholder later disagrees with
D4 (repointing "Live" to `Sessions/Index`), the layout link is a one-line
change.
