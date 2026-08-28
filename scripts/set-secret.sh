#!/usr/bin/env bash
# =============================================================================
#  Write or rotate a secret in an environment's Key Vault.
#
#    ./scripts/set-secret.sh dev Third-Party-Api-Key
#
#  The value is read from the terminal without echoing and is never passed as a
#  command-line argument, so it does not reach your shell history or the process
#  list. Requires the 'Key Vault Secrets Officer' role on the vault — a
#  DATA-PLANE role, which subscription Contributor does not include.
#
#  Rotation needs no redeployment: the app settings reference the secret without
#  a version, so App Service picks up the new value within 24 hours. Pass
#  --restart to make it immediate.
# =============================================================================
set -euo pipefail

ENVIRONMENT="${1:?Usage: set-secret.sh <dev|test|prod> <secret-name> [--restart]}"
SECRET_NAME="${2:?Usage: set-secret.sh <dev|test|prod> <secret-name> [--restart]}"
RESTART="${3:-}"

WORKLOAD_NAME="${WORKLOAD_NAME:-ocrai}"
RESOURCE_GROUP="rg-${WORKLOAD_NAME}-${ENVIRONMENT}"

VAULT_NAME=$(az keyvault list \
  --resource-group "$RESOURCE_GROUP" \
  --query '[0].name' -o tsv)

[ -n "$VAULT_NAME" ] || { echo "No Key Vault found in ${RESOURCE_GROUP}." >&2; exit 1; }

echo "Vault:  ${VAULT_NAME}"
echo "Secret: ${SECRET_NAME}"
read -rsp 'Value (input hidden): ' SECRET_VALUE
echo

[ -n "$SECRET_VALUE" ] || { echo "Empty value; nothing written." >&2; exit 1; }

# --value is read from a variable rather than typed inline for the reasons in
# the header. Output is suppressed because 'az keyvault secret set' echoes the
# secret back in its JSON response.
az keyvault secret set \
  --vault-name "$VAULT_NAME" \
  --name "$SECRET_NAME" \
  --value "$SECRET_VALUE" \
  --output none

echo "Written. A new version now exists; previous versions remain recoverable."

if [ "$RESTART" = "--restart" ]; then
  WEB_APP_NAME=$(az webapp list \
    --resource-group "$RESOURCE_GROUP" \
    --query '[0].name' -o tsv)
  az webapp restart --resource-group "$RESOURCE_GROUP" --name "$WEB_APP_NAME" --output none
  echo "Restarted ${WEB_APP_NAME} so the new value is picked up now."
fi
