## Backend Source

This folder contains the .NET 10 backend scaffold following Clean Architecture:

- `CloudNativeImageProcessing.Api`: ASP.NET Core Web API (minimal API endpoints).
- `CloudNativeImageProcessing.Application`: application services and use-case DTOs.
- `CloudNativeImageProcessing.Domain`: core entities and enums.
- `CloudNativeImageProcessing.Infrastructure`: repository + integration adapters (currently in-memory stubs).
- `CloudNativeImageProcessing.Worker`: separate process that consumes `image-processing` Event Hub events, applies image operations (e.g. grayscale), overwrites blobs, and updates Postgres.

## Implemented API Endpoints

- `GET /health`
- `POST /api/auth/register`
- `POST /api/auth/login?useCookies=false&useSessionCookies=false`
- `GET /api/images`
- `GET /api/images/{id}`
- `GET /api/images/{id}/preview`
- `POST /api/images`
- `DELETE /api/images/{id}`

## Notes

- PostgreSQL is now configured as the storage backend via EF Core + Npgsql.
- Auth is implemented with Microsoft Identity Framework (ASP.NET Core Identity) + EF Core code-first.
- Protected image endpoints require bearer token from `/api/auth/login`.
- Docker support is included:
  - API Dockerfile: `CloudNativeImageProcessing.Api/Dockerfile`
  - Image processing worker Dockerfile: `CloudNativeImageProcessing.Worker/Dockerfile`
  - Compose stack: repository root `docker-compose.yml` (uses `postgres:16.4-alpine`, includes `image-processing-worker`)
- Run locally with containers:
  - from repository root: `docker compose up --build`
