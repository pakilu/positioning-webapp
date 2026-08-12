## Why

The webapp accumulated scaffolding drift and stale files over several iterations.
Several structures now lie about themselves (an `Areas/Admin` folder that is not
actually an ASP.NET area, a `SessionConfigService` referenced only by a comment,
a `Home/Live` view that overlaps `Sessions/Live/{id}`, orphan assets at the
repo root). Nothing is broken, but a new reader has to reverse-engineer which
pieces are load-bearing and which are historical noise, and one Razor view even
reaches across a fake area boundary. This change removes dead code and resolves
the naming/layering mismatches without altering any runtime behavior.

## What Changes

Behavior-preserving cleanup only. Runtime routes, ingestion, positioning
math, database schema, and API surface are unchanged.

- Flatten `WebApp/Areas/Admin/Controllers/*` into `WebApp/Controllers/` and drop
  the `WebApp.Areas.Admin.Controllers` namespace. Delete the empty
  `WebApp/Areas/Admin/` folder. Update the one Razor view that imports that
  namespace (`Views/Sessions/Live.cshtml`).
- Extract `SessionsController.MinAnchorsForTrilateration` into a public constant
  on an `App.BLL.Positioning` type (e.g. on `LeastSquaresTrilaterationSolver`
  or a new `PositioningConstants`) so views and controllers share one source.
- Delete `App.BLL/SessionConfigService.cs`. It is unregistered, unreferenced,
  and never invoked. Fix the stale comment in
  `App.BLL/Positioning/AnchorPositionProvider.cs` that claims it runs on save.
- Delete `HomeController.Privacy()` and `Views/Home/Privacy.cshtml`
  (ASP.NET template boilerplate, unlinked).
- Delete `HomeController.Live()` and `Views/Home/Live.cshtml`. The layout link
  "Live" is repointed to `Sessions/Index` (the entry point that leads to
  `Sessions/Live/{id}`), and step 5 of the `Home/Index` setup wizard is
  updated to link to `Sessions/Index` instead of `Home/Live`.
- Remove `App.Domain.Enums.ESessionStatus.Created`. The value is the default
  on `Session.Status` but `SessionsController.Create` immediately calls
  `StartSession` which sets `Active`, so no persisted row can ever hold
  `Created`. Change the default initializer on `Session.Status` to `Active`
  (matches actual persisted state) and delete the enum member. **No EF
  migration is required** because the stored `int` values for the remaining
  members do not change; verify with `dotnet ef migrations has-pending-model-changes`.
- Delete `MVC_CONTROLLERS_REVIEW.md` (empty file, BOM only).
- Move `cmd open as admin.txt` under `docs/mosquitto.md` with proper
  formatting.
- Move `next-steps.txt` under `docs/archive/next-steps.md` with a header
  noting it is a historical design log, not a current spec.
- Move `WebApp/Maps/ICO_3.korrus.pdf` under `docs/floorplans/` (source
  artefact for the PNG that is actually served from `wwwroot/maps/`).
- Ensure `node_modules/` is in `.gitignore` (npm dependency is the pi
  coding-agent tooling, not part of the webapp) and that `package.json` /
  `package-lock.json` are either kept intentionally or moved out of the repo
  root — decision recorded in design.md.

Explicitly **out of scope** (would change behavior; deferred to separate
proposals):
- Wiring `SessionConfigService.ValidateSessionConfig` into save paths.
- Consolidating admin controllers behind auth / making `Admin` a real area.
- The UX changes discussed in the old `next-steps.txt` (session status flow,
  "Layout chips" placement in the nav, etc.).

## Capabilities

### New Capabilities
_None._

### Modified Capabilities
_None. This change is `skip_specs: true` — it is a pure refactor with no
requirement-level behavior change. Every removal or move is either dead
code, a stale comment, an orphan asset, or a rename of an internal
namespace/constant location._

## Impact

- **Code**: `WebApp/Controllers/`, `WebApp/Areas/`, `WebApp/Views/Home/`,
  `WebApp/Views/Sessions/Live.cshtml`, `WebApp/Views/Shared/_Layout.cshtml`,
  `App.BLL/SessionConfigService.cs`,
  `App.BLL/Positioning/AnchorPositionProvider.cs` (comment fix),
  `App.Domain/Session.cs`, `App.Domain/Enums/ESessionStatus.cs`.
- **Config / routing**: none. All URLs remain valid.
- **Database**: none. `ESessionStatus` int values for `Active`, `Finished`,
  `Cancelled` stay stable; only the unused `Created = 0` (or whatever its
  ordinal is) is removed. Task list requires verifying the enum ordinals
  before deletion to guarantee no migration is needed. If ordinals would
  shift, keep the enum member and instead mark it `[Obsolete]`.
- **Tests**: `App.BLL.Tests` should still pass unchanged. No test currently
  references `SessionConfigService` or `ESessionStatus.Created` (to be
  reconfirmed during the tasks phase).
- **Docs**: `readme.md` scaffolding section still names `Areas/Admin/Controllers`
  as the scaffold output directory — must be updated to `Controllers/`.
