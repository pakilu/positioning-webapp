# Positioning Webapp

ASP.NET Core (net10.0) web application for an Ultra-Wideband (UWB) indoor positioning system. The app ingests raw range measurements from UWB tags over MQTT or USB serial, solves for tag positions in 2D or 3D, persists results to PostgreSQL via Entity Framework Core, and exposes both MVC admin views and a versioned REST API.

## Solution layout

- `App.Domain` — domain entities (Chip, Session, SessionConfig, RawMeasurement, PositionResult, …).
- `App.DAL.EF` — EF Core `AppDbContext`, migrations, repositories.
- `App.BLL` — business logic services (positioning solver, session management, …).
- `App.BLL.Tests` — unit tests for the BLL layer.
- `WebApp` — ASP.NET Core host: MVC controllers, API controllers, SignalR hubs, MQTT ingestion service.

## Prerequisites

- .NET 10 SDK
- PostgreSQL (default connection points at `127.0.0.1:5432`, db `positioning_db`, user/password `postgres`)
- An MQTT broker (default `localhost:1883`) or one USB serial gateway
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
| `Enabled` | If `true`, the webapp connects to the MQTT broker. Set to `false` when using USB serial only. |
| `Host`, `Port` | Broker endpoint. |
| `ClientId` | MQTT client id used by the webapp. |
| `Username`, `Password` | Optional broker credentials (`null` for anonymous). |
| `UseTls` | Enable TLS to the broker. |
| `RawMeasurementTopic` | Topic pattern for incoming tag range measurements (default `uwb/tags/+/measurement`). |
| `ChipRegistrationTopic` | Topic on which chips announce themselves (default `uwb/chips/registration`). |
| `PersistToDatabase` | If `true`, raw measurements received over MQTT are stored to the database. |

### `Serial`
Settings for USB serial ingestion from one ESP32/DW3000 gateway.

| Key | Description |
|---|---|
| `Enabled` | If `true`, the webapp opens the configured serial port and reads JSON lines. |
| `PortName` | Windows COM port, for example `COM3`. |
| `BaudRate` | Must match `Serial.begin(...)` in the firmware. |
| `ReadTimeoutMs` | Read timeout used so shutdown/reconnects stay responsive. |
| `ReconnectDelaySeconds` | Delay before retrying when the port is unavailable. |
| `PersistToDatabase` | If `true`, raw measurements received over serial are stored to the database. |

Each serial payload should be one JSON object per line, written with `Serial.println(...)`.

Registration payload:

```json
{"deviceIdentifier":"TAG-01","macAddress":"AA:BB:CC:11:22:33"}
```

Raw measurement payload:

```json
{"tagDeviceId":"TAG-01","anchorDeviceId":"ANC-01","distance":2.34,"rssi":-81}
```

`sessionId` is optional. If it is omitted, the server tries to resolve the single active session that contains the tag and anchor.

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

## Operator flow

The webapp is organised around three operator surfaces:

1. **Sessions** — list of live and finished sessions; open a session for its live positioning view.
2. **Rooms** — each room is a positioning layout with an optional floor plan. Open a room to place anchors on the plan (drag them to reposition, coordinates persist automatically) and to add anchors or tags via the inline "+ Add anchor" / "+ Add tag" dialogs. The dialog can either pick an existing chip from the pool or register a brand-new chip in one step.
3. **Chips** — the device pool. Each row shows every room the chip is currently placed in (a chip may belong to more than one room at the same time).

A collapsible getting-started wizard on the home page walks new operators
through the four setup steps (register chips → create a room → place
anchors → start a session) and auto-collapses once all four are done.

Debug surfaces (raw MQTT/serial feed, `RawMeasurement` and
`PositionResult` tables) are still available under the "Debug" dropdown
on the right side of the navbar.

## Scaffolding (reference)

The MVC controllers (in `Controllers/`) were generated with:

```bash
dotnet aspnet-codegenerator controller -name ChipsController                -m Chip                -actions -dc AppDbContext -outDir Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name PositionResultsController      -m PositionResult      -actions -dc AppDbContext -outDir Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name RawMeasurementsController      -m RawMeasurement      -actions -dc AppDbContext -outDir Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name SessionsController             -m Session             -actions -dc AppDbContext -outDir Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name SessionConfigsController       -m SessionConfig       -actions -dc AppDbContext -outDir Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
dotnet aspnet-codegenerator controller -name SessionConfigChipsController   -m SessionConfigChip   -actions -dc AppDbContext -outDir Controllers --useDefaultLayout --useAsyncActions --referenceScriptLibraries -f
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
