# DevOps

Single guide for **local Docker Compose** and **production on Azure (Terraform + Helm)**. Helm on Azure uses **Key Vault + workload identity + CSI** only—no manual `kubectl create secret` for app credentials.

| Path                                                                         | Purpose                                                                                                                                                         |
| ---------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`terraform/`](terraform/)                                                   | Azure: RG, ACR, AKS, data plane, Key Vault, **App Configuration**, monitoring, optional ingress / Front Door — see [`terraform/README.md`](terraform/README.md) |
| [`scripts/`](scripts/)                                                       | [`export-compose-env-from-terraform.sh`](scripts/export-compose-env-from-terraform.sh), [`appconfig-to-helm-values.py`](scripts/appconfig-to-helm-values.py)    |
| [`helm/cloud-native-image-processing/`](helm/cloud-native-image-processing/) | Chart: API, workers, optional frontend                                                                                                                          |

---

## Configuration (three tfvars + portal)

| Layer             | Tfvars file                 | Terraform                          | Runtime source                                                         |
| ----------------- | --------------------------- | ---------------------------------- | ---------------------------------------------------------------------- |
| **Infra**         | `config.auto.tfvars`        | Creates/updates Azure resources    | N/A                                                                    |
| **App settings**  | `app.auto.tfvars`           | Seeds App Configuration keys once  | **Azure Portal** → deploy reads App Config → Helm ConfigMap + replicas |
| **Vault secrets** | `vault-secrets.auto.tfvars` | Seeds Key Vault secret values once | **Azure Portal** → Helm CSI syncs KV → pod `envFrom`                   |

Terraform does **not** overwrite App Configuration or Key Vault **values** after the first apply (`ignore_changes`). Change them in Azure Portal; redeploy to pick up App Config changes in Helm.

**Local:** copy all three examples in [`terraform/`](terraform/) (see [`terraform/README.md`](terraform/README.md)).

**GitHub Actions:** `TERRAFORM_CONFIG_TFVARS`, `TERRAFORM_APP_TFVARS`, `TERRAFORM_VAULT_SECRETS_TFVARS` — [`.github/workflows/terraform-manual.yml`](../.github/workflows/terraform-manual.yml) (plan/apply), [`.github/workflows/terraform-destroy.yml`](../.github/workflows/terraform-destroy.yml) (plan-destroy/destroy).

**Migrating:** split old `TERRAFORM_TFVARS` / `TERRAFORM_SECRETS_TFVARS` into infra (`config`), `cnip_app_settings` (`app`), and `cnip_vault_secrets_init` (`vault-secrets`). Move `key_vault_additional_admin_principal_ids` to **config** only.

---

## GitHub Actions (CI)

Branch → environment: `main` → **Production**, `develop` → **Staging**, other → **Development**.

| Name                                                                                                     | Type                 | Used by                                                                                                                                                                                                          |
| -------------------------------------------------------------------------------------------------------- | -------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `TERRAFORM_USE_REMOTE_STATE`                                                                             | Variable             | Terraform + Deploy (must be `true` for CI)                                                                                                                                                                       |
| `TERRAFORM_STATE`                                                                                        | Variable (JSON)      | Terraform + Deploy — parsed by [`.github/actions/load-terraform-backend`](../.github/actions/load-terraform-backend); see [`terraform-state.github.json.example`](terraform/terraform-state.github.json.example) |
| `USE_TERRAFORM_OUTPUTS`                                                                                  | Variable             | Deploy — read ACR/AKS/KV/App Config from remote state (recommended `true`)                                                                                                                                       |
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`                                            | Variables            | Terraform + Deploy OIDC                                                                                                                                                                                          |
| `TERRAFORM_CONFIG_TFVARS`                                                                                | Variable             | Terraform — `config.auto.tfvars` (infra)                                                                                                                                                                         |
| `TERRAFORM_APP_TFVARS`                                                                                   | Variable             | Terraform — `app.auto.tfvars` (app settings seed)                                                                                                                                                                |
| `TF_STATE_LOCATION`                                                                                      | Variable (optional)  | Terraform bootstrap only (default `southeastasia`)                                                                                                                                                               |
| `K8S_NAMESPACE`                                                                                          | Variable (optional)  | Deploy Helm namespace (default `cnip`)                                                                                                                                                                           |
| `TERRAFORM_VAULT_SECRETS_TFVARS`                                                                         | Secret               | Terraform — `vault-secrets.auto.tfvars` (Computer Vision keys, etc.)                                                                                                                                             |
| `ACR_NAME`, `CNIP_*`, `AKS_*`, `KEY_VAULT_NAME`, `WORKLOAD_IDENTITY_CLIENT_ID`, `APP_CONFIGURATION_NAME` | Variables (optional) | Deploy fallbacks when `USE_TERRAFORM_OUTPUTS` is not `true`                                                                                                                                                      |

**Migrating:** replace four `TF_STATE_*` secrets + `AZURE_*` secrets with variables; add one `TERRAFORM_STATE` JSON variable.

Deploy [`.github/workflows/deploy-main-azure.yml`](../.github/workflows/deploy-main-azure.yml): **App Configuration** → `appconfig.overrides.yaml` → Helm; **Key Vault** → CSI at pod start (not tfvars).

**Teardown:** [`.github/workflows/terraform-destroy.yml`](../.github/workflows/terraform-destroy.yml) — `plan-destroy` / `destroy` (confirmation `destroy`). By default also deletes the `TERRAFORM_STATE` resource group (state storage + tfstate blob). Uncheck **delete_state_resource_group** to keep state for re-apply.

---

## Local development

**Requirements:** Docker and Docker Compose v2 (`docker compose`).

From the **repository root**:

1. **Infra only** (Postgres, Redis, Azurite, Event Hubs emulator) — optional if you use the full stack compose below, which already includes it.

   ```bash
   docker compose -f docker-compose-infra.yml up -d
   ```

2. **API + workers only** (includes infra via `include`):

   ```bash
   docker compose -f docker-compose-backend.yml up -d --build
   ```

3. **Full stack** (API, workers, frontend SPA):

   ```bash
   docker compose up -d --build
   ```

4. **Frontend** is usually at `http://localhost:5173`; API at `http://localhost:8080` (see compose files for ports). Use **Development** settings (`appsettings.Development.json` / user secrets) for local connection strings.

**Rebuild after code changes:** `docker compose build` (or `docker compose -f docker-compose-backend.yml build …`) then `docker compose up -d` again.

---

## Production (Azure AKS)

**Requirements:** [Terraform](https://developer.hashicorp.com/terraform/install) ≥ 1.5, [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) (`az login`), [kubectl](https://kubernetes.io/docs/tasks/tools/), [Helm 3](https://helm.sh/docs/intro/install/), Docker (for building/pushing images to ACR).

### 1. Terraform — provision Azure

```bash
cd devops/terraform
cp config.auto.tfvars.example config.auto.tfvars
cp app.auto.tfvars.example app.auto.tfvars
cp vault-secrets.auto.tfvars.example vault-secrets.auto.tfvars
# Edit the three files (infra, app settings seed, optional vault secrets), then:

terraform init
terraform apply -parallelism=10
```

Terraform seeds App Configuration and Key Vault on first apply; manage **values** in Azure Portal afterward. Deploy reads live App Config and Key Vault (CSI) into Helm.

**Merge AKS credentials into kubeconfig** (required before `kubectl` / `helm` from your laptop). From repo **root** after a successful apply:

```bash
terraform -chdir=devops/terraform output -raw aks_kube_config_command | sh
```

That runs `az aks get-credentials … --overwrite-existing` and merges the cluster into your default kubeconfig (`~/.kube/config`). Alternatively, run the command printed by `terraform -chdir=devops/terraform output aks_kube_config_command` yourself. Confirm access:

```bash
kubectl config current-context
kubectl get nodes
```

Terraform installs **ingress-nginx** by default (`enable_public_nginx_ingress`).

**Optional — install ingress-nginx yourself:** If you set `enable_public_nginx_ingress = false` in `config.auto.tfvars` (or use a cluster without the Terraform-managed controller), install the chart so the CNIP Helm `ingress.className: nginx` resolves. Example:

```bash
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update
helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx \
  --create-namespace
```

See the [ingress-nginx deployment docs](https://kubernetes.github.io/ingress-nginx/deploy/) for provider-specific notes (LoadBalancer, DNS, TLS).

### 2. Scripts — env for image build/push + ACR login

From repo **root** (adjust `CNIP_IMAGE_TAG` to match image tags in [`values.yaml`](helm/cloud-native-image-processing/values.yaml)):

```bash
. devops/scripts/export-compose-env-from-terraform.sh
export CNIP_IMAGE_TAG=1.0.5-cnip   # must match Helm image tags

az acr login -n "$(terraform -chdir=devops/terraform output -raw acr_name)"
```

Build and push Linux **amd64** images for AKS:

```bash
docker compose build api image-processing-worker ai-generation-worker frontend
docker compose push api image-processing-worker ai-generation-worker frontend
```

Backend-only images (no frontend container):

```bash
docker compose -f docker-compose-backend.yml build api image-processing-worker ai-generation-worker
docker compose -f docker-compose-backend.yml push api image-processing-worker ai-generation-worker
```

`CNIP_PUBLIC_APP_URL` from the script should match your real browser origin. Add that origin to **App Configuration** key `cnip/app/cors_allowed_origins` in Azure Portal (JSON array), then redeploy.

### 3. Helm — `values.yaml` + App Configuration overrides

Base chart: [`helm/cloud-native-image-processing/values.yaml`](helm/cloud-native-image-processing/values.yaml). On Azure, non-secret app settings and replica counts come from **App Configuration** via `appconfig.overrides.yaml` (not from editing `values.yaml` alone).

Requires `az login`. Terraform grants the apply principal **App Configuration Data Reader** and **Data Owner**; deploy needs Reader (included with Owner).

```bash
export K8S_NAMESPACE="cnip"   # must match Terraform kubernetes_namespace

python3 devops/scripts/appconfig-to-helm-values.py \
  --app-config-name "$(terraform -chdir=devops/terraform output -raw app_configuration_name)" \
  --label cnip \
  -o appconfig.overrides.yaml

helm upgrade --install cnip ./devops/helm/cloud-native-image-processing \
  --namespace "$K8S_NAMESPACE" \
  --create-namespace \
  -f ./devops/helm/cloud-native-image-processing/values.yaml \
  -f appconfig.overrides.yaml \
  --set keyVault.enabled=true \
  --set-string acrLoginServer="$(terraform -chdir=devops/terraform output -raw acr_login_server)" \
  --set-string keyVault.tenantId="$(terraform -chdir=devops/terraform output -raw azure_tenant_id)" \
  --set-string keyVault.vaultName="$(terraform -chdir=devops/terraform output -raw key_vault_name)" \
  --set-string keyVault.workloadIdentityClientId="$(terraform -chdir=devops/terraform output -raw workload_identity_client_id)"
```

Tune replica counts and **`cnip/helm/application_insights_enabled`** in **App Configuration** (Portal), then redeploy. **Do not** create `cnip-app-secrets` manually; CSI creates it from Key Vault. Wait briefly after the first pods schedule for CSI sync.

**Verify:**

```bash
kubectl get pods,svc,ingress -n "$K8S_NAMESPACE"
```

---

## Optional reference

| Topic                     | Notes                                                                                                                                                                                            |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Ingress without Terraform | Set `enable_public_nginx_ingress = false` and install [ingress-nginx](https://kubernetes.github.io/ingress-nginx/deploy/) manually (see optional step in §1).                                    |
| Terraform outputs         | `terraform -chdir=devops/terraform output` — connection strings, Key Vault name, ingress URL, ACR.                                                                                               |
| Remote state              | Local: default backend. CI: `TERRAFORM_USE_REMOTE_STATE` + `TERRAFORM_STATE` JSON (see [GitHub Actions](#github-actions-ci)).                                                                    |
| Destroy                   | CI: **Terraform (destroy)** workflow. Local: `terraform destroy`, then `az group delete -n <TERRAFORM_STATE.resource_group_name> -y` to remove state storage.                                                                 |
| Front Door / WAF          | `enable_azure_front_door` in `config.auto.tfvars`; align Helm ingress/CORS with `terraform output -raw cdn_frontdoor_endpoint_url`.                                                              |
| App / Helm config         | `app.auto.tfvars` seeds App Config; edit in Portal; deploy reads into `appconfig.overrides.yaml`.                                                                                                |
| Vault secrets             | `vault-secrets.auto.tfvars` seeds optional secrets; platform strings seeded once; edit in Portal.                                                                                                |
| TLS                       | e.g. cert-manager + `ingress.tls` in values.                                                                                                                                                     |
| API replicas > 1          | Shared Data Protection keys required — see chart comments / backend docs.                                                                                                                        |
| AKS diagnostic settings   | One Terraform setting `{prefix}-aks-diag` when `enable_azure_monitor=true`. If apply fails with “limit of 5”, delete extra settings on the cluster in Portal (Monitoring → Diagnostic settings). |

**End-to-end flow:** three tfvars → Terraform (infra + seed KV/App Config) → Portal owns values → deploy: App Config → Helm ConfigMap/replicas; Key Vault → CSI → `envFrom` on pods.

---

## Managing configuration in Azure Portal

After the first successful `terraform apply`, treat **tfvars as bootstrap only**. Change runtime values in the Portal, then **re-run Deploy to Azure** (or local `appconfig-to-helm-values.py` + `helm upgrade`) so App Configuration changes reach the cluster. Key Vault changes are picked up by CSI on pod restart (or after CSI refresh).

### App Configuration (label `cnip`)

Portal: **App Configuration** → your store (`terraform output -raw app_configuration_name`) → **Configuration explorer** → filter **Label** = `cnip`. Your user needs **App Configuration Data Owner** (included if your Object ID is in `key_vault_additional_admin_principal_ids` in `config.auto.tfvars`).

| Key                                                  | Helm / app effect                                                                                        |
| ---------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| `cnip/app/aspnetcore_environment`                    | ConfigMap `ASPNETCORE_ENVIRONMENT`                                                                       |
| `cnip/app/dotnet_environment`                        | ConfigMap `DOTNET_ENVIRONMENT`                                                                           |
| `cnip/app/blob_container_name`                       | `BlobStorage__ContainerName`                                                                             |
| `cnip/app/eventhub_*`                                | Event hub names / consumer groups                                                                        |
| `cnip/app/demo_*`                                    | Demo delay env vars                                                                                      |
| `cnip/app/upload_max_request_body_bytes`             | Upload limit                                                                                             |
| `cnip/app/identity_bearer_token_hours`               | Token TTL                                                                                                |
| `cnip/app/redis_details_expiration_minutes`          | Redis cache TTL                                                                                          |
| `cnip/app/cors_allowed_origins`                      | JSON array string, e.g. `["https://my.host"]`                                                            |
| `cnip/helm/api_replica_count`                        | `api.replicaCount`                                                                                       |
| `cnip/helm/worker_replica_count`                     | `worker.replicaCount`                                                                                    |
| `cnip/helm/ai_worker_replica_count`                  | `aiWorker.replicaCount`                                                                                  |
| `cnip/helm/frontend_replica_count`                   | `frontend.replicaCount`                                                                                  |
| `cnip/helm/application_insights_enabled`             | `true` or `false`                                                                                        |
| `cnip/helm/frontend_images_refresh_interval_seconds` | Frontend poll interval (also used at image build in CI)                                                  |
| `cnip/helm/deployment_reload_trigger`                | Bump (e.g. `1` → `2`) then redeploy to restart pods and reload Key Vault secrets from `cnip-app-secrets` |

### Key Vault secrets

Portal: **Key Vault** → **Secrets** (`terraform output -raw key_vault_name`). Helm does not read these at deploy time; the **Secrets Store CSI** driver syncs them into Kubernetes secret `cnip-app-secrets`.

| Secret name                                   | Set by                                              |
| --------------------------------------------- | --------------------------------------------------- |
| `postgres-connection-string`                  | Terraform (first apply); edit in Portal if needed   |
| `blob-storage-connection-string`              | Terraform (first apply)                             |
| `eventhub-connection-string-image-processing` | Terraform (first apply)                             |
| `eventhub-connection-string-ai-description`   | Terraform (first apply)                             |
| `redis-connection-string`                     | Terraform (first apply)                             |
| `application-insights-connection-string`      | Terraform when `enable_application_insights = true` |
| `computer-vision-endpoint`                    | Initial `vault-secrets.auto.tfvars`; then Portal    |
| `computer-vision-api-key`                     | Initial `vault-secrets.auto.tfvars`; then Portal    |

After editing Key Vault secret **values**, wait ~2 minutes for CSI sync (or confirm the Kubernetes secret), then bump **`cnip/helm/deployment_reload_trigger`** in App Configuration and run **Deploy to Azure** (or `appconfig-to-helm-values.py` + `helm upgrade`). That changes Helm checksum annotations and restarts API, workers, AI worker, and frontend so pods pick up the updated `cnip-app-secrets`.

**Infra-only changes** (AKS size, Front Door, new region) still require editing `config.auto.tfvars` and `terraform apply` — not the Portal tables above.
