# Azure Front Door (Premium) + WAF in front of the ingress-nginx public hostname.
# Optional Network DDoS Protection Plan (associate with a VNet or public IPs separately).
# https://learn.microsoft.com/azure/frontdoor/

locals {
  # Premium SKU is required for managed rule sets (OWASP-style Default Rule Set) on the WAF policy.
  afd_enabled = var.enable_azure_front_door && var.enable_public_nginx_ingress && local.ingress_fqdn != null
}

resource "random_string" "afd_suffix" {
  length  = 5
  lower   = true
  upper   = false
  numeric = true
  special = false
}

resource "azurerm_network_ddos_protection_plan" "main" {
  count = var.enable_network_ddos_protection_plan ? 1 : 0

  name                = "${var.prefix}-ddos-${random_string.afd_suffix.result}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = var.tags
}

resource "azurerm_cdn_frontdoor_profile" "main" {
  count = local.afd_enabled ? 1 : 0

  name                = "${var.prefix}-afd-${random_string.afd_suffix.result}"
  resource_group_name = azurerm_resource_group.main.name
  sku_name            = "Premium_AzureFrontDoor"
  tags                = var.tags
}

resource "azurerm_cdn_frontdoor_firewall_policy" "main" {
  count = local.afd_enabled ? 1 : 0

  name                = substr("${replace(lower(var.prefix), "-", "")}waf${random_string.afd_suffix.result}", 0, 128)
  resource_group_name = azurerm_resource_group.main.name
  sku_name            = "Premium_AzureFrontDoor"
  enabled             = true
  mode                = var.frontdoor_waf_mode

  managed_rule {
    type    = "DefaultRuleSet"
    version = var.frontdoor_waf_default_rule_set_version
    action  = "Block"
  }

  tags = var.tags
}

resource "azurerm_cdn_frontdoor_origin_group" "aks_ingress" {
  count = local.afd_enabled ? 1 : 0

  name                     = "${var.prefix}-aks-origin"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.main[0].id
  session_affinity_enabled = true

  load_balancing {
    additional_latency_in_milliseconds = 50
    sample_size                        = 4
    successful_samples_required        = 3
  }

  health_probe {
    interval_in_seconds = 100
    path                = "/"
    protocol            = "Http"
    request_type        = "GET"
  }
}

resource "azurerm_cdn_frontdoor_origin" "aks_ingress" {
  count = local.afd_enabled ? 1 : 0

  name                          = "${var.prefix}-ingress-origin"
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.aks_ingress[0].id
  enabled                       = true

  certificate_name_check_enabled = true
  host_name                      = local.ingress_fqdn
  http_port                      = 80
  https_port                     = 443
  origin_host_header             = local.ingress_fqdn
  priority                       = 1
  weight                         = 1000
}

resource "azurerm_cdn_frontdoor_endpoint" "main" {
  count = local.afd_enabled ? 1 : 0

  name                     = "${var.prefix}-ep-${random_string.afd_suffix.result}"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.main[0].id
  tags                     = var.tags
}

resource "azurerm_cdn_frontdoor_route" "main" {
  count = local.afd_enabled ? 1 : 0

  name                          = "${var.prefix}-route"
  cdn_frontdoor_endpoint_id     = azurerm_cdn_frontdoor_endpoint.main[0].id
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.aks_ingress[0].id
  cdn_frontdoor_origin_ids      = [azurerm_cdn_frontdoor_origin.aks_ingress[0].id]
  enabled                       = true
  forwarding_protocol           = "HttpOnly"
  https_redirect_enabled        = true
  link_to_default_domain        = true
  patterns_to_match             = ["/*"]
  supported_protocols           = ["Http", "Https"]

  # No cache {} block — avoid caching authenticated /api and dynamic content at the edge.
  depends_on = [azurerm_cdn_frontdoor_origin.aks_ingress]
}

resource "azurerm_cdn_frontdoor_security_policy" "waf" {
  count = local.afd_enabled ? 1 : 0

  name                     = "${var.prefix}-waf-policy"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.main[0].id

  security_policies {
    firewall {
      cdn_frontdoor_firewall_policy_id = azurerm_cdn_frontdoor_firewall_policy.main[0].id

      association {
        domain {
          cdn_frontdoor_domain_id = azurerm_cdn_frontdoor_endpoint.main[0].id
        }
        patterns_to_match = ["/*"]
      }
    }
  }

  depends_on = [
    azurerm_cdn_frontdoor_route.main,
    azurerm_cdn_frontdoor_firewall_policy.main,
  ]
}
