// Development: cheapest thing that still exercises the real services.
// Single site, no slot, short log retention.
using '../main.bicep'

param environmentName = 'dev'
param location = 'southafricanorth'
param appServicePlanSku = 'B1'
param documentIntelligenceSku = 'S0'
param useDeploymentSlot = false
param alwaysOn = false // Not available below the Basic tier's dedicated compute; off keeps dev cheap.
param logRetentionInDays = 30
param aspNetCoreEnvironment = 'Development' // Turns on Swagger UI at /swagger.

// Key Vault. Purge protection stays off in dev so the environment can be torn
// down and rebuilt: with it on, the vault name is reserved for the full
// retention period and a rebuild fails until it is purged.
param keyVaultSoftDeleteRetentionInDays = 7
param enableKeyVaultPurgeProtection = false

// The app uses its managed identity, so no Document Intelligence key is stored
// or used. To try the key-based path instead, set all three of these:
//   param disableLocalAuth = false
//   param storeDocumentIntelligenceKeyInVault = true
//   param useKeyBasedAuthentication = true
param storeDocumentIntelligenceKeyInVault = false
param useKeyBasedAuthentication = false

// SECRETS ARE NOT WRITTEN IN THIS FILE. readEnvironmentVariable reads them from
// the environment of whatever is compiling the parameters — the pipeline agent,
// where they arrive from GitHub Environment secrets or an Azure DevOps variable
// group, and where the platform masks them in logs. The default '{}' keeps a
// local run working with no secrets configured at all.
//
// Format: OCRAI_ADDITIONAL_SECRETS='{"Some-Api-Key":"...","Other-Secret":"..."}'
param additionalSecrets = json(readEnvironmentVariable('OCRAI_ADDITIONAL_SECRETS', '{}'))

param additionalTags = {
  costCentre: 'engineering'
}
