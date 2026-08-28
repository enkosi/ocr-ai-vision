# Azure CI/CD and Infrastructure as Code

Everything this API needs in Azure is declared in `infra/`, and two pipelines
apply it: one for GitHub Actions, one for Azure DevOps. Nothing here is created
by hand in the portal — including the resource group and the role assignment
that lets the app authenticate without a key.

This document explains what was added, why each piece is shaped the way it is,
and what you have to do once before it will run.

---

## 1. The idea in one paragraph

**Infrastructure as Code** means the answer to "what is deployed?" is a file in
this repository, reviewed like any other file, rather than a memory of what
somebody clicked. **Bicep** is Azure's language for that: a typed, modular
front end to ARM templates. It is *declarative* — you describe the end state,
Azure works out the difference, and running the same file twice changes nothing
the second time (idempotence). **CI/CD** is what runs it: continuous integration
proves each change compiles and passes tests; continuous delivery promotes one
artifact through environments in a fixed order, with a human gate before the
last one.

---

## 2. What was added

```
infra/
  main.bicep                        subscription-scoped entry point
  bicepconfig.json                  linter rules
  modules/
    monitoring.bicep                Log Analytics + Application Insights
    identity.bicep                  user-assigned managed identity
    key-vault.bicep                 the vault, its secrets and its audit log
    document-intelligence.bicep     the AI service the API calls
    document-intelligence-key.bicep listKeys() → vault, without a human seeing it
    app-service.bicep               Linux plan, web app, optional staging slot
    role-assignments.bicep          RBAC: identity → AI account and vault
  params/
    dev.bicepparam                  ┐
    test.bicepparam                 ├ the only thing that differs per environment
    prod.bicepparam                 ┘

.github/workflows/
  ci.yml                            build + test + Bicep lint, on every PR
  cd.yml                            build once, promote dev → test → prod
  deploy-environment.yml            reusable "deploy one environment" workflow

azure-pipelines.yml                 Azure DevOps multi-stage equivalent
.azuredevops/templates/
  build-stage.yml
  validate-stage.yml
  deploy-stage.yml                  instantiated once per environment

scripts/
  setup-github-oidc.sh              one-time: passwordless auth for the pipeline
  deploy-infra.sh                   run the same deployment from a laptop
  set-secret.sh                     write or rotate a secret in a vault
```

Two pipelines are included because "Azure CI/CD" means both things in practice.
They are the same five stages expressed in two dialects — pick one, read the
other for contrast, delete whichever you do not use.

---

## 3. The architecture that gets provisioned

```
  Resource group  rg-ocrai-<env>
  ┌────────────────────────────────────────────────────────────────────┐
  │                                                                    │
  │   User-assigned managed identity  id-ocrai-<env>                   │
  │        │  created FIRST, granted its roles before anything uses it │
  │        ├──────────────► Cognitive Services User ──┐                │
  │        └──────────────► Key Vault Secrets User ─┐ │                │
  │                                                 │ │                │
  │   App Service plan (Linux)                      │ │                │
  │   ┌──────────────────────────────────────────┐  │ │                │
  │   │  Web app  app-ocrai-<env>-<token>        │  │ │                │
  │   │  .NET 8 · HTTPS only · /health           │  │ │                │
  │   │  runs as the identity above              │  │ │                │
  │   │  keyVaultReferenceIdentity ──────────────┼──┤ │                │
  │   │  [prod only] staging slot                │  │ │                │
  │   └──────────────────────────────────────────┘  │ │                │
  │                                                 ▼ │                │
  │   Key Vault  kv<workload><env><token>             │                │
  │   RBAC auth · soft delete · purge protection      │                │
  │   [prod] · audit log ──┐                          │                │
  │                        │                          ▼                │
  │   Document Intelligence di-ocrai-<env>-<token> ◄───                │
  │   FormRecognizer · custom subdomain · keys disabled                │
  │                        │                                           │
  │   Application Insights │                                           │
  │        └───────────────┴──► Log Analytics workspace                │
  └────────────────────────────────────────────────────────────────────┘
```

### Why each resource is there

| Resource | Why |
| --- | --- |
| **Resource group** | Created by `main.bicep` itself, at subscription scope, so the group's name, region and tags are code too. |
| **Log Analytics workspace** | One destination for every log. Created first; everything else points at it. |
| **Application Insights** | Workspace-based (the classic mode is retired). Its connection string is injected into the app as `APPLICATIONINSIGHTS_CONNECTION_STRING`. |
| **User-assigned managed identity** | The identity the app runs as. Created before the app, so its role assignments exist before anything needs them — see below for why that matters. |
| **Key Vault** | RBAC-authorised, soft-delete on, purge protection in prod, every access audited to Log Analytics. Holds any secret the app needs; today that is the optional Document Intelligence key. |
| **Document Intelligence account** | `Microsoft.CognitiveServices/accounts`, `kind: 'FormRecognizer'`. `customSubDomainName` is set because **Entra ID authentication does not work without it** — with only the regional endpoint the account accepts keys and nothing else. `disableLocalAuth: true` in prod turns keys off entirely. |
| **App Service plan + web app** | Linux (`reserved: true`), `DOTNETCORE|8.0`, `httpsOnly`, TLS 1.2 floor, FTPS disabled, and `healthCheckPath: '/health'` — which is the endpoint `Program.cs` already maps, so App Service takes an unhealthy instance out of rotation by itself. |
| **Staging slot** (prod) | A second copy of the site on the same plan. Deploy there, warm it, smoke test it, then swap. |
| **Role assignments** | `Cognitive Services User` on the AI account and `Key Vault Secrets User` on the vault, both granted to the managed identity, both scoped to the single resource rather than the resource group. |

### How configuration reaches the app

`AzureDocumentIntelligenceOptions` binds the `DocumentIntelligence` section. The
Bicep sets environment variables using ASP.NET Core's separator convention:

```bicep
{ name: 'DocumentIntelligence__Endpoint', value: documentIntelligenceEndpoint }
```

`__` becomes `:`, so this overrides `DocumentIntelligence:Endpoint` from
`appsettings.json` — environment variables sit higher in the configuration
precedence chain. The endpoint is not hardcoded anywhere: it is an *output* of
the Document Intelligence module, passed into the App Service module, so the app
is always pointed at the account this same deployment just created.

Note what is deliberately absent: `DocumentIntelligence__ApiKey`. The adapter's
`UsesManagedIdentity` returns true when the key is empty, and it falls through to
`DefaultAzureCredential` — which on App Service picks up the managed identity
the template created and the role assignment authorised. **No key is generated,
stored, rotated or leaked, because none exists.**

One extra setting exists for that to work: `AZURE_CLIENT_ID`. With a
*user-assigned* identity, `DefaultAzureCredential` has to be told which identity
to present, because a resource can have several attached. Its value is the
identity module's `clientId` output.

---

## 4. Reading `main.bicep`

```bicep
targetScope = 'subscription'
```

Most Bicep samples deploy *into* a resource group you made first. This one
deploys at subscription scope so the group is part of the same declaration.
The cost is that the deployment command needs `--location` (metadata about
where to record the deployment) and the principal running it needs rights at
subscription scope.

```bicep
var resourceToken = toLower(uniqueString(subscription().id, environmentName, workloadName))
```

Web app hostnames and Cognitive Services subdomains are **globally** unique, so
`app-ocrai-prod` would collide with anyone else who ever picked that name.
`uniqueString` hashes its arguments into 13 stable characters: the same inputs
always give the same output, so redeploying finds the same resources rather than
making new ones, while a different subscription or environment gets a different
name.

```bicep
module documentIntelligence 'modules/document-intelligence.bicep' = {
  scope: resourceGroup
  params: { ... logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId }
}
```

Modules are Bicep's unit of reuse, and consuming another module's `outputs` is
also how ordering is expressed. Because App Service needs the Document
Intelligence endpoint, and Document Intelligence needs the workspace id, Bicep
infers the graph from the data alone and deploys what it can in parallel.

There is exactly one explicit `dependsOn` in these templates, on the App Service
module, and it is the rare case the feature exists for: App Service resolves its
Key Vault references *as the site starts*, so the role assignment letting it read
the vault must already exist — but no value flows from the role-assignments
module into the app-service module, so Bicep has no way to infer that ordering.
Without the explicit dependency the two deploy in parallel and the site can come
up with an unresolved setting.

### The `.bicepparam` files — where environments differ

One template, three parameter files. `.bicepparam` is the typed successor to
JSON parameter files: it starts with `using '../main.bicep'`, and the compiler
checks every value against that template's parameter declarations, so a typo or
a wrong type fails at build time rather than fifteen minutes into a deployment.

| | dev | test | prod |
| --- | --- | --- | --- |
| App Service plan | B1 | S1 | P1v3 |
| Always On | off | on | on |
| Staging slot | — | — | **yes** |
| AI keys disabled | yes | yes | yes |
| Log retention | 30 days | 30 days | 90 days |
| Vault soft-delete | 7 days | 7 days | 90 days |
| Vault purge protection | off | off | **on** (irreversible) |
| `ASPNETCORE_ENVIRONMENT` | Development (Swagger on) | Production | Production |

That table *is* the difference between environments. There is no separate prod
template to drift out of sync — a change to how the web app is configured
lands in all three at once, which is the point.

---

## 5. The pipeline stages

```
   ┌───────┐   ┌──────────┐   ┌─────┐   ┌──────┐   ┌────────────┐   ┌──────┐
   │ Build │──►│ Validate │──►│ Dev │──►│ Test │──►│  approval  │──►│ Prod │
   └───────┘   └──────────┘   └─────┘   └──────┘   └────────────┘   └──────┘
    compile     bicep build    apply     apply       required        apply
    test        + what-if      deploy    deploy      reviewers       slot
    publish                    smoke     smoke                       swap
```

**Build once.** The zip is published in the Build stage and downloaded by every
deployment. The bytes that passed the dev smoke test are the exact bytes that
reach production; nothing is rebuilt per environment, so nothing but
configuration can differ between them.

**Validate** compiles the templates and runs `what-if`. A what-if is a dry run:
ARM computes the delta and prints it per resource as Create / Modify / Delete /
NoChange. Reading that on a pull request is how you catch "this change replaces
the database" before it happens. It runs before every real deployment too, so
the log records what each one changed.

**Each deploy stage does the same four things**, in an order that matters:

1. **Deploy infrastructure** and read its outputs. The app cannot be deployed
   to a site that does not exist, and the site's configuration comes out of
   this step.
2. **Deploy the app** — into the staging slot where one exists, otherwise into
   the site.
3. **Smoke test `/health`**, with retries, because a cold .NET site takes a few
   seconds. A failure here stops the promotion.
4. **Swap** (prod only) once the slot is warm and answering. Swapping back is
   the rollback, and it takes seconds.

**Gates.** The approval before prod is not YAML. In GitHub Actions it is
*required reviewers* on the `prod` Environment; in Azure DevOps it is an
approval check on the `ocrai-prod` Environment. The pipeline stops there because
the platform holds it, which means the audit trail lives with the deployment
history rather than in a chat message.

### GitHub Actions specifics

`deploy-environment.yml` is a **reusable workflow** (`on: workflow_call`) called
three times from `cd.yml`. The deployment procedure is written once; `dev`,
`test` and `prod` differ only by an input. Promotion order is `needs:`, and
`concurrency` prevents two runs of the same branch overlapping — a half-applied
template racing another one is how environments drift.

### Azure DevOps specifics

`deploy-stage.yml` is a **stage template** with parameters, instantiated once per
environment from `azure-pipelines.yml`. It uses a `deployment` job rather than a
plain `job`, which is what binds it to an Environment and therefore to approvals
and deployment history. Outputs travel between steps as
`##vso[task.setvariable ...;isOutput=true]`.

---

## 6. How the pipeline authenticates: no stored credential

Neither pipeline stores an Azure password. Both use **workload identity
federation** (OIDC):

```
GitHub/ADO ──1── "here is a signed token saying I am
                  repo:enkosi/ocr-ai-vision, environment:prod"
              ──2──► Entra ID checks that claim against the federated
                     credential on the app registration
              ◄─3── short-lived Azure access token
```

The trust is on the *claim*, so a fork, another repository, or a job outside the
`prod` environment cannot obtain the prod token even with the client ID, which
is not a secret in the first place. Nothing expires and nothing needs rotating.

Then a second, separate identity does the runtime work: the web app's
system-assigned managed identity, which the Bicep grants `Cognitive Services
User` on the AI account. **The pipeline identity deploys; the app identity
runs.** Neither one is a key in a config file.

---

## 7. Secrets and Key Vault

There are three separate secret problems here, and they have three different
answers. Conflating them is the usual reason a "secure" setup still has a key in
a config file somewhere.

| Problem | Answer |
| --- | --- |
| How does the **pipeline** prove who it is to Azure? | Workload identity federation — section 6. Nothing stored. |
| How does the **app** prove who it is to Azure services? | Its managed identity. Nothing stored. |
| Where do **secrets that are genuinely secrets** live? | Key Vault, read through App Service Key Vault references. |

The first two remove the need for a secret. Key Vault is for what is left — a
third-party API key, a connection string to something outside Azure, anything
that is not an Azure resource you can grant a role on.

### The vault

`modules/key-vault.bicep` creates it with:

- **`enableRbacAuthorization: true`** — permissions are ordinary Azure role
  assignments rather than the legacy per-vault access-policy list. Access then
  lives with every other role assignment in the subscription instead of in a
  data structure only this resource has.
- **Soft delete**, always on and not switchable. Worth knowing the consequence:
  after you delete a vault its *name stays reserved* for the retention period,
  so recreating the environment fails until you run
  `az keyvault purge --name <name>`. That is why dev and test use the 7-day
  minimum.
- **Purge protection in production only.** It blocks permanent deletion until
  retention elapses, so neither a compromised pipeline nor an `az group delete`
  can destroy production secrets. **It cannot be turned off once enabled** —
  hence off in dev and test, where you want to be able to tear down and rebuild.
- **Diagnostics to Log Analytics**, including failed access attempts. If
  something ever reads a secret it should not, that is the record.

### Control plane vs data plane — the distinction that trips people up

Two different permission systems apply to a vault:

| | Operation | Role that grants it |
| --- | --- | --- |
| **Control plane** | Create the vault; create a secret *as an ARM resource* (what Bicep does) | Contributor covers it |
| **Data plane** | Read a secret's **value**; `az keyvault secret set`; the portal's secret blade | `Key Vault Secrets User` (read), `Key Vault Secrets Officer` (write) |

So the pipeline's Contributor role is enough to **deploy** a vault and put
secrets in it, and still leaves it unable to **read** anything back. That is a
feature, and it is why `scripts/setup-github-oidc.sh` does not hand out a
data-plane role. If a principal genuinely needs to rotate values afterwards,
grant it per-vault instead of subscription-wide:

```bicep
param secretsOfficerPrincipalIds = ['<object-id>']
```

### How the app reads a secret

It does not. App Service does it, and the app sees an ordinary environment
variable:

```bicep
{
  name: 'DocumentIntelligence__ApiKey'
  value: '@Microsoft.KeyVault(SecretUri=${documentIntelligenceApiKeySecretUri})'
}
```

App Service resolves that itself — using the identity named by
`keyVaultReferenceIdentity` — and hands the *value* to the process. The
application needs no Key Vault SDK, no vault URI, and no awareness that any of
this is happening: it reads `DocumentIntelligence:ApiKey` from `IConfiguration`
exactly as it does on a laptop. **Nothing in `src/` changed to support any of
this.**

The URI carries no version, which is what makes rotation work: write a new
version into the vault and App Service picks it up within 24 hours, or
immediately on restart, with no redeployment.

```bash
./scripts/set-secret.sh prod Third-Party-Api-Key --restart
```

### Why a user-assigned identity

This is the one non-obvious design decision in the whole template, and Key Vault
references are the reason for it.

A *system-assigned* identity does not exist until its web app exists, so the role
granting it vault access can only be created afterwards. Code that calls Azure at
runtime copes with that — it retries. But App Service resolves
`@Microsoft.KeyVault(...)` settings **as the site starts**, which on a first
deployment happens before the role assignment exists. The site comes up with an
unresolved setting and stays that way until something restarts it.

A *user-assigned* identity is created first, granted its roles first, and only
then attached to the site. The ordering problem disappears. It also means the
site and its staging slot share one identity, so a slot swap never involves a
principal that has not been granted anything.

The tradeoff: a system-assigned identity is deleted with its resource, while this
one is a resource of its own that outlives the app — here it lives in the same
resource group, so it goes when the group does.

### Getting a secret in without writing it down

Two mechanisms, for two situations.

**A secret that already exists inside Azure.** `modules/document-intelligence-key.bicep`
copies the Document Intelligence account key into the vault using `listKeys()`,
evaluated by ARM during the deployment:

```bicep
resource secret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: secretName
  properties: { value: account.listKeys().key1 }
}
```

No human ever sees that key. It is not typed into a pipeline variable, not pasted
into a parameter file, not printed in a log — it moves from one Azure resource to
another without leaving Azure. It is off by default (`storeDocumentIntelligenceKeyInVault`),
because the app uses its managed identity and needs no key at all; it exists for
the cases where managed identity is not possible — a subscription where you
cannot create role assignments, an F0 sandbox, break-glass debugging. Turning it
on requires `disableLocalAuth = false` in the same parameter file: with keys
switched off, `listKeys()` fails and the deployment stops, which is the correct,
loud outcome.

**A secret that comes from outside.** The parameter files read it from the
environment:

```bicep
param additionalSecrets = json(readEnvironmentVariable('OCRAI_ADDITIONAL_SECRETS', '{}'))
```

The pipeline supplies that variable from its own secret store — a GitHub
Environment secret (`ADDITIONAL_SECRETS_JSON`) or an Azure DevOps variable group,
ideally one **linked to Key Vault** so the values are fetched at queue time
rather than stored a second time in Azure DevOps. The default `'{}'` keeps a
local run working with nothing configured.

The parameter is `@secure()`, which is not decoration. It means the value is
absent from deployment history, redacted in `what-if` output, and not readable
afterwards by anyone with subscription read access. **A `what-if` showing
`additionalSecrets: null` is the decorator working, not a missing value.**

### The rules this all comes down to

- No secret in a file in this repository, including parameter files.
- No secret in a template `output` — deployment outputs are stored in the
  deployment history and readable by anyone with read access. The
  `outputs-should-not-contain-secrets` linter rule in `infra/bicepconfig.json`
  fails the build if you try.
- Any parameter that carries a secret is `@secure()`.
- Prefer removing the secret over storing it: a managed identity and a role
  assignment beat the best-kept key.

---

## 8. What you must do once

### For GitHub Actions

```bash
az login
./scripts/setup-github-oidc.sh <subscription-id> enkosi/ocr-ai-vision
```

That creates the app registration, the federated credentials for `dev`, `test`
and `prod`, and assigns two roles at subscription scope:

- **Contributor** — to create the resource group and the resources;
- **Role Based Access Control Administrator** — because `main.bicep` creates a
  role assignment, and creating role assignments is itself a privileged action
  that Contributor does not include.

Then, in GitHub → Settings → Environments, create `dev`, `test` and `prod`; add
`AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID` (the script
prints all three) as **environment** secrets; and add required reviewers to
`prod`.

If an environment has application secrets to place in the vault, add
`ADDITIONAL_SECRETS_JSON` to it as well, holding a JSON object:

```json
{ "Third-Party-Api-Key": "...", "Some-Connection-String": "..." }
```

Each entry becomes a secret of that name in that environment's vault. Nothing
requires it — with the variable unset, the deployment creates an empty vault.

### For Azure DevOps

Create three service connections — `azure-ocrai-dev`, `azure-ocrai-test`,
`azure-ocrai-prod` — of type *Azure Resource Manager* with **Workload identity
federation**, give each the same two roles as above, then create the
Environments `ocrai-dev`, `ocrai-test`, `ocrai-prod` and put an approval check
on `ocrai-prod`.

### Before the first prod run

`F0` (free) Document Intelligence is limited to one per subscription and is
throttled; the parameter files use `S0`. `P1v3` is not free either. Check
`infra/params/*.bicepparam` against what your subscription is willing to pay for
before running the full chain, and change `location` if
`southafricanorth` is not where you want this.

Also note what **purge protection** commits you to. `prod.bicepparam` enables it,
and it cannot be disabled afterwards: that production vault, and every secret
version in it, stays recoverable — and undeletable — for 90 days. That is the
right default for production and the wrong one for an environment you intend to
tear down, which is why dev and test leave it off.

---

## 9. Working with it day to day

```bash
# Preview what a template change would do, without applying it
./scripts/deploy-infra.sh dev --what-if

# Apply it to dev from your machine
./scripts/deploy-infra.sh dev

# Compile and lint the templates the way CI does
az bicep build --file infra/main.bicep --stdout > /dev/null
az bicep build-params --file infra/params/prod.bicepparam --stdout > /dev/null

# Write or rotate a secret (prompts, does not echo, never hits shell history)
./scripts/set-secret.sh prod Third-Party-Api-Key

# List what a vault holds — names and versions, not values
az keyvault secret list --vault-name <vault> --query '[].name' -o tsv
```

`az deployment sub create --parameters infra/params/dev.bicepparam` needs no
`--template-file`: the `using` statement inside the `.bicepparam` names the
template.

**Rotating a secret** needs no deployment at all: Key Vault references are
versionless, so a new secret version is picked up within 24 hours, or at once
with `--restart`. **Recovering a deleted secret** is `az keyvault secret recover`
within the retention window.

**Rolling back.** In prod, `az webapp deployment slot swap ... --target-slot
production` again puts the previous build back. For infrastructure, the
templates are the source of truth — revert the commit and let the pipeline
reconcile, rather than editing anything in the portal. A manual portal change
survives exactly until the next deployment.

---

## 10. Where to take it next

- **Private networking** — VNet integration on the app, private endpoints on the
  vault and the AI account, and `publicNetworkAccess: 'Disabled'` on both. The
  modules already take the parameters this would extend.
- **Automatic rotation** — Key Vault can raise an event a set time before a
  secret expires; an Event Grid subscription and a function can then rotate the
  underlying credential and write the new version. Set `attributes.exp` on the
  secrets to start.
- **Integration tests as a stage** between Dev and Test, running against the
  deployed dev URL. `Program.cs` already exposes `public partial class Program`
  for `WebApplicationFactory`.
- **Deployment stacks** (`az stack sub create`) instead of plain deployments, so
  that deleting a resource from the template deletes it in Azure rather than
  orphaning it.
- **Azure Verified Modules** — replace hand-written modules with the
  Microsoft-maintained `br/public:avm/res/...` registry ones as they cover more
  of what you need.
