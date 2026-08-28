#!/usr/bin/env bash
# =============================================================================
#  Run the same deployment the pipeline runs, from a laptop.
#
#    ./scripts/deploy-infra.sh dev --what-if   # preview only
#    ./scripts/deploy-infra.sh dev             # apply
#
#  Use it to try a template change before pushing it. Anything that reaches a
#  shared environment should still go through the pipeline.
# =============================================================================
set -euo pipefail

ENVIRONMENT="${1:?Usage: deploy-infra.sh <dev|test|prod> [--what-if]}"
MODE="${2:-apply}"
LOCATION="${LOCATION:-southafricanorth}"

REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PARAMETER_FILE="${REPOSITORY_ROOT}/infra/params/${ENVIRONMENT}.bicepparam"

[ -f "$PARAMETER_FILE" ] || { echo "No parameter file at ${PARAMETER_FILE}" >&2; exit 1; }

DEPLOYMENT_NAME="ocrai-${ENVIRONMENT}-$(date -u +%Y%m%d%H%M%S)"

if [ "$MODE" = "--what-if" ]; then
  az deployment sub what-if \
    --name "$DEPLOYMENT_NAME" \
    --location "$LOCATION" \
    --parameters "$PARAMETER_FILE"
  exit 0
fi

az deployment sub create \
  --name "$DEPLOYMENT_NAME" \
  --location "$LOCATION" \
  --parameters "$PARAMETER_FILE" \
  --query properties.outputs \
  --output json
