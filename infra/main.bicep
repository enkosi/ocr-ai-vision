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

@description('''
Refuse Document Intelligence API keys, forcing Entra ID (managed identity)
authentication. Must be false if storeDocumentIntelligenceKeyInVault is true —
a key cannot be read out of an account that has keys switched off.
''')
param disableLocalAuth bool = true

@description('Days to retain Log Analytics data.')
@minValue(30)
@maxValue(730)
param logRetentionInDays int = 30

@description('Days a deleted Key Vault or secret stays recoverable.')
@minValue(7)
@maxValue(90)
param keyVaultSoftDeleteRetentionInDays int = 7

@description('Block permanent deletion of the vault. Irreversible once enabled — production only.')
param enableKeyVaultPurgeProtection bool = false

@description('''
Copy the Document Intelligence account key into Key Vault. Off by default: the
app authenticates with its managed identity and needs no key at all. Turn it on
only where managed identity is not an option, and set disableLocalAuth to false
in the same parameter file.
''')
param storeDocumentIntelligenceKeyInVault bool = false

@description('''
Point the app at the Key Vault secret instead of its managed identity. Requires
storeDocumentIntelligenceKeyInVault, or a secret written into the vault by some
other means under the same name.
''')
param useKeyBasedAuthentication bool = false

@description('''
Extra secrets to place in the vault, as a name-to-value map. @secure(), so
values never reach deployment history, what-if output or a log. Supply it from
the pipeline's secret store — see docs/azure-cicd.md — never from a file here.
''')
@secure()
param additionalSecrets object = {}

@description('''
Object IDs allowed to write and rotate secrets in the vault — typically the
pipeline's service principal, or an on-call group. The application is NOT in
this list: it gets read-only access to secret values and nothing more.
''')
param secretsOfficerPrincipalIds array = []

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
// Cognitive Services custom subdomain, the vault) stable across redeployments of
// the same environment, while staying different between environments and
// subscriptions.

var resourceToken = toLower(uniqueString(subscription().id, environmentName, workloadName))
var namePrefix = '${workloadName}-${environmentName}'

var resourceGroupName = 'rg-${namePrefix}'
var logAnalyticsName = 'log-${namePrefix}'
var appInsightsName = 'appi-${namePrefix}'
var appServicePlanName = 'asp-${namePrefix}'
var managedIdentityName = 'id-${namePrefix}'
var webAppName = 'app-${namePrefix}-${resourceToken}'
var documentIntelligenceName = 'di-${namePrefix}-${resourceToken}'

// Vault names are capped at 24 characters and allow no hyphen runs, so this one
// is built differently from the rest.
var keyVaultName = take('kv${replace(namePrefix, '-', '')}${resourceToken}', 24)

// Key Vault secret names allow alphanumerics and hyphens only, which is why the
// ASP.NET Core key "DocumentIntelligence:ApiKey" is spelled with a hyphen.
var documentIntelligenceKeySecretName = 'DocumentIntelligence-ApiKey'

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

// Created early and deliberately: everything it needs access to is granted
// before the web app that uses it exists. See modules/identity.bicep.
module identity 'modules/identity.bicep' = {
  name: 'identity'
  scope: resourceGroup
  params: {
    name: managedIdentityName
    location: location
    tags: tags
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'key-vault'
  scope: resourceGroup
  params: {
    name: keyVaultName
    location: location
    tags: tags
    softDeleteRetentionInDays: keyVaultSoftDeleteRetentionInDays
    enablePurgeProtection: enableKeyVaultPurgeProtection
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    secrets: additionalSecrets
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

// The app authenticates to Document Intelligence and reads its secrets with a
// managed identity, so it needs a data-plane role on each. Granting them here —
// rather than out-of-band with a script — is what makes "no keys anywhere"
// reproducible.
module roleAssignments 'modules/role-assignments.bicep' = {
  name: 'role-assignments'
  scope: resourceGroup
  params: {
    principalId: identity.outputs.principalId
    documentIntelligenceAccountName: documentIntelligence.outputs.name
    keyVaultName: keyVault.outputs.name
    secretsOfficerPrincipalIds: secretsOfficerPrincipalIds
  }
}

// Optional, and off by default. Moves the account key into the vault without it
// passing through a pipeline variable or a human's clipboard.
module documentIntelligenceKey 'modules/document-intelligence-key.bicep' = if (storeDocumentIntelligenceKeyInVault) {
  name: 'document-intelligence-key'
  scope: resourceGroup
  params: {
    accountName: documentIntelligence.outputs.name
    keyVaultName: keyVault.outputs.name
    secretName: documentIntelligenceKeySecretName
  }
}

// Reading the name back from the module (rather than reusing the variable) is
// what tells Bicep the app must not be configured until the secret exists. When
// the module is not deployed the safe-dereference yields null and the fallback
// keeps the URI well-formed for a secret written by other means.
var keySecretName = documentIntelligenceKey.?outputs.secretName ?? documentIntelligenceKeySecretName

var documentIntelligenceApiKeySecretUri = useKeyBasedAuthentication
  ? '${keyVault.outputs.uri}secrets/${keySecretName}'
  : ''

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
    managedIdentityId: identity.outputs.id
    managedIdentityClientId: identity.outputs.clientId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    documentIntelligenceEndpoint: documentIntelligence.outputs.endpoint
    defaultModelId: defaultModelId
    maxUploadSizeInBytes: maxUploadSizeInBytes
    documentIntelligenceApiKeySecretUri: documentIntelligenceApiKeySecretUri
  }
  // The one explicit dependency in these templates, and the reason it is here
  // is worth understanding. App Service resolves @Microsoft.KeyVault(...) app
  // settings AS THE SITE STARTS, so the role letting the identity read the
  // vault has to exist before the site does. No value flows from the role
  // assignments into this module, so Bicep cannot infer that ordering from the
  // data — without this line the two modules deploy in parallel and the site
  // can come up with an unresolved setting.
  dependsOn: [roleAssignments]
}

// --- Outputs -----------------------------------------------------------------
// Consumed by the pipelines: the deploy step needs the app name, the smoke test
// needs the URL, secret rotation needs the vault name. No secret VALUE is
// output — deployment outputs are stored in the deployment history and readable
// by anyone with read access to the subscription.

output resourceGroupName string = resourceGroup.name
output webAppName string = appService.outputs.webAppName
output webAppUrl string = appService.outputs.webAppUrl
output webAppSlotName string = appService.outputs.slotName
output documentIntelligenceEndpoint string = documentIntelligence.outputs.endpoint
output applicationInsightsName string = monitoring.outputs.appInsightsName
output keyVaultName string = keyVault.outputs.name
output keyVaultUri string = keyVault.outputs.uri
output managedIdentityClientId string = identity.outputs.clientId
