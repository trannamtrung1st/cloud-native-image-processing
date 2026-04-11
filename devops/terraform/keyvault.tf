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

resource "azurerm_key_vault_secret" "postgres_connection_string" {
  name         = "postgres-connection-string"
  value        = local.postgres_connection_string
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]
}

resource "azurerm_key_vault_secret" "blob_storage_connection_string" {
  name         = "blob-storage-connection-string"
  value        = azurerm_storage_account.main.primary_connection_string
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]
}

resource "azurerm_key_vault_secret" "eventhub_connection_string_image" {
  name         = "eventhub-connection-string-image-processing"
  value        = azurerm_eventhub_namespace_authorization_rule.app.primary_connection_string
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]
}

resource "azurerm_key_vault_secret" "eventhub_connection_string_ai" {
  name         = "eventhub-connection-string-ai-description"
  value        = azurerm_eventhub_namespace_authorization_rule.app.primary_connection_string
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]
}

resource "azurerm_key_vault_secret" "redis_connection_string" {
  name         = "redis-connection-string"
  value        = local.redis_connection_string
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]
}

resource "azurerm_key_vault_secret" "computer_vision_endpoint" {
  name         = "computer-vision-endpoint"
  value        = var.computer_vision_endpoint
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]
}

resource "azurerm_key_vault_secret" "computer_vision_api_key" {
  name         = "computer-vision-api-key"
  value        = var.computer_vision_api_key
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]
}

resource "azurerm_federated_identity_credential" "cnip_workload" {
  name                = "${var.prefix}-fed-cnip-workload"
  resource_group_name = azurerm_resource_group.main.name
  parent_id           = azurerm_user_assigned_identity.workload.id
  audience            = ["api://AzureADTokenExchange"]
  issuer              = azurerm_kubernetes_cluster.main.oidc_issuer_url
  subject             = "system:serviceaccount:${var.kubernetes_namespace}:${var.workload_service_account_name}"

  depends_on = [
    azurerm_kubernetes_cluster.main,
    azurerm_user_assigned_identity.workload,
  ]
}
