# Optional: if the AKS node subnet NSG was tightened (or mirrored from a locked-down template),
# LoadBalancer-forwarded traffic may need an explicit allow from the AzureLoadBalancer service tag
# to node ports used by Services (e.g. ingress-nginx). Default AKS subnets usually already allow this.

data "azurerm_resources" "aks_node_nsgs" {
  count = var.aks_subnet_lb_inbound_nsg_hardening ? 1 : 0

  resource_group_name = azurerm_kubernetes_cluster.main.node_resource_group
  type                = "Microsoft.Network/networkSecurityGroups"
}

locals {
  aks_node_nsg_names = var.aks_subnet_lb_inbound_nsg_hardening && length(data.azurerm_resources.aks_node_nsgs[0].resources) > 0 ? sort([
    for r in data.azurerm_resources.aks_node_nsgs[0].resources : r.name
  ]) : []
  # Prefer the agent pool NSG name when multiple NSGs exist in the node RG.
  aks_node_nsg_name = length(local.aks_node_nsg_names) == 0 ? null : try(
    [for n in local.aks_node_nsg_names : n if can(regex("agentpool", lower(n)))][0],
    local.aks_node_nsg_names[0]
  )
}

resource "azurerm_network_security_rule" "aks_lb_to_nodeports" {
  count = var.aks_subnet_lb_inbound_nsg_hardening && local.aks_node_nsg_name != null ? 1 : 0

  name                        = "AllowAzureLBToNodeServicePorts"
  priority                    = 220
  direction                   = "Inbound"
  access                      = "Allow"
  protocol                    = "Tcp"
  source_port_range           = "*"
  source_address_prefix       = "AzureLoadBalancer"
  destination_port_ranges     = ["80", "443", "30000-32767"]
  destination_address_prefix  = "*"
  resource_group_name         = azurerm_kubernetes_cluster.main.node_resource_group
  network_security_group_name = local.aks_node_nsg_name
}
