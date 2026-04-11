output "resource_group_name" {
  value       = azurerm_resource_group.main.name
  description = "Resource group containing CNIP infrastructure."
}

output "acr_login_server" {
  value       = azurerm_container_registry.main.login_server
  description = "Login server for docker push (same as Helm image repository host)."
}

output "acr_name" {
  value       = azurerm_container_registry.main.name
  description = "ACR resource name (hostname label without .azurecr.io; use with az acr login -n)."
}

output "acr_admin_username" {
  value       = azurerm_container_registry.main.admin_username
  description = "ACR admin user (when admin_enabled is true)."
}

output "acr_admin_password" {
  value       = azurerm_container_registry.main.admin_password
  sensitive   = true
  description = "ACR admin password (sensitive). Prefer az acr login + managed identity on AKS for production."
}

output "aks_name" {
  value       = azurerm_kubernetes_cluster.main.name
  description = "AKS cluster name for kubectl / az aks get-credentials."
}

output "aks_kube_config_command" {
  value       = "az aks get-credentials --resource-group ${azurerm_resource_group.main.name} --name ${azurerm_kubernetes_cluster.main.name} --overwrite-existing"
  description = "Run this to merge kubeconfig for the cluster."
}

output "postgres_fqdn" {
  value       = azurerm_postgresql_flexible_server.main.fqdn
  description = "PostgreSQL flexible server FQDN."
}

output "postgres_connection_string" {
  value       = local.postgres_connection_string
  sensitive   = true
  description = "Npgsql-style connection string for the application database."
}

output "postgres_admin_password" {
  value       = random_password.postgres.result
  sensitive   = true
  description = "PostgreSQL administrator password (same as embedded in postgres_connection_string)."
}

output "redis_hostname" {
  value       = azurerm_managed_redis.main.hostname
  description = "Azure Managed Redis hostname (cluster endpoint)."
}

output "redis_primary_connection_string" {
  value       = local.redis_connection_string
  sensitive   = true
  description = "Redis connection string for Redis__ConnectionString (TLS + access key; Azure Managed Redis)."
}

output "storage_account_name" {
  value       = azurerm_storage_account.main.name
  description = "Blob storage account name."
}

output "storage_primary_connection_string" {
  value       = azurerm_storage_account.main.primary_connection_string
  sensitive   = true
  description = "Azure Storage connection string for BlobStorage__ConnectionString."
}

output "eventhub_namespace" {
  value       = azurerm_eventhub_namespace.main.name
  description = "Event Hubs namespace name."
}

output "eventhub_primary_connection_string" {
  value       = azurerm_eventhub_namespace_authorization_rule.app.primary_connection_string
  sensitive   = true
  description = "Use for both EventHubs__ImageProcessingConnectionString and EventHubs__AiDescriptionConnectionString (hub names are set in Helm ConfigMap)."
}

output "key_vault_name" {
  value       = azurerm_key_vault.main.name
  description = "Key Vault name (DNS segment before .vault.azure.net)."
}

output "key_vault_uri" {
  value       = azurerm_key_vault.main.vault_uri
  description = "Key Vault URI for Azure portal / tooling."
}

output "azure_tenant_id" {
  value       = data.azurerm_client_config.current.tenant_id
  description = "Azure AD tenant ID (Helm keyVault.tenantId)."
}

output "workload_identity_client_id" {
  value       = azurerm_user_assigned_identity.workload.client_id
  description = "User-assigned managed identity client ID for Helm keyVault.workloadIdentityClientId."
}

output "workload_identity_principal_id" {
  value       = azurerm_user_assigned_identity.workload.principal_id
  description = "Object ID of the workload user-assigned identity (for troubleshooting RBAC)."
}

output "kubernetes_namespace_for_identity" {
  value       = var.kubernetes_namespace
  description = "Helm namespace / federated credential namespace segment."
}

output "workload_service_account_name" {
  value       = var.workload_service_account_name
  description = "Kubernetes ServiceAccount name wired in federated credential and Helm."
}

output "ingress_nginx_namespace" {
  value       = var.enable_public_nginx_ingress ? var.ingress_nginx_namespace : null
  description = "Namespace of the Terraform-managed ingress-nginx release (null if enable_public_nginx_ingress is false)."
}

output "ingress_azure_dns_label" {
  value       = var.enable_public_nginx_ingress ? local.ingress_dns_label : null
  description = "Azure DNS name label applied to the LB public IP via Service annotation (FQDN uses <region>.cloudapp.azure.com when enable_public_nginx_ingress is true)."
}

output "ingress_public_ip_fqdn" {
  value       = local.ingress_fqdn
  description = "FQDN for the default LB public IP once Azure applies the DNS label (same shape as cloudapp.azure.com)."
}

output "ingress_test_http_url" {
  value       = local.ingress_fqdn != null ? "http://${local.ingress_fqdn}" : null
  description = "HTTP URL for the ingress controller (point CNIP Helm ingress.hosts / CORS / CNIP_PUBLIC_APP_URL at this host, or use curl -H 'Host: ...')."
}

output "ingress_nginx_load_balancer_ip" {
  value       = local.ingress_lb_ip
  description = "Public IP of the ingress controller Service (may be null until Azure finishes provisioning; refresh with terraform apply -refresh-only or kubectl get svc -n ingress-nginx)."
}

output "cnip_public_app_url_http_ip" {
  value       = local.ingress_lb_ip != null ? "http://${local.ingress_lb_ip}" : null
  description = "Browser origin for the app when using the load balancer IP (no trailing path). Add to Helm config.cors.allowedOrigins; use http://IP/ as Docker build-arg VITE_BASE_URL for Vite asset base; leave VITE_API_BASE_URL unset for same-origin /api."
}

output "cnip_vite_base_url_http_ip" {
  value       = local.ingress_lb_ip != null ? "http://${local.ingress_lb_ip}/" : null
  description = "Vite config base URL with trailing slash (Docker --build-arg VITE_BASE_URL=...)."
}

output "cnip_frontend_docker_build_args_hint" {
  value = var.enable_public_nginx_ingress ? join(" ", compact([
    "docker build -f frontend/Dockerfile ./frontend",
    local.ingress_lb_ip != null ? "--build-arg VITE_BASE_URL=http://${local.ingress_lb_ip}/" : "# after LB IP exists:",
    local.ingress_lb_ip != null ? null : "--build-arg VITE_BASE_URL=http://$(terraform -chdir=devops/terraform output -raw ingress_nginx_load_balancer_ip)/",
    "--build-arg VITE_API_BASE_URL=",
  ])) : null
  description = "Example docker build arguments: VITE_BASE_URL is the public origin for Vite base; empty VITE_API_BASE_URL keeps /api on the same host via ingress."
}

output "cdn_frontdoor_endpoint_host_name" {
  value       = length(azurerm_cdn_frontdoor_endpoint.main) > 0 ? azurerm_cdn_frontdoor_endpoint.main[0].host_name : null
  description = "Public hostname of the Front Door endpoint (e.g. *.azurefd.net). Point app DNS/CORS here when Front Door is enabled."
}

output "cdn_frontdoor_endpoint_url" {
  value       = length(azurerm_cdn_frontdoor_endpoint.main) > 0 ? "https://${azurerm_cdn_frontdoor_endpoint.main[0].host_name}" : null
  description = "HTTPS URL for the Front Door endpoint (use for browser origin / Helm ingress when using Front Door)."
}

output "cdn_frontdoor_profile_name" {
  value       = length(azurerm_cdn_frontdoor_profile.main) > 0 ? azurerm_cdn_frontdoor_profile.main[0].name : null
  description = "Azure Front Door profile name (when enable_azure_front_door is true)."
}

output "network_ddos_protection_plan_id" {
  value       = length(azurerm_network_ddos_protection_plan.main) > 0 ? azurerm_network_ddos_protection_plan.main[0].id : null
  description = "Resource ID of the Network DDoS Protection Plan when enable_network_ddos_protection_plan is true. Associate with a VNet or public IPs for Network DDoS Protection."
}

output "log_analytics_workspace_id" {
  value       = length(azurerm_log_analytics_workspace.main) > 0 ? azurerm_log_analytics_workspace.main[0].id : null
  description = "Azure resource ID of the Log Analytics workspace (Azure Monitor / Log Analytics queries)."
}

output "log_analytics_workspace_customer_id" {
  value       = length(azurerm_log_analytics_workspace.main) > 0 ? azurerm_log_analytics_workspace.main[0].workspace_id : null
  description = "Workspace (customer) ID for Log Analytics agents and cross-resource queries."
}
