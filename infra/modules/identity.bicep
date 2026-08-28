// -----------------------------------------------------------------------------
//  A user-assigned managed identity, created BEFORE anything that uses it.
//
//  Why user-assigned rather than system-assigned:
//
//  A system-assigned identity does not exist until its web app exists, so its
//  role assignments can only be made afterwards. That is fine for code that
//  calls Azure at runtime (it retries), but App Service resolves
//  @Microsoft.KeyVault(...) app settings while the site starts — and on the
//  very first deployment it would try to read the vault before the role
//  assignment granting it access had been created. The result is a site that
//  boots with an unresolved setting until something restarts it.
//
//  Creating the identity first breaks that ordering problem: it is granted its
//  roles before the site that uses it is ever created.
// -----------------------------------------------------------------------------

@description('Name of the user-assigned managed identity.')
param name string

@description('Region for the identity.')
param location string

@description('Tags applied to the identity.')
param tags object

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
  tags: tags
}

output id string = identity.id
output name string = identity.name

@description('Object ID — what role assignments are made against.')
output principalId string = identity.properties.principalId

@description('Application ID — what DefaultAzureCredential needs to pick this identity out of several.')
output clientId string = identity.properties.clientId
