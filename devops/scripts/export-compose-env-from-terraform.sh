#!/usr/bin/env bash
# Source from the repository root so Docker Compose picks up the exports:
#   . devops/scripts/export-compose-env-from-terraform.sh
#   export CNIP_IMAGE_TAG=1.0.5-cnip   # must match Helm image tags; not from Terraform
#   docker compose build … && docker compose push …
#
# Sets: CNIP_ACR_LOGIN_SERVER, CNIP_PUBLIC_APP_URL (browser origin for frontend build).
# Requires: terraform applied for devops/terraform and outputs available.
# Not using `set -e` so a failed `terraform output` does not close an interactive shell when sourced.

if [[ -n "${BASH_SOURCE[0]:-}" ]]; then
  _SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
elif [[ -n "${ZSH_VERSION:-}" ]]; then
  _SCRIPT_DIR="$(cd "$(dirname "${(%):-%x}")" && pwd)"
else
  _SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
fi
_REPO_ROOT="$(cd "${_SCRIPT_DIR}/../.." && pwd)"

_tf() {
  (cd "${_REPO_ROOT}" && terraform -chdir=devops/terraform "$@")
}

export CNIP_ACR_LOGIN_SERVER="$(_tf output -raw acr_login_server)"

_ingress="$(_tf output -raw ingress_test_http_url 2>/dev/null || true)"
if [ -n "${_ingress}" ] && [ "${_ingress}" != "null" ]; then
  export CNIP_PUBLIC_APP_URL="${_ingress}"
else
  _ip_origin="$(_tf output -raw cnip_public_app_url_http_ip 2>/dev/null || true)"
  if [ -n "${_ip_origin}" ] && [ "${_ip_origin}" != "null" ]; then
    export CNIP_PUBLIC_APP_URL="${_ip_origin}"
  else
    export CNIP_PUBLIC_APP_URL="${CNIP_PUBLIC_APP_URL:-http://localhost:8080}"
    echo "export-compose-env-from-terraform.sh: no ingress URL in Terraform state; using CNIP_PUBLIC_APP_URL=${CNIP_PUBLIC_APP_URL}. Set enable_public_nginx_ingress or override CNIP_PUBLIC_APP_URL." >&2
  fi
fi

echo "export-compose-env-from-terraform.sh — exported:"
echo "  CNIP_ACR_LOGIN_SERVER=${CNIP_ACR_LOGIN_SERVER}"
echo "  CNIP_PUBLIC_APP_URL=${CNIP_PUBLIC_APP_URL}"
if [[ -n "${CNIP_IMAGE_TAG:-}" ]]; then
  echo "  CNIP_IMAGE_TAG=${CNIP_IMAGE_TAG}"
else
  echo "  CNIP_IMAGE_TAG=(not set — export before docker compose build/push, e.g. export CNIP_IMAGE_TAG=1.0.5-cnip)"
fi
