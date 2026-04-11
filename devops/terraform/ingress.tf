# Public NGINX Ingress for AKS: Azure creates the Standard LB public IP; we only set a DNS name
# via service annotation (<label>.<region>.cloudapp.azure.com). See:
# https://learn.microsoft.com/azure/aks/configure-load-balancer-standard

resource "random_string" "ingress_dns_suffix" {
  length  = 6
  lower   = true
  upper   = false
  numeric = true
  special = false
}

locals {
  ingress_dns_label = var.enable_public_nginx_ingress ? substr(
    join("", [
      for ch in regexall(".", lower("${var.prefix}-ing-${random_string.ingress_dns_suffix.result}")) :
      length(regexall("[a-z0-9-]", ch)) > 0 ? ch : "-"
    ]),
    0,
    63
  ) : null
  # Azure RM returns region as a slug (e.g. southeastasia) on resource group / cluster.
  ingress_cloudapp_region = replace(lower(trimspace(azurerm_resource_group.main.location)), " ", "")
  ingress_fqdn = var.enable_public_nginx_ingress ? "${local.ingress_dns_label}.${local.ingress_cloudapp_region}.cloudapp.azure.com" : null
  # Populated after Azure assigns the ingress Service external IP (may be null on first plan).
  ingress_lb_ip = var.enable_public_nginx_ingress ? try(
    data.kubernetes_service.ingress_nginx[0].status[0].load_balancer[0].ingress[0].ip,
    null
  ) : null
}

resource "helm_release" "ingress_nginx" {
  count = var.enable_public_nginx_ingress ? 1 : 0

  name             = "ingress-nginx"
  namespace        = var.ingress_nginx_namespace
  create_namespace = true

  repository = "https://kubernetes.github.io/ingress-nginx"
  chart      = "ingress-nginx"
  version    = var.ingress_nginx_chart_version

  values = [
    yamlencode({
      controller = {
        service = {
          type = "LoadBalancer"
          annotations = {
            "service.beta.kubernetes.io/azure-dns-label-name" = local.ingress_dns_label
          }
        }
      }
    })
  ]

  depends_on = [
    azurerm_kubernetes_cluster.main,
  ]
}

data "kubernetes_service" "ingress_nginx" {
  count = var.enable_public_nginx_ingress ? 1 : 0

  metadata {
    name      = "${helm_release.ingress_nginx[0].name}-controller"
    namespace = var.ingress_nginx_namespace
  }

  depends_on = [helm_release.ingress_nginx]
}
