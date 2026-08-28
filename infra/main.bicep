// =============================================================================
//  OCR AI Vision — infrastructure entry point
// =============================================================================
//  Deployed at SUBSCRIPTION scope so that the resource group itself is part of
//  the infrastructure-as-code, not a thing somebody made by hand first.
//
//    az deployment sub create \
//      --name ocrai-dev-$(date +%Y%m%d%H%M%S) \
//      --location southafricanorth \
//      --template-file infra/main.bicep \
//      --parameters infra/params/dev.bicepparam
//
//  Every environment runs this same file. The only thing that differs between
//  dev, test and prod is the .bicepparam file fed to it.
// =============================================================================

targetScope = 'subscription'

// --- What varies between environments ---------------------------------------

@description('Environment discriminator. Becomes part of every resource name.')
@allowed([
  'dev'
  'test'
  'prod'
])
param environmentName string

@description('Azure region for every resource in this deployment.')
param location string

@description('Short workload name. Keep it to a few characters — it prefixes resource names.')
@minLength(3)
@maxLength(10)
param workloadName string = 'ocrai'

@description('App Service plan SKU. B1 is fine for dev; slots and zone redundancy need S1 or higher.')
param appServicePlanSku string = 'B1'

@description('Document Intelligence SKU. F0 is the free tier (one per subscription); S0 is standard.')
@allowed([
  'F0'
  'S0'
])
param documentIntelligenceSku string = 'S0'

@description('Create a "staging" deployment slot to swap into production. Requires a Standard or Premium plan.')
param useDeploymentSlot bool = false

@description('Keep the app warm. Not available on the Free or Shared tiers.')
param alwaysOn bool = true

@description('Refuse Document Intelligence API keys, forcing Entra ID (managed identity) authentication.')
param disableLocalAuth bool = true

@description('Days to retain Log Analytics data.')
@minValue(30)
@maxValue(730)
param logRetentionInDays int = 30

@description('Document Intelligence model used when a request does not name one.')
param defaultModelId string = 'prebuilt-layout'

@description('Largest upload the API will accept, in bytes.')
param maxUploadSizeInBytes int = 52428800

@description('ASPNETCORE_ENVIRONMENT value for the web app.')
param aspNetCoreEnvironment string = 'Production'

@description('Extra tags merged into the standard set applied to every resource.')
param additionalTags object = {}

// --- Naming ------------------------------------------------------------------
// A deterministic suffix keeps globally-unique names (the web app hostname, the
// Cognitive Services custom subdomain) stable across redeployments of the same
// environment, while staying different between environments and subscriptions.

var resourceToken = toLower(uniqueString(subscription().id, environmentName, workloadName))
var namePrefix = '${workloadName}-${environmentName}'

var resourceGroupName = 'rg-${namePrefix}'
var logAnalyticsName = 'log-${namePrefix}'
var appInsightsName = 'appi-${namePrefix}'
var appServicePlanName = 'asp-${namePrefix}'
var webAppName = 'app-${namePrefix}-${resourceToken}'
var documentIntelligenceName = 'di-${namePrefix}-${resourceToken}'

var tags = union(
  {
    workload: workloadName
    environment: environmentName
    managedBy: 'bicep'
  },
  additionalTags
)

// --- Resource group ----------------------------------------------------------

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

// --- Modules -----------------------------------------------------------------

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  scope: resourceGroup
  params: {
    logAnalyticsName: logAnalyticsName
    appInsightsName: appInsightsName
    location: location
    tags: tags
    retentionInDays: logRetentionInDays
  }
}

module documentIntelligence 'modules/document-intelligence.bicep' = {
  name: 'document-intelligence'
  scope: resourceGroup
  params: {
    name: documentIntelligenceName
    location: location
    tags: tags
    skuName: documentIntelligenceSku
    disableLocalAuth: disableLocalAuth
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

module appService 'modules/app-service.bicep' = {
  name: 'app-service'
  scope: resourceGroup
  params: {
    appServicePlanName: appServicePlanName
    webAppName: webAppName
    location: location
    tags: tags
    skuName: appServicePlanSku
    alwaysOn: alwaysOn
    useDeploymentSlot: useDeploymentSlot
    aspNetCoreEnvironment: aspNetCoreEnvironment
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    documentIntelligenceEndpoint: documentIntelligence.outputs.endpoint
    defaultModelId: defaultModelId
    maxUploadSizeInBytes: maxUploadSizeInBytes
  }
}

// The app authenticates to Document Intelligence with its managed identity, so
// it needs a data-plane role on that account. Granting it here — rather than
// out-of-band with a script — is what makes "no keys anywhere" reproducible.
module documentIntelligenceAccess 'modules/cognitive-services-role.bicep' = {
  name: 'document-intelligence-access'
  scope: resourceGroup
  params: {
    accountName: documentIntelligence.outputs.name
    principalIds: appService.outputs.principalIds
  }
}

// --- Outputs -----------------------------------------------------------------
// Consumed by the pipelines: the deploy step needs the app name, the smoke test
// needs the URL.

output resourceGroupName string = resourceGroup.name
output webAppName string = appService.outputs.webAppName
output webAppUrl string = appService.outputs.webAppUrl
output webAppSlotName string = appService.outputs.slotName
output documentIntelligenceEndpoint string = documentIntelligence.outputs.endpoint
output applicationInsightsName string = monitoring.outputs.appInsightsName
