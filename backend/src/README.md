# Backend source (.NET 10)

Clean Architecture layout for the image-processing service.

## Projects

| Project | Role |
|---------|------|
| `CloudNativeImageProcessing.Api` | ASP.NET Core HTTP API, Identity, image CRUD |
| `CloudNativeImageProcessing.Application` | Use cases, abstractions, DTOs |
| `CloudNativeImageProcessing.Domain` | Entities, domain rules |
| `CloudNativeImageProcessing.Infrastructure` | EF Core (PostgreSQL), blob storage, Event Hubs, Redis, email hooks |
| `CloudNativeImageProcessing.Worker` | Consumes `image-processing` hub events, applies processing, updates DB/blobs |
| `CloudNativeImageProcessing.AiGenerationWorker` | Consumes `ai-description` events, calls Computer Vision, updates descriptions |

## API surface (representative)

- `GET /health`
- `POST /api/auth/register`, `POST /api/auth/login`
- `GET/POST/DELETE /api/images` and preview routes (bearer auth)

## Run locally

- **Docker (recommended):** from repo root, `docker compose up --build` (see root [`README.md`](../../README.md)).
- **IDE:** set user secrets or environment variables for connection strings and Event Hubs; use `docker-compose-infra.yml` for local Postgres, Redis, Azurite, Event Hubs emulator.

## Ship to Kubernetes

Dockerfiles live next to each host project. Build context is **`backend/src`**. Full push and Helm steps: [`../../devops/README.md`](../../devops/README.md).
