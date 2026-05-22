#!/usr/bin/env python3
"""Parse TERRAFORM_STATE JSON and append TF_STATE_* lines to GITHUB_ENV."""

from __future__ import annotations

import json
import os
import sys


def main() -> int:
    raw = os.environ.get("TERRAFORM_STATE", "")
    if not raw.strip():
        print(
            "::error::Set environment variable TERRAFORM_STATE (JSON). "
            "See devops/terraform/terraform-state.github.json.example",
            file=sys.stderr,
        )
        return 1

    try:
        obj = json.loads(raw)
    except json.JSONDecodeError as exc:
        print(f"::error::TERRAFORM_STATE must be valid JSON: {exc}", file=sys.stderr)
        return 1

    if not isinstance(obj, dict):
        print("::error::TERRAFORM_STATE must be a JSON object", file=sys.stderr)
        return 1

    fields = {
        "resource_group_name": "TF_STATE_RG",
        "storage_account_name": "TF_STATE_SA",
        "container_name": "TF_STATE_CONTAINER",
        "key": "TF_STATE_KEY",
    }

    lines: list[str] = []
    for json_key, env_key in fields.items():
        value = obj.get(json_key)
        if value is None or not str(value).strip():
            print(
                f"::error::TERRAFORM_STATE missing or empty field: {json_key}",
                file=sys.stderr,
            )
            return 1
        lines.append(f"{env_key}={value}")

    github_env = os.environ.get("GITHUB_ENV")
    if not github_env:
        print("::error::GITHUB_ENV is not set", file=sys.stderr)
        return 1

    with open(github_env, "a", encoding="utf-8") as env_file:
        env_file.write("\n".join(lines) + "\n")

    return 0


if __name__ == "__main__":
    sys.exit(main())
