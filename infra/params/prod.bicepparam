// Production: Premium v3 plan, a staging slot to swap through, longer
// retention, keys switched off on the AI account, and a vault that cannot be
// destroyed on a bad afternoon.
using '../main.bicep'

param environmentName = 'prod'
param location = 'southafricanorth'
param appServicePlanSku = 'P1v3'
param documentIntelligenceSku = 'S0'
param useDeploymentSlot = true
param alwaysOn = true
param logRetentionInDays = 90
param aspNetCoreEnvironment = 'Production'

// No API key exists to steal: the account refuses key authentication entirely
// and the app authenticates as its managed identity.
param disableLocalAuth = true
param storeDocumentIntelligenceKeyInVault = false
param useKeyBasedAuthentication = false

// Purge protection is IRREVERSIBLE. Enabling it here is deliberate: it means a
// compromised pipeline, or a mistaken 'az group delete', cannot permanently
// destroy production secrets — they stay recoverable for the full 90 days.
param keyVaultSoftDeleteRetentionInDays = 90
param enableKeyVaultPurgeProtection = true

// See dev.bicepparam for what this does and why it is not a literal.
param additionalSecrets = json(readEnvironmentVariable('OCRAI_ADDITIONAL_SECRETS', '{}'))

param additionalTags = {
  costCentre: 'engineering'
  dataClassification: 'confidential'
}
