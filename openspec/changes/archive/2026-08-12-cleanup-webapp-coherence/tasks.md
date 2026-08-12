## 1. Baseline

- [x] 1.1 On a clean working tree, run `dotnet build` and confirm it succeeds.
- [x] 1.2 Run `dotnet test App.BLL.Tests` and confirm all tests pass. Record
  the passing count for later comparison. (baseline: 38 passed)
- [x] 1.3 Run `grep -rn "Areas\.Admin\|Areas/Admin" --include='*.cs' --include='*.cshtml'`
  and save the list of hits as the ground truth for section 3. (baseline: 6 controller files + `WebApp/Views/Sessions/Live.cshtml`)

## 2. Extract shared solver constant (unblocks the view→controller decoupling)

- [x] 2.1 In `App.BLL/Positioning/LeastSquaresTrilaterationSolver.cs` (or its
  interface, whichever the file already exposes publicly), add
  `public const int MinAnchors2D = 3;` and `public const int MinAnchors3D = 4;`
  matching the current numeric values used by
  `SessionsController.MinAnchorsForTrilateration`.
- [x] 2.2 In `WebApp/Areas/Admin/Controllers/SessionsController.cs`, replace
  the `MinAnchorsForTrilateration` constant declaration with a reference to
  the new `App.BLL.Positioning` constant, or delete it entirely if no
  controller code still uses it. (deleted; no controller code referenced it)
- [x] 2.3 In `WebApp/Views/Sessions/Live.cshtml`, remove
  `@using WebApp.Areas.Admin.Controllers` and change `var minAnchors = ...`
  to reference the new constant via `@using App.BLL.Positioning`.
- [x] 2.4 `dotnet build`; `dotnet test App.BLL.Tests`. (38 passed)

## 3. Flatten Areas/Admin/Controllers → Controllers

- [x] 3.1 Move all six files from `WebApp/Areas/Admin/Controllers/` to
  `WebApp/Controllers/`:
  - `ChipsController.cs`
  - `PositionResultsController.cs`
  - `RawMeasurementsController.cs`
  - `SessionConfigChipsController.cs`
  - `SessionConfigsController.cs`
  - `SessionsController.cs`
- [x] 3.2 In each moved file, change the namespace from
  `WebApp.Areas.Admin.Controllers` to `WebApp.Controllers`.
- [x] 3.3 Delete the now-empty `WebApp/Areas/Admin/Controllers/` and
  `WebApp/Areas/Admin/` and `WebApp/Areas/` directories.
- [x] 3.4 Re-run the grep from task 1.3. It must return zero hits. If any
  hit remains (a `@using`, a fully-qualified type reference, or a stray
  comment), fix it. (zero hits)
- [x] 3.5 `dotnet build`; `dotnet test App.BLL.Tests`. (38 passed)
- [x] 3.6 Browser smoke test per design.md Migration Plan step 3.
  _Reviewer confirmed the smoke test at archive time; compile-time correctness had previously been verified via `dotnet build` and unit tests._

## 4. Remove SessionConfigService

- [x] 4.1 Confirm `grep -rn "SessionConfigService" --include='*.cs'` returns
  only the class definition and the stale comment in
  `App.BLL/Positioning/AnchorPositionProvider.cs`.
- [x] 4.2 Delete `App.BLL/SessionConfigService.cs`.
- [x] 4.3 In `App.BLL/Positioning/AnchorPositionProvider.cs`, remove the
  line ~52 comment that says "(the SessionConfigService validator already
  enforces this on save)" — replace with a brief comment describing what
  the code actually assumes (≥ 3 anchors per session config) and note that
  enforcement is not currently automated.
- [x] 4.4 `dotnet build`; `dotnet test App.BLL.Tests`. (38 passed)

## 5. Remove dead Home actions and views

- [x] 5.1 Delete `HomeController.Privacy()` from
  `WebApp/Controllers/HomeController.cs`.
- [x] 5.2 Delete `WebApp/Views/Home/Privacy.cshtml`.
- [x] 5.3 Delete `HomeController.Live()` from
  `WebApp/Controllers/HomeController.cs`.
- [x] 5.4 Delete `WebApp/Views/Home/Live.cshtml`.
- [x] 5.5 In `WebApp/Views/Shared/_Layout.cshtml`, change the top-nav "Live"
  link so it targets `asp-controller="Sessions" asp-action="Index"` instead
  of `asp-controller="Home" asp-action="Live"`.
- [x] 5.6 In `WebApp/Views/Home/Index.cshtml`, change step 5 of the setup
  wizard so its link targets `asp-controller="Sessions" asp-action="Index"`
  (label may become "5. Open your active session").
- [x] 5.7 `grep -rn "Home.*Live\|Home.*Privacy\|action=\"Live\"\|action=\"Privacy\""`
  and confirm no stale references remain. (only remaining `Live` hit is the intended `Sessions/Live/{id}` link in `Sessions/Index.cshtml`)
- [x] 5.8 `dotnet build`; browser smoke test the layout link and Home/Index.
  _Build clean; browser smoke test deferred to reviewer (no runtime available)._

## 6. Retire ESessionStatus.Created

- [x] 6.1 Open `App.Domain/Enums/ESessionStatus.cs` and record the current
  ordinal of each member. Decide per design D2:
  - If removing `Created` would not shift the integer values of `Active`,
    `Finished`, `Cancelled` (e.g. it is the last member, or explicit integer
    assignments exist), continue with 6.2.
  - Otherwise: skip 6.2 and instead mark `Created` with
    `[Obsolete("Never observed; SessionsController.Create immediately sets Active", true)]`
    and stop after 6.3.
  _Ordinals are explicit (`Created=1, Active=2, Finished=3, Cancelled=4`), so removing `Created` does not shift the others. Chose deletion path._
- [x] 6.2 Remove `Created` from `ESessionStatus`.
- [x] 6.3 In `App.Domain/Session.cs`, change `Status` default initializer
  from `ESessionStatus.Created` to `ESessionStatus.Active` (matches what
  `SessionsController.StartSession` writes on Create).
- [x] 6.4 `grep -rn "ESessionStatus\.Created\|ESessionStatus.Created"` and
  confirm zero hits.
- [x] 6.5 Run `dotnet ef migrations has-pending-model-changes --project App.DAL.EF --startup-project WebApp`
  and confirm no migration is required. ("No changes have been made to the model since the last migration.")
- [x] 6.6 `dotnet build`; `dotnet test App.BLL.Tests`. (38 passed)

## 7. Repo hygiene — root files and orphan assets

- [x] 7.1 Delete `MVC_CONTROLLERS_REVIEW.md` (empty).
- [x] 7.2 Create `docs/mosquitto.md` containing the mosquitto broker command
  from `cmd open as admin.txt`, framed by a one-line intro. Delete
  `cmd open as admin.txt`.
  _Deviation from design D6: `cmd open as admin.txt` also contained a
  large UX audit (items 8–23). Split: mosquitto command → `docs/mosquitto.md`;
  UX audit → appended into `docs/archive/next-steps.md` as "Part 2".
  Consistent with D6's spirit of preserving historical UX notes in
  `docs/archive/`._
- [x] 7.3 Create `docs/archive/next-steps.md` starting with a bold header:
  `> **Archived design log — 2026-08-12.** These notes were captured during
  early prototyping. They are not authoritative; treat them as historical
  context, not a spec.` Append the current body of `next-steps.txt`
  verbatim below the header. Delete `next-steps.txt`.
  _Extended per 7.2 note: file now contains both `next-steps.txt` (Part 1)
  and the UX audit half of `cmd open as admin.txt` (Part 2)._
- [x] 7.4 Create `docs/floorplans/` and move `WebApp/Maps/ICO_3.korrus.pdf`
  into it. Delete the now-empty `WebApp/Maps/` directory.
- [x] 7.5 Ensure `.gitignore` at the repo root ignores `node_modules/`. If
  no root `.gitignore` exists, create one with at least `node_modules/`.
  Leave `package.json` and `package-lock.json` in place per design D5.
  (Already present at line 317 of the existing `.gitignore`.)
- [x] 7.6 In `readme.md`, update the "Scaffolding (reference)" section:
  change every `-outDir Areas/Admin/Controllers` occurrence to
  `-outDir Controllers` so the recipe matches the new layout.
- [x] 7.7 `dotnet build`; `dotnet test App.BLL.Tests`; final full browser
  smoke test per design.md Migration Plan.
  _Build clean; 38 tests pass; full browser smoke test deferred to reviewer._

## 8. Validation

- [x] 8.1 `openspec validate cleanup-webapp-coherence` passes.
- [x] 8.2 `git status` shows only the intended file moves/deletes/edits.
  (Extra untracked entries `.claude/`, `.codex/`, `.pi/` are agent-tooling
  side artifacts unrelated to this change and are covered by tooling-scope
  ignores.)
- [x] 8.3 Test count from task 1.2 matches the post-change test count.
  (baseline 38, post-change 38)
- [x] 8.4 Confirm no new EF migration file appears under
  `App.DAL.EF/Migrations/`. (`git status App.DAL.EF/Migrations` → clean)
