# Azure Monitor: Log Analytics workspace + resource diagnostics (platform logs/metrics).
# https://learn.microsoft.com/azure/azure-monitor/

locals {
  monitor_enabled    = var.enable_azure_monitor
  appinsights_name   = var.application_insights_name != "" ? var.application_insights_name : "${var.prefix}-appinsights"
}

resource "random_string" "law_suffix" {
  length  = 5
  lower   = true
  upper   = false
  numeric = true
  special = false
}

# Unique per Terraform state so platform diagnostic settings never collide with names already in Azure
# when state was empty/lost but resources remained (fixed names like {prefix}-aks-diag always failed).
resource "random_string" "diag_suffix" {
  length  = 5
  lower   = true
  upper   = false
  numeric = true
  special = false
}

resource "azurerm_log_analytics_workspace" "main" {
  count = local.monitor_enabled ? 1 : 0

  name                = "${var.prefix}-law-${random_string.law_suffix.result}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = var.log_analytics_retention_in_days
  tags                = var.tags
}

# AKS — audit, API server, Defender for Cloud (guard), and platform metrics.
resource "azurerm_monitor_diagnostic_setting" "aks" {
  count = local.monitor_enabled ? 1 : 0

  name                       = "${var.prefix}-aks-diag-${random_string.diag_suffix.result}"
  target_resource_id         = azurerm_kubernetes_cluster.main.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main[0].id

  enabled_log {
    category = "kube-audit"
  }
  enabled_log {
    category = "kube-apiserver"
  }
  enabled_log {
    category = "guard"
  }
  enabled_log {
    category = "cluster-autoscaler"
  }

  enabled_metric {
    category = "AllMetrics"
  }
}

resource "azurerm_monitor_diagnostic_setting" "key_vault" {
  count = local.monitor_enabled ? 1 : 0

  name                       = "${var.prefix}-kv-diag-${random_string.diag_suffix.result}"
  target_resource_id         = azurerm_key_vault.main.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main[0].id

  enabled_log {
    category = "AuditEvent"
  }

  enabled_metric {
    category = "AllMetrics"
  }
}

resource "azurerm_monitor_diagnostic_setting" "acr" {
  count = local.monitor_enabled && var.enable_azure_monitor_acr_diagnostics ? 1 : 0

  name                       = "${var.prefix}-acr-diag-${random_string.diag_suffix.result}"
  target_resource_id         = azurerm_container_registry.main.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main[0].id

  enabled_log {
    category = "ContainerRegistryRepositoryEvents"
  }

  enabled_metric {
    category = "AllMetrics"
  }
}

# Application Insights (workspace-based) — SDK telemetry is stored in the linked Log Analytics workspace.
# Portal: App Insights → Transaction search / Logs runs Kusto against workspace tables (e.g. AppTraces, AppRequests),
# not the classic Application Insights-only schema. Allow a few minutes after deploy for ingestion.
# https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview
resource "azurerm_application_insights" "main" {
  count = local.monitor_enabled && var.enable_application_insights ? 1 : 0

  name                       = local.appinsights_name
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  workspace_id               = azurerm_log_analytics_workspace.main[0].id
  application_type           = "web"
  internet_ingestion_enabled = true
  internet_query_enabled     = true
  tags                       = var.tags
}

resource "azurerm_key_vault_secret" "application_insights_connection_string" {
  count = local.monitor_enabled && var.enable_application_insights ? 1 : 0

  name         = "application-insights-connection-string"
  value        = azurerm_application_insights.main[0].connection_string
  key_vault_id = azurerm_key_vault.main.id
  content_type = "text/plain"
  depends_on   = [azurerm_role_assignment.terraform_kv_admin]
}
