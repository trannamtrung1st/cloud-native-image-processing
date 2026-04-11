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
