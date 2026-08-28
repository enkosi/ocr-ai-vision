// -----------------------------------------------------------------------------
//  Every data-plane permission the application needs, in one place.
//
//  Each assignment is scoped to a single resource rather than to the resource
//  group, so the identity can read this vault and call this AI account and
//  nothing else — least privilege by construction rather than by intention.
// -----------------------------------------------------------------------------

@description('Object ID of the identity being granted access.')
param principalId string

@description('Name of the Document Intelligence account to grant access to.')
param documentIntelligenceAccountName string

@description('Name of the Key Vault to grant access to.')
param keyVaultName string

@description('''
Object IDs allowed to WRITE secrets — a release pipeline that rotates a
credential, or an on-call group. Empty by default: nothing needs this to deploy,
because Bicep creates secrets through the ARM control plane, which subscription
Contributor already covers. This role is for the data plane — 'az keyvault
secret set' and the portal's secret blade.
''')
param secretsOfficerPrincipalIds array = []

// Built-in roles, referred to by GUID because display names are not a stable
// contract. These are the two read-only data-plane roles this app needs:
//
//   Cognitive Services User   call the analysis API (no management rights)
//   Key Vault Secrets User    read secret VALUES (not list, rotate or delete)
var cognitiveServicesUserRoleId = 'a97b65f3-24c7-4388-baec-2e87135dc908'
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

// Key Vault Secrets Officer: read, write, rotate and delete secret values.
// Deliberately separate from the role above — the application can read what it
// needs and change nothing.
var keyVaultSecretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

resource account 'Microsoft.CognitiveServices/accounts@2023-05-01' existing = {
  name: documentIntelligenceAccountName
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// guid() of the scope, principal and role makes the assignment name
// deterministic, so redeploying finds the existing assignment and no-ops
// instead of failing on a duplicate.
resource documentIntelligenceAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, principalId, cognitiveServicesUserRoleId)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      cognitiveServicesUserRoleId
    )
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

resource keyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, principalId, keyVaultSecretsUserRoleId)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      keyVaultSecretsUserRoleId
    )
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

resource secretsOfficerAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for officerPrincipalId in secretsOfficerPrincipalIds: {
    name: guid(vault.id, officerPrincipalId, keyVaultSecretsOfficerRoleId)
    scope: vault
    properties: {
      roleDefinitionId: subscriptionResourceId(
        'Microsoft.Authorization/roleDefinitions',
        keyVaultSecretsOfficerRoleId
      )
      principalId: officerPrincipalId
    }
  }
]
