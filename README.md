# Flowmetry

Cashflow intelligence system for SMBs.

## Tech Stack
- .NET 8 (Clean Architecture, CQRS)
- React (Vite)
- PostgreSQL (Neon)

## Features
- Invoice lifecycle tracking
- Payment reminders
- Cashflow analytics

## Environment Variables

### API (`Flowmetry.API`)

| Variable | Required | Description |
|---|---|---|
| `DATABASE_URL` | Yes | Neon Postgres connection string. Must be set before running the API. |
| `ALLOWED_ORIGIN_VERCEL` | No | Production Vercel URL to include in the CORS policy. |

`DATABASE_URL` must be set to a valid Neon Postgres connection string before starting the API. If it is missing, the application will throw a configuration exception at startup.

Example:
```
DATABASE_URL=postgresql://user:password@ep-xxx.us-east-1.aws.neon.tech/neondb?sslmode=require
```
