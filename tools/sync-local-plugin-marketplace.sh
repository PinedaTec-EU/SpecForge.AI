#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
org_root="$(cd "$repo_root/.." && pwd)"

plugin_name="specforge-ai"
source_plugin="$repo_root/plugins/$plugin_name"
marketplace_root="$org_root/.agents/plugins"
central_plugin="$marketplace_root/$plugin_name"

if [[ ! -f "$source_plugin/.codex-plugin/plugin.json" ]]; then
  echo "Missing source plugin: $source_plugin" >&2
  exit 1
fi

python3 - "$repo_root/version_definition.json" "$source_plugin/.codex-plugin/plugin.json" <<'PY'
import json
import sys
from pathlib import Path

version_definition_path = Path(sys.argv[1])
plugin_manifest_path = Path(sys.argv[2])
version_definition = json.loads(version_definition_path.read_text())
plugin_manifest = json.loads(plugin_manifest_path.read_text())
plugin_manifest["version"] = version_definition["currentVersion"]
plugin_manifest_path.write_text(json.dumps(plugin_manifest, indent=2) + "\n")
PY

mkdir -p "$marketplace_root"
rsync -a --delete "$source_plugin/" "$central_plugin/"

write_marketplace() {
  local marketplace_path="$1"
  local marketplace_name="$2"
  local marketplace_display_name="$3"

  python3 - "$marketplace_path" "$marketplace_name" "$marketplace_display_name" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
marketplace_name = sys.argv[2]
display_name = sys.argv[3]
entry = {
    "name": "specforge-ai",
    "source": {
        "source": "local",
        "path": "./plugins/specforge-ai",
    },
    "policy": {
        "installation": "AVAILABLE",
        "authentication": "ON_INSTALL",
    },
    "category": "Coding",
}

if path.exists():
    data = json.loads(path.read_text())
else:
    data = {}

data["name"] = data.get("name") or marketplace_name
interface = data.get("interface")
if not isinstance(interface, dict):
    interface = {}
interface["displayName"] = interface.get("displayName") or display_name
data["interface"] = interface

plugins = data.get("plugins")
if not isinstance(plugins, list):
    plugins = []

plugins = [item for item in plugins if item.get("name") != "specforge-ai"]
plugins.append(entry)
data["plugins"] = plugins

path.parent.mkdir(parents=True, exist_ok=True)
path.write_text(json.dumps(data, indent=2) + "\n")
PY
}

write_marketplace "$marketplace_root/marketplace.json" "pinedatec-local" "PinedaTec local plugins"

timestamp="$(date +%Y%m%d%H%M%S)"

while IFS= read -r git_dir; do
  consumer_root="$(dirname "$git_dir")"
  agents_plugins="$consumer_root/.agents/plugins"
  consumer_plugin="$agents_plugins/$plugin_name"

  mkdir -p "$agents_plugins"

  if [[ -L "$consumer_plugin" ]]; then
    current_target="$(readlink "$consumer_plugin")"
    if [[ "$current_target" != "$central_plugin" ]]; then
      rm "$consumer_plugin"
      ln -s "$central_plugin" "$consumer_plugin"
    fi
  elif [[ -e "$consumer_plugin" ]]; then
    backup_path="$agents_plugins/$plugin_name.backup.$timestamp"
    mv "$consumer_plugin" "$backup_path"
    ln -s "$central_plugin" "$consumer_plugin"
    echo "Backed up existing plugin copy: $backup_path"
  else
    ln -s "$central_plugin" "$consumer_plugin"
  fi

  write_marketplace "$agents_plugins/marketplace.json" "pinedatec-local" "PinedaTec local plugins"
  echo "Linked $consumer_root -> $central_plugin"
done < <(find "$org_root" -maxdepth 2 -name .git -type d | sort)

echo "Central SpecForge plugin marketplace ready: $marketplace_root/marketplace.json"
