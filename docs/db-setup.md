# Shared PostgreSQL setup

The database runs in a Docker container on the school VM and is shared by
all developers. The WebApp itself runs locally on each developer's machine
and connects out to the VM.

## VM side (automated via GitLab CI)

`docker-compose.yml` defines a single `postgres-dev` service. On every push
to `main`, the GitLab runner on the VM executes:

```
docker compose -p positioning up -d --remove-orphans
```

The password is hardcoded in `docker-compose.yml` (`POSTGRES_PASSWORD=...`)
and must match the password used in `WebApp/appsettings.json`.

## Developer laptop setup

Edit `WebApp/appsettings.json` and replace the placeholders in the
`DefaultConnection` string:

```
"DefaultConnection": "Server=<VM_HOSTNAME>;Port=5432;Database=positioning_db;Username=positioning;Password=<PASSWORD>;"
```

- `<VM_HOSTNAME>` — the school VM hostname or IP.
- `<PASSWORD>` — same value as `POSTGRES_PASSWORD` in `docker-compose.yml`.

Then run the app:

```bash
dotnet run --project WebApp
```

> Note: this file is committed to Git, so the password will be visible in
> the repository history. This is acceptable for this school project but
> would not be for a real production system.

## Applying EF migrations

Since everyone shares one database, migrations only need to be applied
**once** after they are added:

```bash
dotnet ef migrations add <Name> --project App.DAL.EF --startup-project WebApp
dotnet ef database update       --project App.DAL.EF --startup-project WebApp
```

Commit the generated files under `App.DAL.EF/Migrations/`.

## Sanity check

From your laptop, once the CI pipeline is green:

```bash
psql -h <VM_HOSTNAME> -U positioning -d positioning_db
```

If it prompts for a password and lets you in, you're done. If it hangs or
times out, the school firewall is blocking inbound 5432 — you would then
need to switch to an SSH tunnel setup.
