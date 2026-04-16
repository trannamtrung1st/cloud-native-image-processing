variable "location" {
  type        = string
  description = "Azure region (e.g. southeastasia)."
  default     = "southeastasia"
}

variable "resource_group_name" {
  type        = string
  description = "Resource group name for all CNIP infrastructure."
}

variable "prefix" {
  type        = string
  description = "Short name prefix for Azure resources (letters/digits; used inside globally-unique names). Use at least 3 alphanumeric characters (e.g. cnip)."
  default     = "cnip"

  validation {
    # Use regexall (Terraform 1.x) instead of regexreplace for compatibility with older CLI builds.
    condition     = length(regexall("[a-zA-Z0-9]", coalesce(var.prefix, ""))) >= 3
    error_message = "prefix must contain at least 3 letters or digits (hyphens and other punctuation are ignored for this check)."
  }
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to supported resources."
  default     = {}
}

variable "aks_node_count" {
  type        = number
  description = "Initial node count for the AKS default pool."
  default     = 1
}

variable "aks_vm_size" {
  type        = string
  description = "VM size for AKS nodes."
  default     = "Standard_D2s_v3"
}

variable "enable_public_nginx_ingress" {
  type        = bool
  description = "Install ingress-nginx via Helm with a public Azure LoadBalancer (Azure-managed public IP) and DNS label (<label>.<region>.cloudapp.azure.com) for HTTP testing."
  default     = true
}

variable "ingress_nginx_namespace" {
  type        = string
  description = "Kubernetes namespace for the ingress-nginx Helm release."
  default     = "ingress-nginx"
}

variable "ingress_nginx_chart_version" {
  type        = string
  description = "ingress-nginx Helm chart version (https://github.com/kubernetes/ingress-nginx/tree/main/charts/ingress-nginx)."
  default     = "4.14.0"
}

variable "aks_subnet_lb_inbound_nsg_hardening" {
  type        = bool
  default     = false
  description = "When true, add an inbound NSG rule on the primary NSG in the AKS node resource group (AzureLoadBalancer -> TCP 80,443,30000-32767). Enable only if public LoadBalancer services (ingress) fail while the cluster is otherwise healthy."
}

variable "postgres_admin_login" {
  type        = string
  description = "PostgreSQL administrator login (not 'azure_superuser')."
  default     = "cnipadmin"
}

variable "postgres_database_name" {
  type        = string
  description = "Application database name on the flexible server."
  default     = "cloud_native_image_processing"
}

variable "postgres_sku_name" {
  type        = string
  description = "Azure PostgreSQL Flexible Server SKU (e.g. B_Standard_B1ms)."
  default     = "B_Standard_B1ms"
}

variable "managed_redis_sku_name" {
  type        = string
  description = "Azure Managed Redis SKU (e.g. Balanced_B1 for small dev). See https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/managed_redis#sku_name"
  default     = "Balanced_B1"
}

variable "managed_redis_high_availability" {
  type        = bool
  description = "Enable zone redundancy for Azure Managed Redis (adds cost)."
  default     = false
}

variable "eventhub_sku" {
  type        = string
  description = "Event Hubs namespace SKU."
  default     = "Standard"
}

variable "eventhub_capacity" {
  type        = number
  description = "Event Hubs throughput units (Standard)."
  default     = 1
}

variable "kubernetes_namespace" {
  type        = string
  description = "Namespace where Helm will install the app (used for workload identity federated credential subject)."
  default     = "cnip"
}

variable "workload_service_account_name" {
  type        = string
  description = "Kubernetes ServiceAccount name used by CNIP pods for Key Vault CSI + workload identity."
  default     = "cnip-workload"
}

variable "computer_vision_endpoint" {
  type        = string
  description = "Optional Computer Vision endpoint stored in Key Vault (can be empty)."
  default     = ""
  sensitive   = false
}

variable "computer_vision_api_key" {
  type        = string
  description = "Optional Computer Vision API key stored in Key Vault (can be empty)."
  default     = ""
  sensitive   = true
}

variable "key_vault_additional_admin_principal_ids" {
  type        = list(string)
  description = "Optional Azure AD object IDs to grant Key Vault Administrator on the project Key Vault (for example, specific user object IDs)."
  default     = []

  validation {
    condition = alltrue([
      for id in var.key_vault_additional_admin_principal_ids :
      can(regex("^[0-9a-fA-F-]{36}$", trimspace(id)))
    ])
    error_message = "Each key_vault_additional_admin_principal_ids entry must be a valid Azure AD object ID (GUID)."
  }
}

variable "enable_azure_front_door" {
  type        = bool
  description = "Deploy Azure Front Door (Premium) with WAF (managed Default Rule Set) in front of the ingress public hostname. Requires enable_public_nginx_ingress. Adds cost."
  default     = false
}

variable "frontdoor_waf_mode" {
  type        = string
  description = "WAF policy mode: Detection (log only) or Prevention (block)."
  default     = "Prevention"

  validation {
    condition     = contains(["Detection", "Prevention"], var.frontdoor_waf_mode)
    error_message = "frontdoor_waf_mode must be Detection or Prevention."
  }
}

variable "frontdoor_waf_default_rule_set_version" {
  type        = string
  description = "Managed Default Rule Set version for type DefaultRuleSet. Use 1.0 or preview-0.1 (azurerm provider does not support 2.x for this type)."
  default     = "1.0"
}

variable "enable_network_ddos_protection_plan" {
  type        = bool
  description = "Create an Azure Network DDoS Protection Plan in this region (high cost). Associate it with a VNet or protected public IPs in Azure Portal or extend Terraform."
  default     = false
}

variable "enable_azure_monitor" {
  type        = bool
  description = "Create a Log Analytics workspace and stream platform diagnostics from AKS, Key Vault, and (optionally) ACR. Log ingestion has per-GB cost."
  default     = true
}

variable "log_analytics_retention_in_days" {
  type        = number
  description = "Log Analytics workspace retention in days (30–730 for paid tiers)."
  default     = 30
}

variable "enable_azure_monitor_acr_diagnostics" {
  type        = bool
  description = "When enable_azure_monitor is true, also send Container Registry diagnostics to the workspace."
  default     = true
}

variable "enable_application_insights" {
  type        = bool
  description = "When enable_azure_monitor is true, create workspace-based Application Insights and store its connection string in Key Vault (application-insights-connection-string). Enable Helm applicationInsights.enabled to sync it into pods."
  default     = true
}

variable "application_insights_name" {
  type        = string
  description = "Application Insights resource name (empty = \"{prefix}-appinsights\"). Must be 3–255 characters when set."
  default     = ""

  validation {
    condition     = var.application_insights_name == "" || (length(var.application_insights_name) >= 3 && length(var.application_insights_name) <= 255)
    error_message = "application_insights_name must be empty or between 3 and 255 characters."
  }
}
