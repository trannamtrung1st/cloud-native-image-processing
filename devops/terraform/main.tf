resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location
  tags     = var.tags
}

resource "random_string" "storage_suffix" {
  length  = 6
  lower   = true
  upper   = false
  numeric = true
  special = false
}

resource "random_string" "redis_suffix" {
  length  = 8
  lower   = true
  upper   = false
  numeric = true
  special = false
}

resource "random_password" "postgres" {
  length  = 24
  special = true
}

locals {
  # Storage account: 3–24 chars, lowercase letters and numbers only
  sa_prefix            = substr(replace(lower(var.prefix), "-", ""), 0, 10)
  storage_account_name = local.sa_prefix == "" ? "stg${random_string.storage_suffix.result}" : "${local.sa_prefix}${random_string.storage_suffix.result}"
  acr_name             = substr(replace("${lower(var.prefix)}acr", "-", ""), 0, 50)
  aks_name             = "${var.prefix}-aks"
  pg_name              = "${var.prefix}-pg"
  redis_name           = substr("${replace(lower(var.prefix), "-", "")}r${random_string.redis_suffix.result}", 0, 63)
  eh_namespace         = "${var.prefix}-eh"
}

resource "azurerm_container_registry" "main" {
  name                = local.acr_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  admin_enabled       = true
  tags                = var.tags
}

data "azurerm_kubernetes_service_versions" "current" {
  location = azurerm_resource_group.main.location
}

resource "azurerm_kubernetes_cluster" "main" {
  name                = local.aks_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  dns_prefix          = local.aks_name
  kubernetes_version  = data.azurerm_kubernetes_service_versions.current.latest_version
  tags                = var.tags

  default_node_pool {
    name            = "default"
    node_count      = var.aks_node_count
    vm_size         = var.aks_vm_size
    os_disk_size_gb = 60
  }

  identity {
    type = "SystemAssigned"
  }

  oidc_issuer_enabled       = true
  workload_identity_enabled = true

  key_vault_secrets_provider {
    secret_rotation_enabled  = true
    secret_rotation_interval = "2m"
  }

  network_profile {
    network_plugin    = "kubenet"
    load_balancer_sku = "standard"
  }
}

resource "azurerm_role_assignment" "aks_acr_pull" {
  principal_id                     = azurerm_kubernetes_cluster.main.kubelet_identity[0].object_id
  role_definition_name             = "AcrPull"
  scope                            = azurerm_container_registry.main.id
  skip_service_principal_aad_check = true
}

resource "azurerm_postgresql_flexible_server" "main" {
  name                          = local.pg_name
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  version                       = "16"
  administrator_login           = var.postgres_admin_login
  administrator_password        = random_password.postgres.result
  storage_mb                    = 32768
  sku_name                      = var.postgres_sku_name
  public_network_access_enabled = true
  tags                          = var.tags

  # Azure assigns zone after create; changing zone without HA standby swap is rejected by the API.
  lifecycle {
    ignore_changes = [zone]
  }
}

resource "azurerm_postgresql_flexible_server_database" "app" {
  name      = var.postgres_database_name
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "azure" {
  name             = "allow-azure-internal"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# Azure Managed Redis (not legacy Azure Cache for Redis). See https://learn.microsoft.com/azure/redis/overview
resource "azurerm_managed_redis" "main" {
  name                      = local.redis_name
  location                  = azurerm_resource_group.main.location
  resource_group_name       = azurerm_resource_group.main.name
  sku_name                  = var.managed_redis_sku_name
  high_availability_enabled = var.managed_redis_high_availability
  public_network_access     = "Enabled"
  tags                      = var.tags

  default_database {
    access_keys_authentication_enabled = true
    client_protocol                    = "Encrypted"
    clustering_policy                  = "NoCluster"
  }
}

resource "azurerm_storage_account" "main" {
  name                     = local.storage_account_name
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
  tags                     = var.tags
}

resource "azurerm_storage_container" "images" {
  name                  = "images"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_storage_container" "eh_checkpoints" {
  name                  = "eh-checkpoints"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_eventhub_namespace" "main" {
  name                = local.eh_namespace
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = var.eventhub_sku
  capacity            = var.eventhub_capacity
  tags                = var.tags
}

resource "azurerm_eventhub" "image_processing" {
  name              = "image-processing"
  namespace_id      = azurerm_eventhub_namespace.main.id
  partition_count   = 2
  message_retention = 1
}

resource "azurerm_eventhub" "ai_description" {
  name              = "ai-description"
  namespace_id      = azurerm_eventhub_namespace.main.id
  partition_count   = 2
  message_retention = 1
}

resource "azurerm_eventhub_consumer_group" "image_processing_cg1" {
  name                = "cg1"
  namespace_name      = azurerm_eventhub_namespace.main.name
  eventhub_name       = azurerm_eventhub.image_processing.name
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_eventhub_consumer_group" "ai_description_cg1" {
  name                = "cg1"
  namespace_name      = azurerm_eventhub_namespace.main.name
  eventhub_name       = azurerm_eventhub.ai_description.name
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_eventhub_namespace_authorization_rule" "app" {
  name                = "app-send-listen"
  namespace_name      = azurerm_eventhub_namespace.main.name
  resource_group_name = azurerm_resource_group.main.name
  listen              = true
  send                = true
  manage              = false
}
