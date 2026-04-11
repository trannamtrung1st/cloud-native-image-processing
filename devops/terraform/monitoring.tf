# Azure Monitor: Log Analytics workspace + resource diagnostics (platform logs/metrics).
# https://learn.microsoft.com/azure/azure-monitor/

locals {
  monitor_enabled = var.enable_azure_monitor
}

resource "random_string" "law_suffix" {
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

  name                       = "${var.prefix}-aks-diag"
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

  name                       = "${var.prefix}-kv-diag"
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

  name                       = "${var.prefix}-acr-diag"
  target_resource_id         = azurerm_container_registry.main.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main[0].id

  enabled_log {
    category = "ContainerRegistryRepositoryEvents"
  }

  enabled_metric {
    category = "AllMetrics"
  }
}
