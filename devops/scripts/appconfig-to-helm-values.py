#!/usr/bin/env python3
"""Read Azure App Configuration keys (label cnip) and write a Helm values override YAML.

Used by deploy CI and local Helm installs. Reads live App Configuration (Portal is source of truth after Terraform seed).
Key Vault secrets are not read here — Helm CSI syncs Key Vault at pod start.
Requires: az CLI logged in, App Configuration Data Reader (or Data Owner) on the store.
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path


def fetch_keys(app_config_name: str, label: str) -> dict[str, str]:
    result = subprocess.run(
        [
            "az",
            "appconfig",
            "kv",
            "list",
            "--name",
            app_config_name,
            "--label",
            label,
            "--query",
            "[].{key:key, value:value}",
            "-o",
            "json",
        ],
        check=True,
        capture_output=True,
        text=True,
    )
    items = json.loads(result.stdout or "[]")
    return {item["key"]: item["value"] for item in items}


def normalize_decimal_string(value: str) -> str:
    """App Configuration / YAML may return large integers as scientific notation (e.g. 1.048576e+07)."""
    s = value.strip()
    if not s:
        return s
    try:
        number = float(s)
        if number == int(number):
            return str(int(number))
        return format(number, "f").rstrip("0").rstrip(".") or "0"
    except ValueError:
        return s


def yaml_quote(value: str) -> str:
    if value == "":
        return '""'
    escaped = value.replace("\\", "\\\\").replace('"', '\\"')
    if any(c in value for c in ":#{}[],&*?|>-<>=!%@`") or value.strip() != value:
        return f'"{escaped}"'
    return value


def yaml_config_string(value: str, default: str = "") -> str:
    """Quoted YAML string for ConfigMap env vars (.NET binds these as strings)."""
    s = normalize_decimal_string(value or default)
    escaped = s.replace("\\", "\\\\").replace('"', '\\"')
    return f'"{escaped}"'


def parse_int_setting(value: str, default: str) -> int:
    return int(normalize_decimal_string(value or default) or default)


def write_helm_overrides(keys: dict[str, str], output: Path) -> None:
    app_prefix = "cnip/app/"
    helm_prefix = "cnip/helm/"

    def app(key_suffix: str, default: str = "") -> str:
        return keys.get(f"{app_prefix}{key_suffix}", default)

    cors_raw = app("cors_allowed_origins", "[]")
    try:
        cors_origins = json.loads(cors_raw)
    except json.JSONDecodeError as exc:
        raise SystemExit(f"Invalid JSON in cnip/app/cors_allowed_origins: {exc}") from exc
    if not isinstance(cors_origins, list):
        raise SystemExit("cnip/app/cors_allowed_origins must be a JSON array")

    lines: list[str] = [
        "# Generated from Azure App Configuration — do not edit manually.",
        "config:",
        f"  aspnetcoreEnvironment: {yaml_config_string(app('aspnetcore_environment', 'Production'))}",
        f"  dotnetEnvironment: {yaml_config_string(app('dotnet_environment', 'Production'))}",
        "  blobStorage:",
        f"    containerName: {yaml_config_string(app('blob_container_name', 'images'))}",
        "  eventHubs:",
        f"    imageProcessingHubName: {yaml_config_string(app('eventhub_image_processing_hub', 'image-processing'))}",
        f"    imageProcessingConsumerGroup: {yaml_config_string(app('eventhub_image_processing_cg', 'cg1'))}",
        f"    aiDescriptionHubName: {yaml_config_string(app('eventhub_ai_description_hub', 'ai-description'))}",
        f"    aiDescriptionConsumerGroup: {yaml_config_string(app('eventhub_ai_description_cg', 'cg1'))}",
        f"    checkpointContainerName: {yaml_config_string(app('eventhub_checkpoint_container', 'eh-checkpoints'))}",
        "  demo:",
        f"    getByIdDelayMs: {yaml_config_string(app('demo_get_by_id_delay_ms', '0'))}",
        f"    grayscaleProcessingDelayMs: {yaml_config_string(app('demo_grayscale_delay_ms', '0'))}",
        f"    aiDescriptionProcessingDelayMs: {yaml_config_string(app('demo_ai_description_delay_ms', '0'))}",
        "  upload:",
        f"    maxRequestBodyBytes: {yaml_config_string(app('upload_max_request_body_bytes', '10485760'))}",
        "  identity:",
        f"    bearerTokenExpirationHours: {yaml_config_string(app('identity_bearer_token_hours', '8'))}",
        "  redis:",
        f"    detailsExpirationMinutes: {yaml_config_string(app('redis_details_expiration_minutes', '5'))}",
        "  cors:",
        "    allowedOrigins:",
    ]
    for origin in cors_origins:
        lines.append(f"      - {yaml_quote(str(origin))}")

    ai_enabled = keys.get(f"{helm_prefix}application_insights_enabled", "true").lower() == "true"
    lines.extend(
        [
            "applicationInsights:",
            f"  enabled: {str(ai_enabled).lower()}",
            "api:",
            f"  replicaCount: {parse_int_setting(keys.get(f'{helm_prefix}api_replica_count', ''), '1')}",
            "worker:",
            f"  replicaCount: {parse_int_setting(keys.get(f'{helm_prefix}worker_replica_count', ''), '1')}",
            "aiWorker:",
            f"  replicaCount: {parse_int_setting(keys.get(f'{helm_prefix}ai_worker_replica_count', ''), '1')}",
            "frontend:",
            f"  replicaCount: {parse_int_setting(keys.get(f'{helm_prefix}frontend_replica_count', ''), '1')}",
            f"  imagesRefreshIntervalSeconds: {yaml_config_string(keys.get(f'{helm_prefix}frontend_images_refresh_interval_seconds', ''), '5')}",
            f"deploymentReloadTrigger: {yaml_config_string(keys.get(f'{helm_prefix}deployment_reload_trigger', ''), '1')}",
        ]
    )

    output.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app-config-name", required=True)
    parser.add_argument("--label", default="cnip")
    parser.add_argument(
        "-o",
        "--output",
        type=Path,
        default=Path("appconfig.overrides.yaml"),
    )
    args = parser.parse_args()

    keys = fetch_keys(args.app_config_name, args.label)
    if not keys:
        print(
            f"No App Configuration keys found for store {args.app_config_name!r} label {args.label!r}.",
            file=sys.stderr,
        )
        return 1

    write_helm_overrides(keys, args.output)
    print(f"Wrote Helm overrides to {args.output}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
