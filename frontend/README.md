# Frontend — React + Vite

SPA for **cloud-native-image-processing**: auth, image library, upload, and processing status. Calls the .NET API under `/api` (configure base URL for each environment).

## Prerequisites

- Node.js 20+ (or the version pinned in CI / `.nvmrc` if present)
- npm

## Local development

From `frontend/`:

```bash
npm install
npm run dev
```

Set **`VITE_API_BASE_URL`** to where the API is reachable (e.g. `http://localhost:8080` if the API is on another port, or rely on `docker-compose.yml` build args / Vite defaults).

## Production build

The Docker image is built at the **repository root** with a build-arg so the SPA bakes in the public API URL (see [`../devops/README.md`](../devops/README.md) — ACR / frontend build steps).

```bash
export PUBLIC_APP_URL="https://your-host"   # no trailing slash
docker build -t your-registry/cnip-frontend:1 \
  --build-arg "VITE_API_BASE_URL=${PUBLIC_APP_URL}" \
  -f Dockerfile .
```

## More documentation

- Monorepo overview: [`../README.md`](../README.md)
- Azure, Terraform, Helm: [`../devops/README.md`](../devops/README.md)
