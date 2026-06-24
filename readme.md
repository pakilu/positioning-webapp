# Positioning Webapp

ASP.NET Core (net10.0) web application for an Ultra-Wideband (UWB) indoor positioning system. The app ingests raw range measurements from UWB tags over MQTT, solves for tag positions in 2D or 3D, persists results to PostgreSQL via Entity Framework Core, and exposes both MVC admin views and a versioned REST API.

## Solution layout

- `App.Domain` — domain entities (Chip, Session, SessionConfig, RawMeasurement, PositionResult, …).
- `App.DAL.EF` — EF Core `AppDbContext`, migrations, repositories.
- `App.BLL` — business logic services (positioning solver, session management, …).
- `App.BLL.Tests` — unit tests for the BLL layer.
- `WebApp` — ASP.NET Core host: MVC controllers, Admin area, API controllers, SignalR hubs, MQTT ingestion service.

## Prerequisites

- .NET 10 SDK
- PostgreSQL (default connection points at `127.0.0.1:5432`, db `positioning_db`, user/password `postgres`)
- An MQTT broker (default `localhost:1883`)
- EF Core CLI tool:

```bash
dotnet tool install dotnet-ef
```

## Database setup

Create the initial migration and apply it:

```bash
dotnet ef migrations add Initial --project App.DAL.EF --startup-project WebApp
dotnet ef database update          --project App.DAL.EF --startup-project WebApp
```

## Running

```bash
dotnet run --project WebApp
```

## Configuration (`WebApp/appsettings.json`)

### `ConnectionStrings:DefaultConnection`
PostgreSQL connection string used by `AppDbContext`.

### `Mqtt`
Settings for the MQTT ingestion client.

| Key | Description |
|---|---|
| `Host`, `Port` | Broker endpoint. |
| `ClientId` | MQTT client id used by the webapp. |
| `Username`, `Password` | Optional broker credentials (`null` for anonymous). |
| `UseTls` | Enable TLS to the broker. |
| `RawMeasurementTopic` | Topic pattern for incoming tag range measurements (default `uwb/tags/+/measurement`). |
| `ChipRegistrationTopic` | Topic on which chips announce themselves (default `uwb/chips/registration`). |
| `PersistToDatabase` | If `true`, raw measurements received over MQTT are stored to the database. |

### `Positioning`
Controls the live positioning pipeline.

- **`MaxMeasurementAge`** (`00:00:03`)
  Maximum age of a range measurement still considered "fresh" when assembling a solver snapshot. Must comfortably exceed one full round-robin through all anchors so the snapshot can see ≥ 3 fresh distances at once. The ping firmware paces measurements with a 500 ms `ROUND_DELAY` between successful anchor rounds, so for 3 anchors a full sweep is ~1.5 s; 3 s gives safe headroom and tolerates an occasional ranging retry.

- **`MinSolveInterval`** (`00:00:00.080`)
  Minimum wall-clock gap between successive position solves per tag. `80 ms` ≈ ~12 Hz fix rate. Set to `"00:00:00"` to solve on every message.

- **`PersistResults`**
  If `true`, computed `PositionResult` rows are written to the database.

- **`Solver:Mode`** — `"TwoD"` or `"ThreeD"`.
  `TwoD` needs ≥ 3 anchors and assumes a fixed tag Z plane; `ThreeD` needs ≥ 4 anchors.

- **`Solver:TagZ`**
  The height plane (in metres) the tag is assumed to lie on in 2D mode. Set to `null` (or omit) to use the mean of the anchor Z coordinates.

A `appsettings.Development.json` is also available for environment-specific overrides.

## Scaffolding (reference)

The MVC admin controllers (in `Areas/Admin/Controllers`) were generated with:

```bash
dotnet aspnet-codegenerator controller -name ChipsController                -m Chip                -actions -dc AppDbContext -outDir Areas/Admin/Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name PositionResultsController      -m PositionResult      -actions -dc AppDbContext -outDir Areas/Admin/Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name RawMeasurementsController      -m RawMeasurement      -actions -dc AppDbContext -outDir Areas/Admin/Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name SessionsController             -m Session             -actions -dc AppDbContext -outDir Areas/Admin/Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name SessionConfigsController       -m SessionConfig       -actions -dc AppDbContext -outDir Areas/Admin/Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name SessionConfigChipsController   -m SessionConfigChip   -actions -dc AppDbContext -outDir Areas/Admin/Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
```

The REST API controllers (in `ApiControllers`) were generated with:

```bash
dotnet aspnet-codegenerator controller -name ChipsController                -m App.Domain.Chip                -dc AppDbContext -outDir ApiControllers -api --useAsyncActions -f
dotnet aspnet-codegenerator controller -name PositionResultsController      -m App.Domain.PositionResult      -dc AppDbContext -outDir ApiControllers -api --useAsyncActions -f
dotnet aspnet-codegenerator controller -name RawMeasurementsController      -m App.Domain.RawMeasurement      -dc AppDbContext -outDir ApiControllers -api --useAsyncActions -f
dotnet aspnet-codegenerator controller -name SessionsController             -m App.Domain.Session             -dc AppDbContext -outDir ApiControllers -api --useAsyncActions -f
dotnet aspnet-codegenerator controller -name SessionConfigsController       -m App.Domain.SessionConfig       -dc AppDbContext -outDir ApiControllers -api --useAsyncActions -f
dotnet aspnet-codegenerator controller -name SessionConfigChipsController   -m App.Domain.SessionConfigChip   -dc AppDbContext -outDir ApiControllers -api --useAsyncActions -f
```

## Tests

```bash
dotnet test App.BLL.Tests
```
