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
param additionalTags = {
  costCentre: 'engineering'
}
