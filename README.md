# Flowmetry

Cashflow intelligence system for SMBs.

## Tech Stack

- .NET 9 — Clean Architecture, CQRS via MediatR
- PostgreSQL — Neon (serverless Postgres)
- React + Vite — frontend ([flowmetry-ui](https://github.com/ankitmadridista/flowmetry-ui/blob/main/README.md))

## Features

- Invoice lifecycle tracking (Draft → Sent → Paid / PartiallyPaid / Overdue)
- Payment recording and cumulative status
- Automated payment reminders
- Cashflow analytics dashboard
- Risk scoring per customer

## Running locally

```bash
# Set the database connection string
$env:DATABASE_URL="postgresql://user:password@..."

dotnet run --project Flowmetry.API
```

API starts at `https://localhost:7130`.

## Running tests

```bash
dotnet test
```

The test suite includes unit tests, integration tests, and property-based tests (CsCheck).

## Environment variables

| Variable | Required | Description |
|---|---|---|
| `DATABASE_URL` | Yes | Neon Postgres connection string |
| `ALLOWED_ORIGIN_VERCEL` | No | Production Vercel URL for CORS |

Example:
```
DATABASE_URL=postgresql://user:password@ep-xxx.us-east-1.aws.neon.tech/neondb?sslmode=require
```

## Database migrations

```bash
# Apply all pending migrations
$env:DATABASE_URL="postgresql://..."
dotnet ef database update --project Flowmetry.Infrastructure --startup-project Flowmetry.API
```

## Deployment

The API is deployed on **Render** using Docker.

### Why Render for the backend?

Vercel is not an option for .NET — it only supports serverless JavaScript/Edge functions, not persistent .NET processes. The choice was between Render and Railway:

| | Render | Railway |
|---|---|---|
| .NET / Docker | Yes | Yes |
| Free tier | Runs indefinitely (cold starts) | Caps compute hours per month |
| Persistent server | Yes | Yes |
| Pricing predictability | Flat rate on paid plans | Usage-based, can spike |

Railway's free tier limits compute to a fixed number of hours per month, which means a demo API goes offline mid-month. Render's free tier keeps the service live indefinitely with the tradeoff of cold starts after inactivity — a better fit for a portfolio project that needs to stay accessible.

### Free-tier limitations

- **Cold starts**: Render shuts down the container after ~15 minutes of inactivity. The first request after idle takes 30–60 seconds to respond. Paid plans eliminate this by keeping the instance warm.
- **Single instance**: The free tier runs one container with no auto-scaling. For production traffic, you'd add replicas behind a load balancer or move to a managed container platform (ECS, Cloud Run, etc.).
- **inotify limits**: Render's free tier containers share a low inotify instance limit. .NET's config file watcher can exhaust this on startup. Mitigated by disabling `ReloadOnChange` on all config sources in `Program.cs`.
- **Neon free tier**: Postgres compute is quota-limited. Sustained production load would require a paid Neon plan or a managed RDS/Cloud SQL instance.

These are deliberate tradeoffs for a demo/portfolio deployment — not decisions you'd make for a production system.
