#!/usr/bin/env bash
set -Eeuo pipefail
trap 'echo "Deployment failed at line $LINENO. Inspect docker compose ps and service logs (do not share secrets)." >&2' ERR
cd "$(dirname "${BASH_SOURCE[0]}")"
mode=${1:-}
if [[ "$mode" == --install ]]; then
  [[ $(uname -s) == Linux ]] || { echo 'Installation supports Ubuntu only.' >&2; exit 1; }
  source /etc/os-release
  [[ "$ID" == ubuntu ]] || { echo 'Installation supports Ubuntu only.' >&2; exit 1; }
  sudo apt-get update
  sudo apt-get install -y ca-certificates curl git
  if ! command -v docker >/dev/null; then
    sudo install -m 0755 -d /etc/apt/keyrings
    sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    sudo chmod a+r /etc/apt/keyrings/docker.asc
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu ${UBUNTU_CODENAME:-$VERSION_CODENAME} stable" | sudo tee /etc/apt/sources.list.d/docker.list >/dev/null
    sudo apt-get update
    sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  elif ! docker compose version >/dev/null 2>&1; then
    sudo apt-get install -y docker-compose-plugin
  fi
  sudo systemctl enable --now docker
  echo "Docker installed and enabled. Configure .env, apply migrations, then run deploy.sh. See README.md."
  exit 0
fi
if [[ "$mode" == --update ]]; then
  [[ -z $(git status --porcelain) ]] || { echo 'Working tree must be clean before update.' >&2; exit 1; }
  git pull --ff-only
fi
[[ -f .env ]] || { echo 'Configure .env first; see README.md.' >&2; exit 1; }
chmod 600 .env
compose=(docker compose --env-file .env -f compose.yaml)
if [[ ${USE_GPU:-0} == 1 ]]; then compose+=(-f compose.gpu.yaml); fi
# Validate without rendering secret values.
"${compose[@]}" config --quiet
python3 validate_config.py "${compose[@]}"
"${compose[@]}" pull ollama model-init
"${compose[@]}" build backend document-ai
# A completed initializer must be re-created to verify weights on every deployment.
"${compose[@]}" up -d --wait --wait-timeout 180 ollama
"${compose[@]}" run --rm model-init
# Re-create both namespace-sharing containers together on updates.
"${compose[@]}" up -d --force-recreate --wait --wait-timeout 2100
"${compose[@]}" exec -T ollama ollama show qwen3:4b >/dev/null
"${compose[@]}" exec -T document-ai python -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8090/ready',timeout=5)"
# Installed tags and process health do not establish adequate inference capacity.
# Fail deployment if a real synthetic document cannot traverse the model pipeline.
"${compose[@]}" exec -T document-ai python -m app.smoke
for attempt in {1..30}; do
  if curl -fsS --max-time 5 http://127.0.0.1:8080/api/health; then
    echo; echo 'Backend, document worker, and qwen3:4b checks passed.'; exit 0
  fi
  sleep 2
done
echo 'Backend health failed: verify database connectivity, migrations, and forwarded-header CIDRs.' >&2
exit 1
