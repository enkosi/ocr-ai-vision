// -----------------------------------------------------------------------------
//  Key Vault — the one place a secret is allowed to live.
//
//  Two things are worth understanding before reading this file:
//
//  CONTROL PLANE vs DATA PLANE. Creating the vault, and creating a secret as an
//  ARM resource (as this module does), are control-plane operations authorised
//  by Azure RBAC on the resource. Reading a secret's *value* at runtime is a
//  data-plane operation authorised by a data-plane role — 'Key Vault Secrets
//  User'. A principal can therefore be allowed to deploy the vault and still be
//  unable to read what is in it, which is the separation you want for a
//  deployment pipeline.
//
//  RBAC vs ACCESS POLICIES. 'enableRbacAuthorization: true' opts into Azure RBAC
//  and ignores the legacy per-vault access policy list. RBAC is the current
//  recommendation: the permissions live with every other role assignment in the
//  subscription instead of in a data structure only this resource has.
// -----------------------------------------------------------------------------

@description('Name of the vault. Globally unique, 3-24 characters, alphanumeric and hyphens.')
@minLength(3)
@maxLength(24)
param name string

@description('Region for the vault.')
param location string

@description('Tags applied to the vault.')
param tags object

@description('Days a deleted vault or secret stays recoverable. Minimum 7.')
@minValue(7)
@maxValue(90)
param softDeleteRetentionInDays int = 7

@description('''
Block permanent deletion until the retention period elapses. Protects against a
compromised or mistaken pipeline destroying secrets — and CANNOT BE TURNED OFF
once enabled, so it is off for dev and test and on for production.
''')
param enablePurgeProtection bool = false

@description('Workspace that receives the vault audit logs.')
param logAnalyticsWorkspaceId string

@description('''
Secrets to create, as a name-to-value map. Marked @secure() so the values never
appear in deployment history, what-if output or the portal. Empty by default —
supply it from the pipeline's secret store, never from a file in this repository.
''')
@secure()
param secrets object = {}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    // Soft delete is mandatory and cannot be switched off. Note the
    // consequence: after deleting this vault its NAME stays reserved for the
    // retention period, so recreating the environment fails until the old vault
    // is purged with 'az keyvault purge --name <name>'.
    enableSoftDelete: true
    softDeleteRetentionInDays: softDeleteRetentionInDays
    enablePurgeProtection: enablePurgeProtection ? true : null // 'false' is rejected; the property must be absent.
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// items() turns the map into an array of {key, value} so it can be looped over.
// The values stay secure: they came from a @secure() parameter and are written
// straight into the resource, never into a variable that could be output.
resource secretResources 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = [
  for secret in items(secrets): {
    parent: vault
    name: secret.key
    properties: {
      value: secret.value
      attributes: {
        enabled: true
      }
    }
  }
]

// Every read, write and failed access attempt, kept with the other logs.
resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: vault
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

output name string = vault.name

// The vault URI is an address, not a credential: reaching it still requires a
// data-plane role. It is what App Service needs to build a Key Vault reference.
output uri string = vault.properties.vaultUri
