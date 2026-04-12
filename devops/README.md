# DevOps

Single guide for **local Docker Compose** and **production on Azure (Terraform + Helm)**. Helm on Azure uses **Key Vault + workload identity + CSI** only—no manual `kubectl create secret` for app credentials.

| Path | Purpose |
|------|---------|
| [`terraform/`](terraform/) | Azure: RG, ACR, AKS, data plane, Key Vault, monitoring, optional ingress / Front Door |
| [`scripts/`](scripts/) | [`export-compose-env-from-terraform.sh`](scripts/export-compose-env-from-terraform.sh) — sets `CNIP_ACR_LOGIN_SERVER`, `CNIP_PUBLIC_APP_URL` from Terraform outputs |
| [`helm/cloud-native-image-processing/`](helm/cloud-native-image-processing/) | Chart: API, workers, optional frontend |

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
   docker compose -f docker-compose.backend.yml up -d --build
   ```

3. **Full stack** (API, workers, frontend SPA):

   ```bash
   docker compose up -d --build
   ```

4. **Frontend** is usually at `http://localhost:5173`; API at `http://localhost:8080` (see compose files for ports). Use **Development** settings (`appsettings.Development.json` / user secrets) for local connection strings.

**Rebuild after code changes:** `docker compose build` (or `docker compose -f docker-compose.backend.yml build …`) then `docker compose up -d` again.

---

## Production (Azure AKS)

**Requirements:** [Terraform](https://developer.hashicorp.com/terraform/install) ≥ 1.5, [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) (`az login`), [kubectl](https://kubernetes.io/docs/tasks/tools/), [Helm 3](https://helm.sh/docs/intro/install/), Docker (for building/pushing images to ACR).

### 1. Terraform — provision Azure

```bash
cd devops/terraform
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars: resource_group_name, location, prefix, kubernetes_namespace, computer_vision_* if using AI worker.

terraform init
terraform apply
```

Set `computer_vision_endpoint` / `computer_vision_api_key` in `terraform.tfvars` **before** apply so Key Vault gets those secrets.

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

**Optional — install ingress-nginx yourself:** If you set `enable_public_nginx_ingress = false` in `terraform.tfvars` (or use a cluster without the Terraform-managed controller), install the chart so the CNIP Helm `ingress.className: nginx` resolves. Example:

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
docker compose -f docker-compose.backend.yml build api image-processing-worker ai-generation-worker
docker compose -f docker-compose.backend.yml push api image-processing-worker ai-generation-worker
```

`CNIP_PUBLIC_APP_URL` from the script should match your real browser origin (Terraform ingress URL, Front Door URL, or custom host). Align Helm `ingress.hosts` and `config.cors.allowedOrigins` with that origin (edit `values.yaml` locally if you prefer, or use `--set` / `--set-string` on the Helm command below).

### 3. Helm — single `values.yaml` + Azure flags

Use only [`helm/cloud-native-image-processing/values.yaml`](helm/cloud-native-image-processing/values.yaml). Enable Key Vault, Application Insights (when Terraform created them), ACR host, and Key Vault IDs from Terraform outputs:

```bash
export K8S_NAMESPACE="cnip"   # must match Terraform kubernetes_namespace

helm upgrade --install cnip ./devops/helm/cloud-native-image-processing \
  --namespace "$K8S_NAMESPACE" \
  --create-namespace \
  -f ./devops/helm/cloud-native-image-processing/values.yaml \
  --set keyVault.enabled=true \
  --set applicationInsights.enabled=true \
  --set-string acrLoginServer="$(terraform -chdir=devops/terraform output -raw acr_login_server)" \
  --set-string keyVault.tenantId="$(terraform -chdir=devops/terraform output -raw azure_tenant_id)" \
  --set-string keyVault.vaultName="$(terraform -chdir=devops/terraform output -raw key_vault_name)" \
  --set-string keyVault.workloadIdentityClientId="$(terraform -chdir=devops/terraform output -raw workload_identity_client_id)"
```

Set **`applicationInsights.enabled=false`** if Terraform has `enable_application_insights = false`. Use `--set-string` for GUIDs. **Do not** create `cnip-app-secrets` manually; CSI creates it from Key Vault. Wait briefly after the first pods schedule for CSI sync.

**Verify:**

```bash
kubectl get pods,svc,ingress -n "$K8S_NAMESPACE"
```

---

## Optional reference

| Topic | Notes |
|-------|--------|
| Ingress without Terraform | Set `enable_public_nginx_ingress = false` and install [ingress-nginx](https://kubernetes.github.io/ingress-nginx/deploy/) manually (see optional step in §1). |
| Terraform outputs | `terraform -chdir=devops/terraform output` — connection strings, Key Vault name, ingress URL, ACR. |
| Remote state | Not configured by default; use an Azure Storage backend for teams. |
| Destroy | `cd devops/terraform && terraform destroy` |
| Front Door / WAF | `enable_azure_front_door` in `terraform.tfvars`; align Helm ingress/CORS with `terraform output -raw cdn_frontdoor_endpoint_url`. |
| TLS | e.g. cert-manager + `ingress.tls` in values. |
| API replicas > 1 | Shared Data Protection keys required — see chart comments / backend docs. |

**End-to-end flow:** Terraform writes secrets to Key Vault and wires AKS workload identity → Helm `SecretProviderClass` → CSI syncs into the Kubernetes `Secret` referenced by `envFrom` on pods.
