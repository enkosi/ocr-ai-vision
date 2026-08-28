// Test/QA: production-shaped, so a problem that only appears on a dedicated
// plan with Always On surfaces here rather than in prod.
using '../main.bicep'

param environmentName = 'test'
param location = 'southafricanorth'
param appServicePlanSku = 'S1'
param documentIntelligenceSku = 'S0'
param useDeploymentSlot = false
param alwaysOn = true
param logRetentionInDays = 30
param aspNetCoreEnvironment = 'Production'

param keyVaultSoftDeleteRetentionInDays = 7
param enableKeyVaultPurgeProtection = false

param storeDocumentIntelligenceKeyInVault = false
param useKeyBasedAuthentication = false

// See dev.bicepparam for what this does and why it is not a literal.
param additionalSecrets = json(readEnvironmentVariable('OCRAI_ADDITIONAL_SECRETS', '{}'))

param additionalTags = {
  costCentre: 'engineering'
}
