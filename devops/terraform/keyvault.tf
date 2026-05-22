resource "random_string" "kv_suffix" {
  length  = 5
  lower   = true
  upper   = false
  numeric = true
  special = false
}

locals {
  # Key Vault name: 3–24 alphanumeric
  key_vault_name = substr("${replace(lower(var.prefix), "-", "")}kv${random_string.kv_suffix.result}", 0, 24)
}

resource "azurerm_key_vault" "main" {
  name                       = local.key_vault_name
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7
  purge_protection_enabled   = false
  rbac_authorization_enabled = true
  tags                       = var.tags
}

# Deploy principal (user/SP running Terraform) can populate secrets
resource "azurerm_role_assignment" "terraform_kv_admin" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_role_assignment" "additional_kv_admins" {
  for_each = setsubtract(
    toset(var.key_vault_additional_admin_principal_ids),
    toset([data.azurerm_client_config.current.object_id])
  )

  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = each.value
}

resource "azurerm_user_assigned_identity" "workload" {
  name                = "${var.prefix}-kv-workload"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = var.tags
}

resource "azurerm_role_assignment" "workload_kv_secrets_user" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.workload.principal_id
}

resource "azurerm_federated_identity_credential" "cnip_workload" {
  name                      = "${var.prefix}-fed-cnip-workload"
  user_assigned_identity_id = azurerm_user_assigned_identity.workload.id
  audience                  = ["api://AzureADTokenExchange"]
  issuer                    = azurerm_kubernetes_cluster.main.oidc_issuer_url
  subject                   = "system:serviceaccount:${var.kubernetes_namespace}:${var.workload_service_account_name}"

  depends_on = [
    azurerm_kubernetes_cluster.main,
    azurerm_user_assigned_identity.workload,
  ]
}
