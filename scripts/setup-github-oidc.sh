#!/usr/bin/env bash
# =============================================================================
#  One-time bootstrap: let GitHub Actions deploy to Azure without any secret.
#
#  Creates an Entra ID application, federates it to this repository's
#  environments, and gives it the two roles the pipeline needs:
#
#    Contributor                        create the resource group and resources
#    Role Based Access Control Admin    create the role assignment in
#                                       modules/cognitive-services-role.bicep
#
#  Then GitHub asks Entra ID for a token, proving with a signed OIDC claim which
#  repository, branch and environment is asking — no client secret is stored,
#  and nothing is left to expire.
#
#  Usage:
#    ./scripts/setup-github-oidc.sh <subscription-id> [github-owner/repo]
# =============================================================================
set -euo pipefail

SUBSCRIPTION_ID="${1:?Usage: setup-github-oidc.sh <subscription-id> [owner/repo]}"
REPOSITORY="${2:-enkosi/ocr-ai-vision}"
APP_NAME="${APP_NAME:-github-ocr-ai-vision}"
ENVIRONMENTS=(dev test prod)

CONTRIBUTOR_ROLE_ID='b24988ac-6180-42a0-ab88-20f7382dd24c'
RBAC_ADMIN_ROLE_ID='f58310d9-a9f6-439a-9e8d-f62e7b41a168'

az account set --subscription "$SUBSCRIPTION_ID"
TENANT_ID=$(az account show --query tenantId -o tsv)

echo "==> Application registration"
APP_ID=$(az ad app list --display-name "$APP_NAME" --query '[0].appId' -o tsv)
if [ -z "$APP_ID" ]; then
  APP_ID=$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)
  echo "    created $APP_NAME ($APP_ID)"
else
  echo "    reusing $APP_NAME ($APP_ID)"
fi

az ad sp create --id "$APP_ID" >/dev/null 2>&1 || true
PRINCIPAL_ID=$(az ad sp show --id "$APP_ID" --query id -o tsv)

echo "==> Federated credentials"
for environment in "${ENVIRONMENTS[@]}"; do
  # The subject must match exactly what GitHub puts in the token. For a job
  # bound to an environment that is: repo:<owner>/<repo>:environment:<name>
  az ad app federated-credential create --id "$APP_ID" --parameters "{
    \"name\": \"github-${environment}\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:${REPOSITORY}:environment:${environment}\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }" >/dev/null 2>&1 && echo "    + environment:${environment}" \
    || echo "    = environment:${environment} (already present)"
done

# Lets a pull-request build run 'what-if' without being able to deploy — give
# this one read-only rights if you want that separation.
az ad app federated-credential create --id "$APP_ID" --parameters "{
  \"name\": \"github-main\",
  \"issuer\": \"https://token.actions.githubusercontent.com\",
  \"subject\": \"repo:${REPOSITORY}:ref:refs/heads/main\",
  \"audiences\": [\"api://AzureADTokenExchange\"]
}" >/dev/null 2>&1 && echo "    + ref:refs/heads/main" \
  || echo "    = ref:refs/heads/main (already present)"

echo "==> Role assignments (subscription scope)"
for role in "$CONTRIBUTOR_ROLE_ID" "$RBAC_ADMIN_ROLE_ID"; do
  az role assignment create \
    --assignee-object-id "$PRINCIPAL_ID" \
    --assignee-principal-type ServicePrincipal \
    --role "$role" \
    --scope "/subscriptions/${SUBSCRIPTION_ID}" >/dev/null 2>&1 \
    && echo "    + $role" || echo "    = $role (already assigned)"
done

cat <<SUMMARY

Done. Add these to each GitHub Environment (Settings > Environments > dev|test|prod
> Environment secrets), and turn on required reviewers for 'prod':

  AZURE_CLIENT_ID        ${APP_ID}
  AZURE_TENANT_ID        ${TENANT_ID}
  AZURE_SUBSCRIPTION_ID  ${SUBSCRIPTION_ID}

Optional, per environment, if that environment has application secrets to place
in Key Vault. The pipeline passes it to the .bicepparam through an environment
variable; it is never written to a file in the repository:

  ADDITIONAL_SECRETS_JSON   {"Some-Api-Key":"...","Other-Secret":"..."}

Note on Key Vault permissions. The two subscription-scope roles above are enough
to DEPLOY, including creating secrets, because Bicep creates them through the ARM
control plane. Reading or rotating a secret VALUE afterwards ('az keyvault secret
set', the portal's secret blade) is a data-plane operation needing 'Key Vault
Secrets Officer'. Rather than widening the roles above, grant it per vault by
passing this object ID to the template's secretsOfficerPrincipalIds parameter:

  ${PRINCIPAL_ID}

SUMMARY
