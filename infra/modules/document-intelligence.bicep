// -----------------------------------------------------------------------------
//  Azure AI Document Intelligence (Microsoft.CognitiveServices, kind
//  'FormRecognizer') — the service the API actually calls.
// -----------------------------------------------------------------------------

@description('Name of the Document Intelligence account. Must be globally unique — it becomes the subdomain.')
param name string

@description('Region for the account.')
param location string

@description('Tags applied to the account.')
param tags object

@description('F0 is the free tier (one per subscription, heavily throttled); S0 is standard.')
@allowed([
  'F0'
  'S0'
])
param skuName string = 'S0'

@description('Refuse API keys entirely, so only Entra ID tokens work.')
param disableLocalAuth bool = true

@description('Workspace that receives the account diagnostic logs.')
param logAnalyticsWorkspaceId string

resource account 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  kind: 'FormRecognizer'
  sku: {
    name: skuName
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    // A custom subdomain is REQUIRED for Entra ID authentication. Without it the
    // account only has the regional endpoint, which accepts keys and nothing
    // else — and DefaultAzureCredential in the app would fail at runtime.
    customSubDomainName: name
    disableLocalAuth: disableLocalAuth
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: account
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'audit'
        enabled: true
      }
      {
        categoryGroup: 'allLogs'
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

output name string = account.name
output endpoint string = account.properties.endpoint
