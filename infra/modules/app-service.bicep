// -----------------------------------------------------------------------------
//  Linux App Service plan + web app for the ASP.NET Core API, optionally with a
//  "staging" deployment slot for zero-downtime swaps in production.
// -----------------------------------------------------------------------------

@description('Name of the App Service plan.')
param appServicePlanName string

@description('Name of the web app. Must be globally unique — it becomes <name>.azurewebsites.net.')
param webAppName string

@description('Region for the plan and the app.')
param location string

@description('Tags applied to every resource here.')
param tags object

@description('App Service plan SKU. Slots and Always On need S1 or higher.')
param skuName string = 'B1'

@description('Keep the app warm rather than letting it unload when idle.')
param alwaysOn bool = true

@description('Create a "staging" slot to deploy into and swap from.')
param useDeploymentSlot bool = false

@description('Value of ASPNETCORE_ENVIRONMENT.')
param aspNetCoreEnvironment string = 'Production'

@description('Application Insights connection string.')
param appInsightsConnectionString string

@description('Workspace that receives the web app diagnostic logs.')
param logAnalyticsWorkspaceId string

@description('Document Intelligence endpoint the app will call.')
param documentIntelligenceEndpoint string

@description('Model used when a request does not name one.')
param defaultModelId string

@description('Largest upload the API will accept, in bytes.')
param maxUploadSizeInBytes int

var slotName = 'staging'

// ASP.NET Core reads nested configuration from environment variables using a
// double underscore as the section separator, so DocumentIntelligence__Endpoint
// here binds to the same option as "DocumentIntelligence:Endpoint" in
// appsettings.json — and wins over it, because environment variables sit higher
// in the configuration precedence chain.
//
// Note what is NOT here: DocumentIntelligence__ApiKey. Leaving it unset is what
// makes the adapter fall through to DefaultAzureCredential and use the managed
// identity below.
var appSettings = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: aspNetCoreEnvironment
  }
  {
    name: 'DocumentIntelligence__Endpoint'
    value: documentIntelligenceEndpoint
  }
  {
    name: 'DocumentIntelligence__DefaultModelId'
    value: defaultModelId
  }
  {
    name: 'DocumentIntelligence__MaxUploadSizeInBytes'
    value: string(maxUploadSizeInBytes)
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: appInsightsConnectionString
  }
  {
    name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
    value: '~3'
  }
  {
    name: 'XDT_MicrosoftApplicationInsights_Mode'
    value: 'recommended'
  }
  {
    // The pipeline uploads an already-published zip, so Oryx has nothing to
    // build on the server.
    name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
    value: 'false'
  }
]

var siteConfig = {
  linuxFxVersion: 'DOTNETCORE|8.0'
  alwaysOn: alwaysOn
  http20Enabled: true
  minTlsVersion: '1.2'
  ftpsState: 'Disabled'
  healthCheckPath: '/health'
  appSettings: appSettings
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  sku: {
    name: skuName
  }
  kind: 'linux'
  properties: {
    reserved: true // 'reserved: true' is how ARM spells "this plan is Linux".
  }
}

resource site 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: siteConfig
  }
}

resource stagingSlot 'Microsoft.Web/sites/slots@2023-12-01' = if (useDeploymentSlot) {
  parent: site
  name: slotName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: siteConfig
  }
}

resource siteDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: site
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
      }
      {
        category: 'AppServicePlatformLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

// The slot runs the same code against the same Document Intelligence account,
// so it needs the same data-plane role. Safe dereference (.?) yields null when
// the slot was not deployed, and the filter drops it.
var slotPrincipalId = stagingSlot.?identity.?principalId ?? ''

output webAppName string = site.name
output webAppUrl string = 'https://${site.properties.defaultHostName}'
output slotName string = useDeploymentSlot ? slotName : ''
output principalIds array = filter(
  [site.identity.principalId, slotPrincipalId],
  principalId => !empty(principalId)
)
