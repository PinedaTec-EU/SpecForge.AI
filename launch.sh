#!/usr/bin/env bash
set -euo pipefail

if [ -f .env ]; then
  set -a
  source .env
  set +a
fi

open_url() {
  local url="$1"

  if command -v open >/dev/null 2>&1; then
    open "$url"
    return
  fi

  if command -v xdg-open >/dev/null 2>&1; then
    xdg-open "$url" >/dev/null 2>&1 &
  fi
}

normalize_url() {
  local url="${1:-http://localhost:5128/}"

  if [[ "$url" != */ ]]; then
    url="$url/"
  fi

  printf '%s' "$url"
}

workspace_root="$(pwd)"
user_story_id="${1:-}"
portal_url="$(normalize_url "${SPECFORGE_WORKFLOW_PORTAL_URL:-http://localhost:5128/}")"
project_path="src/SpecForge.Runner.Cli/SpecForge.Runner.Cli.csproj"

echo "Launching the SpecForge workflow portal..."
echo "Portal URL: ${portal_url}"

if [ -n "$user_story_id" ]; then
  echo "Focused user story: ${user_story_id}"
else
  echo "Focused user story: auto"
fi

open_url "$portal_url"

if [ -n "$user_story_id" ]; then
  dotnet run --project "$project_path" -- serve-workflow "$workspace_root" "$user_story_id" "$portal_url"
else
  dotnet run --project "$project_path" -- serve-workflow "$workspace_root" "$portal_url"
fi
