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
    document-intelligence.bicep     the AI service the API calls
    app-service.bicep               Linux plan, web app, optional staging slot
    cognitive-services-role.bicep   RBAC: app identity → AI account
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
```

Two pipelines are included because "Azure CI/CD" means both things in practice.
They are the same five stages expressed in two dialects — pick one, read the
other for contrast, delete whichever you do not use.

---

## 3. The architecture that gets provisioned

```
  Resource group  rg-ocrai-<env>
  ┌──────────────────────────────────────────────────────────────────┐
  │                                                                  │
  │   App Service plan (Linux)                                       │
  │   ┌────────────────────────────────────────────┐                 │
  │   │  Web app  app-ocrai-<env>-<token>          │                 │
  │   │  .NET 8 · HTTPS only · health check /health│                 │
  │   │  System-assigned managed identity ●────────┼──┐              │
  │   │                                            │  │              │
  │   │  [prod only] staging slot ●────────────────┼──┤ RBAC:        │
  │   └────────────────────────────────────────────┘  │ Cognitive    │
  │                                                   │ Services     │
  │   Document Intelligence  di-ocrai-<env>-<token> ◄─┘ User         │
  │   kind: FormRecognizer · custom subdomain · keys disabled        │
  │                                                                  │
  │   Application Insights ──► Log Analytics workspace               │
  │        ▲                         ▲                               │
  │        └── app telemetry         └── platform + AI diagnostics    │
  └──────────────────────────────────────────────────────────────────┘
```

### Why each resource is there

| Resource | Why |
| --- | --- |
| **Resource group** | Created by `main.bicep` itself, at subscription scope, so the group's name, region and tags are code too. |
| **Log Analytics workspace** | One destination for every log. Created first; everything else points at it. |
| **Application Insights** | Workspace-based (the classic mode is retired). Its connection string is injected into the app as `APPLICATIONINSIGHTS_CONNECTION_STRING`. |
| **Document Intelligence account** | `Microsoft.CognitiveServices/accounts`, `kind: 'FormRecognizer'`. `customSubDomainName` is set because **Entra ID authentication does not work without it** — with only the regional endpoint the account accepts keys and nothing else. `disableLocalAuth: true` in prod turns keys off entirely. |
| **App Service plan + web app** | Linux (`reserved: true`), `DOTNETCORE|8.0`, `httpsOnly`, TLS 1.2 floor, FTPS disabled, and `healthCheckPath: '/health'` — which is the endpoint `Program.cs` already maps, so App Service takes an unhealthy instance out of rotation by itself. |
| **Staging slot** (prod) | A second copy of the site on the same plan. Deploy there, warm it, smoke test it, then swap. |
| **Role assignment** | `Cognitive Services User` on the AI account, granted to the web app's managed identity. This is the piece that lets `DocumentIntelligence:ApiKey` stay empty. |

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
also how ordering is expressed. There is no `dependsOn` anywhere in these
templates: because App Service needs the Document Intelligence endpoint, and
Document Intelligence needs the workspace id, Bicep infers the graph and
deploys what it can in parallel. Explicit `dependsOn` is for the rare case where
a dependency exists that the data flow does not show.

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

## 6. Authentication: no secrets anywhere

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

## 7. What you must do once

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

---

## 8. Working with it day to day

```bash
# Preview what a template change would do, without applying it
./scripts/deploy-infra.sh dev --what-if

# Apply it to dev from your machine
./scripts/deploy-infra.sh dev

# Compile and lint the templates the way CI does
az bicep build --file infra/main.bicep --stdout > /dev/null
az bicep build-params --file infra/params/prod.bicepparam --stdout > /dev/null
```

`az deployment sub create --parameters infra/params/dev.bicepparam` needs no
`--template-file`: the `using` statement inside the `.bicepparam` names the
template.

**Rolling back.** In prod, `az webapp deployment slot swap ... --target-slot
production` again puts the previous build back. For infrastructure, the
templates are the source of truth — revert the commit and let the pipeline
reconcile, rather than editing anything in the portal. A manual portal change
survives exactly until the next deployment.

---

## 9. Where to take it next

- **Private networking** — VNet integration on the app, a private endpoint on
  the AI account, and `publicNetworkAccess: 'Disabled'`. The modules already
  take the parameters this would extend.
- **Integration tests as a stage** between Dev and Test, running against the
  deployed dev URL. `Program.cs` already exposes `public partial class Program`
  for `WebApplicationFactory`.
- **Deployment stacks** (`az stack sub create`) instead of plain deployments, so
  that deleting a resource from the template deletes it in Azure rather than
  orphaning it.
- **Azure Verified Modules** — replace hand-written modules with the
  Microsoft-maintained `br/public:avm/res/...` registry ones as they cover more
  of what you need.
