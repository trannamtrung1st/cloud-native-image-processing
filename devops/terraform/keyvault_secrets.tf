# Key Vault secret *values* are seeded on first apply, then managed in Azure Portal (lifecycle ignore_changes).
# Platform connection strings are still created by Terraform from Azure resources; values are not overwritten after init.

resource "azurerm_key_vault_secret" "postgres_connection_string" {
  name         = "postgres-connection-string"
  value        = local.postgres_connection_string
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]

  lifecycle {
    ignore_changes = [value]
  }
}

resource "azurerm_key_vault_secret" "blob_storage_connection_string" {
  name         = "blob-storage-connection-string"
  value        = azurerm_storage_account.main.primary_connection_string
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]

  lifecycle {
    ignore_changes = [value]
  }
}

resource "azurerm_key_vault_secret" "eventhub_connection_string_image" {
  name         = "eventhub-connection-string-image-processing"
  value        = azurerm_eventhub_namespace_authorization_rule.app.primary_connection_string
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]

  lifecycle {
    ignore_changes = [value]
  }
}

resource "azurerm_key_vault_secret" "eventhub_connection_string_ai" {
  name         = "eventhub-connection-string-ai-description"
  value        = azurerm_eventhub_namespace_authorization_rule.app.primary_connection_string
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]

  lifecycle {
    ignore_changes = [value]
  }
}

resource "azurerm_key_vault_secret" "redis_connection_string" {
  name         = "redis-connection-string"
  value        = local.redis_connection_string
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]

  lifecycle {
    ignore_changes = [value]
  }
}

resource "azurerm_key_vault_secret" "computer_vision_endpoint" {
  name         = "computer-vision-endpoint"
  value        = var.cnip_vault_secrets_init.computer_vision_endpoint
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]

  lifecycle {
    ignore_changes = [value]
  }
}

resource "azurerm_key_vault_secret" "computer_vision_api_key" {
  name         = "computer-vision-api-key"
  value        = var.cnip_vault_secrets_init.computer_vision_api_key
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]

  lifecycle {
    ignore_changes = [value]
  }
}
