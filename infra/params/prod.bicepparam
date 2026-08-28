// Production: Premium v3 plan, a staging slot to swap through, longer
// retention, and API keys switched off on the AI account.
using '../main.bicep'

param environmentName = 'prod'
param location = 'southafricanorth'
param appServicePlanSku = 'P1v3'
param documentIntelligenceSku = 'S0'
param useDeploymentSlot = true
param alwaysOn = true
param disableLocalAuth = true
param logRetentionInDays = 90
param aspNetCoreEnvironment = 'Production'
param additionalTags = {
  costCentre: 'engineering'
  dataClassification: 'confidential'
}
