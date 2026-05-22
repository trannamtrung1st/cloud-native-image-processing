# Terraform — Azure infrastructure

Provisions CNIP **infrastructure** (RG, ACR, AKS, data plane, Key Vault store, App Configuration store, monitoring, ingress). **Seeds** Key Vault secrets and App Configuration keys once; ongoing values are managed in **Azure Portal**. **Helm deploy** reads live App Configuration and syncs Key Vault via CSI.

Parent guide: [`../README.md`](../README.md).

## Three tfvars files

Terraform auto-loads `*.auto.tfvars` (gitignored). Copy from the `.example` files.

| File | CI source | Purpose |
|------|-----------|---------|
| [`config.auto.tfvars`](config.auto.tfvars.example) | Variable `TERRAFORM_CONFIG_TFVARS` | **Infra** — RG, AKS, Redis, Front Door, monitor flags, `kubernetes_namespace`, `key_vault_additional_admin_principal_ids` (Key Vault Administrator + App Configuration Data Owner) |
| [`app.auto.tfvars`](app.auto.tfvars.example) | Variable `TERRAFORM_APP_TFVARS` | **App settings** — initial `cnip_app_settings` → App Configuration (label `cnip`) |
| [`vault-secrets.auto.tfvars`](vault-secrets.auto.tfvars.example) | Secret `TERRAFORM_VAULT_SECRETS_TFVARS` | **Vault secrets** — initial `cnip_vault_secrets_init` (e.g. Computer Vision) |

**Local:**

```bash
cp config.auto.tfvars.example config.auto.tfvars
cp app.auto.tfvars.example app.auto.tfvars
cp vault-secrets.auto.tfvars.example vault-secrets.auto.tfvars
terraform init
terraform apply -parallelism=10
```

CI uses `-parallelism=10` on plan/apply (10 concurrent resource operations).

After the first apply, change app settings and secret **values** in Azure Portal. Terraform uses `lifecycle { ignore_changes = [value] }` on App Configuration keys and Key Vault secrets so later applies do not overwrite portal edits.

## What Terraform still owns vs portal

| Resource | Terraform creates | Value updates |
|----------|-------------------|---------------|
| App Configuration store + keys | Yes | Portal after init (`ignore_changes` on key values) |
| Key Vault + secret names | Yes | Portal after init (`ignore_changes` on secret values) |
| Platform connection strings in KV | Seeded from Azure resources on first apply | Portal / rotation outside Terraform |

## `cnip_app_settings`

Non-sensitive app + Helm settings (ConfigMap env vars, replica counts, App Insights flag). Keys: `cnip/app/*`, `cnip/helm/*`. Deploy: [`../scripts/appconfig-to-helm-values.py`](../scripts/appconfig-to-helm-values.py).

## `cnip_vault_secrets_init`

Optional secrets only (Computer Vision). Platform secrets (`postgres-connection-string`, `redis-connection-string`, etc.) are still **created** by Terraform on first apply but not updated after init.

## Helm deploy (runtime)

- **App Configuration** → `appconfig.overrides.yaml` → Helm `config` + replicas
- **Key Vault** → `keyVault.enabled` + CSI → `cnip-app-secrets` in the cluster (no tfvars at deploy time)

## Remote state (CI)

[`terraform-manual.yml`](../../.github/workflows/terraform-manual.yml): `TERRAFORM_USE_REMOTE_STATE=true`, `TF_STATE_*` secrets, plus the three tfvars sources above.

## Outputs

```bash
terraform output -raw app_configuration_name
terraform output -raw key_vault_name
terraform output -raw aks_kube_config_command
```

## Editing values after bootstrap

| Change type | Where to edit | Then |
|-------------|---------------|------|
| CORS, replicas, demo delays, hub names | App Configuration (label `cnip`) | Redeploy Helm (CI or `appconfig-to-helm-values.py` + `helm upgrade`) |
| Connection strings, Computer Vision keys | Key Vault secrets | Bump `cnip/helm/deployment_reload_trigger` in App Configuration, then redeploy Helm |
| AKS nodes, Front Door, monitor toggles | `config.auto.tfvars` | `terraform apply` |

Key and secret name reference: [`../README.md` — Managing configuration in Azure Portal](../README.md#managing-configuration-in-azure-portal).

Re-running `terraform apply` does **not** reset App Configuration or Key Vault secret values if you changed them in the Portal (`ignore_changes` on `value`).

## Destroy

```bash
terraform destroy
```
