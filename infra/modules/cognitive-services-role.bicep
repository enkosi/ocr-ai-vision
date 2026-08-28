// -----------------------------------------------------------------------------
//  Grants "Cognitive Services User" on the Document Intelligence account to the
//  web app's managed identities (the production site and, when it exists, its
//  staging slot).
//
//  This is the piece that lets the app leave DocumentIntelligence:ApiKey empty
//  and authenticate with DefaultAzureCredential instead.
// -----------------------------------------------------------------------------

@description('Name of an existing Document Intelligence account in this resource group.')
param accountName string

@description('Object IDs of the identities to grant access to.')
param principalIds array

// Built-in role. Referring to it by GUID rather than name is deliberate: role
// display names are not stable API contract, GUIDs are.
var cognitiveServicesUserRoleId = 'a97b65f3-24c7-4388-baec-2e87135dc908'

resource account 'Microsoft.CognitiveServices/accounts@2023-05-01' existing = {
  name: accountName
}

resource assignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in principalIds: {
    // A deterministic name makes the assignment idempotent: redeploying finds
    // the same GUID and no-ops instead of failing on a duplicate.
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
]
