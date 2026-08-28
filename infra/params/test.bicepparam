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
param additionalTags = {
  costCentre: 'engineering'
}
