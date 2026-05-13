# Terraform — AI Gateway platform infra

Declarative source of truth for the GCP platform that backs the gateway:

- **APIs** enabled (Run, Artifact Registry, Secret Manager, IAM, IAM Credentials, STS, Logging, Monitoring, Billing Budgets)
- **Artifact Registry** repository for container images, with a cleanup policy that keeps `latest` + the 3 most-recent SHA tags
- **Service account** `github-deployer` + minimal project IAM bindings (no Cloud Build roles — CI builds on the GitHub runner)
- **Workload Identity Federation** pool, provider, and repo binding
- **Secret Manager** containers (values managed separately — never enter state) including `GATEWAY_API_KEY`
- **Billing budget** + email notification channel — $1 monthly cap with alerts at 50/90/100% of spend

The Cloud Run **service** itself is intentionally **not** managed here — the CI pipeline (`.github/workflows/deploy.yml`) creates and updates revisions imperatively. Mixing the two is a recipe for drift.

## Layout

| File | Purpose |
|---|---|
| `versions.tf` | Terraform + provider version pins |
| `main.tf` | Provider config, project data source, computed locals |
| `variables.tf` | Inputs (project ID, region, GitHub owner/repo, etc.) |
| `apis.tf` | `google_project_service` for each required API |
| `artifact_registry.tf` | Docker repository |
| `iam.tf` | Deployer service account + project-level role bindings |
| `wif.tf` | Workload Identity Pool, OIDC provider, repo binding |
| `secrets.tf` | Secret Manager containers + runtime SA accessor bindings |
| `billing.tf` | Budget + email notification channel — free-tier guardrail |
| `outputs.tf` | Values to paste into GitHub Secrets, deployer email, image path |
| `imports.tf` | One-shot import blocks for the resources created manually |
| `terraform.tfvars.example` | Sample inputs — copy to `terraform.tfvars` and edit |

## Prerequisites

- Terraform >= 1.7
- `gcloud` authenticated with a user that has Owner or Editor on the project
- Application Default Credentials configured:

```bash
gcloud auth application-default login
```

## Quickstart (greenfield project)

```bash
cd infra/terraform
cp terraform.tfvars.example terraform.tfvars
# edit terraform.tfvars

# If this is a brand-new project (nothing exists yet in GCP),
# DELETE imports.tf first — its blocks reference resources that don't exist.
rm imports.tf

terraform init
terraform plan
terraform apply
terraform output github_secrets
```

## Adopting the existing project (this repo's case)

The platform was bootstrapped manually before Terraform existed. `imports.tf` makes the first plan import each existing resource into state instead of trying to recreate it.

```bash
cd infra/terraform
cp terraform.tfvars.example terraform.tfvars

terraform init
terraform plan         # should show "X to import, 0 to add, 0 to change, 0 to destroy"
terraform apply        # commits the imports + reconciles drift, if any

# After the first apply, the imports are baked into state — delete the file:
rm imports.tf
git add -A && git commit -m "tf: drop one-shot imports after adoption"
```

If `terraform plan` shows resources to **destroy** before the imports run, stop and investigate — likely the IDs in `imports.tf` don't match the live names.

## State backend (optional but recommended)

Local state is fine for solo work. For a team or for CI to apply, move state to GCS:

```bash
PROJECT_ID=$(terraform output -raw -no-color project_id 2>/dev/null || gcloud config get-value project)
BUCKET="${PROJECT_ID}-tfstate"

gcloud storage buckets create "gs://${BUCKET}" \
  --location=us-central1 \
  --uniform-bucket-level-access \
  --public-access-prevention

gcloud storage buckets update "gs://${BUCKET}" --versioning
```

Then uncomment the `backend "gcs"` block in `versions.tf`, replace the bucket name, and run `terraform init -migrate-state`.

## Managing secret values

Secret *containers* are in Terraform; secret *versions* (the actual key strings) are not — they would leak into the state file.

```bash
# Replace any of these whenever a key rotates:
printf '%s' 'sk-...'     | gcloud secrets versions add OPENAI_API_KEY    --data-file=-
printf '%s' 'AIza...'    | gcloud secrets versions add GOOGLE_API_KEY    --data-file=-
printf '%s' 'sk-ant-...' | gcloud secrets versions add ANTHROPIC_API_KEY --data-file=-
```

To add a new provider's secret, append its name to `var.secret_names` in `terraform.tfvars` and run `terraform apply`. Cloud Run automatically gets read access. Then update `cloudbuild.yaml`'s `--set-secrets=...` flag to mount it.

## Outputs

```bash
terraform output -json github_secrets
```

Returns the three GitHub repo secrets you need to set:

- `GCP_PROJECT_ID`
- `GCP_WIF_SERVICE_ACCOUNT`
- `GCP_WIF_PROVIDER`

## Destroy

```bash
terraform destroy
```

Will tear down everything Terraform owns. It **will not** delete:

- Cloud Run services (managed by the pipeline)
- Secret values (versions of `google_secret_manager_secret` — though the containers themselves will be removed)
- GCS state bucket if you migrated state there

The APIs are configured with `disable_on_destroy = false` so they stay enabled — disabling them can break other workloads in the same project.
