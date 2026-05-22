locals {
  app_configuration_name = substr("${replace(lower(var.prefix), "-", "")}appcfg", 0, 50)

  app_configuration_keys = {
    "cnip/app/aspnetcore_environment"                    = var.cnip_app_settings.app.aspnetcore_environment
    "cnip/app/dotnet_environment"                        = var.cnip_app_settings.app.dotnet_environment
    "cnip/app/blob_container_name"                       = var.cnip_app_settings.app.blob_container_name
    "cnip/app/eventhub_image_processing_hub"             = var.cnip_app_settings.app.eventhub_image_processing_hub
    "cnip/app/eventhub_image_processing_cg"              = var.cnip_app_settings.app.eventhub_image_processing_cg
    "cnip/app/eventhub_ai_description_hub"               = var.cnip_app_settings.app.eventhub_ai_description_hub
    "cnip/app/eventhub_ai_description_cg"                = var.cnip_app_settings.app.eventhub_ai_description_cg
    "cnip/app/eventhub_checkpoint_container"             = var.cnip_app_settings.app.eventhub_checkpoint_container
    "cnip/app/demo_get_by_id_delay_ms"                   = var.cnip_app_settings.app.demo_get_by_id_delay_ms
    "cnip/app/demo_grayscale_delay_ms"                   = var.cnip_app_settings.app.demo_grayscale_delay_ms
    "cnip/app/demo_ai_description_delay_ms"              = var.cnip_app_settings.app.demo_ai_description_delay_ms
    "cnip/app/upload_max_request_body_bytes"             = var.cnip_app_settings.app.upload_max_request_body_bytes
    "cnip/app/identity_bearer_token_hours"               = var.cnip_app_settings.app.identity_bearer_token_hours
    "cnip/app/redis_details_expiration_minutes"          = var.cnip_app_settings.app.redis_details_expiration_minutes
    "cnip/app/cors_allowed_origins"                      = jsonencode(var.cnip_app_settings.app.cors_allowed_origins)
    "cnip/helm/api_replica_count"                        = tostring(var.cnip_app_settings.helm.api_replica_count)
    "cnip/helm/worker_replica_count"                     = tostring(var.cnip_app_settings.helm.worker_replica_count)
    "cnip/helm/ai_worker_replica_count"                  = tostring(var.cnip_app_settings.helm.ai_worker_replica_count)
    "cnip/helm/frontend_replica_count"                   = tostring(var.cnip_app_settings.helm.frontend_replica_count)
    "cnip/helm/application_insights_enabled"             = var.cnip_app_settings.helm.application_insights_enabled ? "true" : "false"
    "cnip/helm/frontend_images_refresh_interval_seconds" = var.cnip_app_settings.helm.frontend_images_refresh_interval_seconds
  }
}

resource "azurerm_app_configuration" "main" {
  name                = local.app_configuration_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = var.app_configuration_sku
  tags                = var.tags
}

resource "azurerm_role_assignment" "workload_appconfig_reader" {
  scope                = azurerm_app_configuration.main.id
  role_definition_name = "App Configuration Data Reader"
  principal_id         = azurerm_user_assigned_identity.workload.principal_id
}

# Terraform / CI OIDC: seed keys and manage key metadata; Portal edits by the same principal need Data Owner.
resource "azurerm_role_assignment" "terraform_appconfig_owner" {
  scope                = azurerm_app_configuration.main.id
  role_definition_name = "App Configuration Data Owner"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Deploy workflow (az appconfig kv list) and local read-only tooling.
resource "azurerm_role_assignment" "terraform_appconfig_reader" {
  scope                = azurerm_app_configuration.main.id
  role_definition_name = "App Configuration Data Reader"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Operators in key_vault_additional_admin_principal_ids can edit App Configuration in Portal.
resource "azurerm_role_assignment" "additional_appconfig_owners" {
  for_each = setsubtract(
    toset(var.key_vault_additional_admin_principal_ids),
    toset([data.azurerm_client_config.current.object_id])
  )

  scope                = azurerm_app_configuration.main.id
  role_definition_name = "App Configuration Data Owner"
  principal_id         = each.value
}

resource "azurerm_app_configuration_key" "cnip" {
  for_each = local.app_configuration_keys

  configuration_store_id = azurerm_app_configuration.main.id
  key                    = each.key
  label                  = "cnip"
  value                  = each.value

  depends_on = [
    azurerm_role_assignment.terraform_appconfig_owner,
    azurerm_role_assignment.terraform_appconfig_reader,
    azurerm_role_assignment.workload_appconfig_reader,
    azurerm_role_assignment.additional_appconfig_owners,
  ]

  lifecycle {
    ignore_changes = [value]
  }
}
