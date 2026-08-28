// -----------------------------------------------------------------------------
//  Copies the Document Intelligence account key into Key Vault.
//
//  The interesting property of this module is that NO HUMAN EVER SEES THE KEY.
//  listKeys() is evaluated by ARM during the deployment, and the result is
//  written straight into the vault. The key is not typed into a pipeline
//  variable, not pasted into a parameter file, and not printed in a log — it
//  moves from one Azure resource to another without leaving Azure.
//
//  This is the fallback credential, not the primary one. The app authenticates
//  with its managed identity; the key exists for the cases where that is not
//  possible (a subscription where you cannot create role assignments, a
//  sandbox on the F0 tier, break-glass debugging). It is only deployed when
//  storeDocumentIntelligenceKeyInVault is true, and that REQUIRES the account's
//  disableLocalAuth to be false — with keys switched off, listKeys() fails and
//  the deployment stops, which is the correct, loud outcome.
// -----------------------------------------------------------------------------

@description('Name of an existing Document Intelligence account in this resource group.')
param accountName string

@description('Name of an existing Key Vault in this resource group.')
param keyVaultName string

@description('''
Name of the secret to write. Key Vault secret names allow alphanumerics and
hyphens only — no colons or underscores — so the ASP.NET Core configuration key
"DocumentIntelligence:ApiKey" is spelled with a hyphen here.
''')
param secretName string = 'DocumentIntelligence-ApiKey'

resource account 'Microsoft.CognitiveServices/accounts@2023-05-01' existing = {
  name: accountName
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource secret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: secretName
  properties: {
    value: account.listKeys().key1
    contentType: 'Document Intelligence account key (key1)'
  }
}

// The name, not the value. Callers build the reference URI from this and the
// vault's own URI; nothing downstream ever needs the secret itself.
output secretName string = secret.name
