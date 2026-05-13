# Deployment — Google Cloud Run (free tier)

Deploy the gateway to **Google Cloud Run** and stay at **$0/month** under hobby usage:

- **Cloud Run free tier**: 2 M requests, 360k vCPU-s, 180k GiB-s, 1 GiB egress/month from NA
- **Build pipeline**: GitHub Actions only — no Cloud Build, no paid builders
- **Image storage**: Artifact Registry, 0.5 GB free; lifecycle policy prunes anything beyond `latest` + 3 most-recent tags
- **Cold start ≈ 2–5 s** — acceptable next to multi-second LLM latency
- **Budget guardrail**: a $1 budget alert wired to your email, fires at 50/90/100% of spend

This guide assumes a working `gcloud` CLI and a Google Cloud project. Alternative free-tier targets are listed at the end.

## 1. One-time setup

```bash
export PROJECT_ID="your-gcp-project"
export REGION="us-central1"           # us-central1, us-east1, europe-west1 are free-tier eligible
export SERVICE="ai-gateway"
export REPO="ai-gateway"

gcloud config set project "$PROJECT_ID"

# Enable the APIs the gateway needs at runtime (Terraform also enables them — this is a fallback)
gcloud services enable \
  run.googleapis.com \
  artifactregistry.googleapis.com \
  secretmanager.googleapis.com \
  billingbudgets.googleapis.com \
  monitoring.googleapis.com

# Create the image repository
gcloud artifacts repositories create "$REPO" \
  --repository-format=docker \
  --location="$REGION" \
  --description="AI Gateway container images"
```

> The cleanest path is to run `terraform apply` in `infra/terraform/` — that creates the APIs, repository (with cleanup policy), service accounts, WIF binding, secret containers, **and** the $1 budget alert in one shot. See `infra/terraform/README.md`.

## 2. Store API keys in Secret Manager

Never bake keys into the image or pass them as plaintext env vars. Use Secret Manager — Cloud Run mounts them as env vars at runtime.

```bash
# Provider keys
printf '%s' "sk-..."     | gcloud secrets create OPENAI_API_KEY    --data-file=-
printf '%s' "AIza..."    | gcloud secrets create GOOGLE_API_KEY    --data-file=-
printf '%s' "sk-ant-..." | gcloud secrets create ANTHROPIC_API_KEY --data-file=-

# Gateway API key — required to keep the public Cloud Run URL from draining your free tier
openssl rand -hex 32 | gcloud secrets create GATEWAY_API_KEY --data-file=-
# Save the random value somewhere safe — clients pass it as X-API-Key on every request
gcloud secrets versions access latest --secret=GATEWAY_API_KEY
```

Grant the runtime service account permission to read each secret. Terraform does this automatically; if you set things up by hand:

```bash
PROJECT_NUMBER=$(gcloud projects describe "$PROJECT_ID" --format='value(projectNumber)')
RUNTIME_SA="${PROJECT_NUMBER}-compute@developer.gserviceaccount.com"

for SECRET in OPENAI_API_KEY GOOGLE_API_KEY ANTHROPIC_API_KEY GATEWAY_API_KEY; do
  gcloud secrets add-iam-policy-binding "$SECRET" \
    --member="serviceAccount:${RUNTIME_SA}" \
    --role=roles/secretmanager.secretAccessor
done
```

> Only create the secrets you actually have keys for. Cloud Run will fail to start if it tries to mount a missing secret — drop the relevant entries from the `--set-secrets=...` flag in `.github/workflows/deploy.yml` for any provider you skip. `GATEWAY_API_KEY` is the only one that's mandatory.

## 3. Deploy

### Option A — continuous deploy via GitHub Actions (recommended)

The workflow at `.github/workflows/deploy.yml` builds the container image **on the GitHub-hosted runner**, pushes it to Artifact Registry, and calls `gcloud run deploy`. No Cloud Build, no paid build minutes.

It triggers on every push to `main` that touches `src/`, `Dockerfile`, or the workflow itself, and is also manually runnable from the Actions tab.

**One-time setup (5 minutes, manual path):**

```bash
export PROJECT_ID="your-gcp-project"
export GH_REPO="Gildofj/AIGateway"    # owner/repo
PROJECT_NUMBER=$(gcloud projects describe "$PROJECT_ID" --format='value(projectNumber)')

# 1. Create a deployer service account
gcloud iam service-accounts create github-deployer \
  --display-name="GitHub Actions deployer"

SA_EMAIL="github-deployer@${PROJECT_ID}.iam.gserviceaccount.com"

# 2. Grant the SA only what the workflow needs
for ROLE in \
  roles/artifactregistry.writer \
  roles/run.admin \
  roles/iam.serviceAccountUser \
  roles/secretmanager.secretAccessor \
  roles/logging.logWriter; do
  gcloud projects add-iam-policy-binding "$PROJECT_ID" \
    --member="serviceAccount:${SA_EMAIL}" --role="$ROLE"
done

# 3. Create a Workload Identity Pool + provider
gcloud iam workload-identity-pools create github \
  --location=global --display-name="GitHub Actions"

gcloud iam workload-identity-pools providers create-oidc github \
  --location=global \
  --workload-identity-pool=github \
  --display-name="GitHub" \
  --issuer-uri="https://token.actions.githubusercontent.com" \
  --attribute-mapping="google.subject=assertion.sub,attribute.repository=assertion.repository,attribute.repository_owner=assertion.repository_owner" \
  --attribute-condition="assertion.repository_owner == '$(echo $GH_REPO | cut -d/ -f1)'"

# 4. Allow the GitHub repo to impersonate the SA
gcloud iam service-accounts add-iam-policy-binding "$SA_EMAIL" \
  --role=roles/iam.workloadIdentityUser \
  --member="principalSet://iam.googleapis.com/projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/github/attribute.repository/${GH_REPO}"

# 5. Print the values to paste into GitHub Secrets
echo ""
echo "Set these as GitHub repository secrets (Settings → Secrets and variables → Actions):"
echo "  GCP_PROJECT_ID            = ${PROJECT_ID}"
echo "  GCP_WIF_SERVICE_ACCOUNT   = ${SA_EMAIL}"
echo "  GCP_WIF_PROVIDER          = projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/github/providers/github"
```

> Using Terraform? All of the above is declared in `infra/terraform/`. Run `terraform output github_secrets` after `apply` to get the values and skip to "After the three secrets are set" below.

After the three secrets are set, push any commit to `main` — the workflow builds, deploys, and prints the service URL in the GitHub job summary. For a manual run, use **Actions → Deploy to Cloud Run → Run workflow** (optionally with a custom tag).

### Option B — manual one-shot from your machine

The Makefile mirrors the workflow: build on your machine, push, deploy. No Cloud Build.

```bash
make deploy                    # uses git short SHA as tag
make deploy TAG=experiment     # custom tag
```

Or run the underlying commands by hand — see `Makefile`'s `deploy` target.

## 4. Custom domain (optional)

```bash
gcloud run domain-mappings create \
  --service="$SERVICE" \
  --domain="api.yourdomain.com" \
  --region="$REGION"
```

Follow the printed DNS instructions (one `CNAME` or four `A`/`AAAA` records). HTTPS is provisioned automatically.

## 5. Resource sizing — tuned for free tier

Set in `.github/workflows/deploy.yml` (and `Makefile` for the manual path):

| Flag | Value | Why |
|---|---|---|
| `--memory=512Mi` | 512 MiB | .NET runtime needs ~250 MB; 512 leaves headroom without doubling GiB-s billing |
| `--cpu=1` | 1 vCPU | One concurrent provider call rarely needs more |
| `--concurrency=80` | 80 req/instance | Each request awaits an LLM call (long I/O); high concurrency keeps instance count low |
| `--min-instances=0` | scales to zero | Required for free tier — accept cold start |
| `--max-instances=2` | hard cap | Sustained abuse caps at 2 vCPU continuously — still well below the 360k vCPU-s monthly free quota |
| `--timeout=60` | 1 min | Shorter than default 300 s — caps blast radius of any single hung request |

If you want zero cold start at higher cost: `--min-instances=1` (~$5/month for a 512 MiB instance). That **will** exit the free tier.

## 6. Observability

```bash
gcloud run services logs read "$SERVICE" --region="$REGION" --limit=50

# Tail
gcloud run services logs tail "$SERVICE" --region="$REGION"
```

Metrics (request count, latency, error rate, billable-time) are in the Cloud Run console → service → **Metrics**.

## 7. Cost guardrails (how this stays at $0)

Three layers, in order of which fires first:

### 7a. `GATEWAY_API_KEY` auth (in-process)

The gateway rejects every `/api/*` call without a matching `X-API-Key` header (constant-time compare). The Cloud Run URL is still public for HTTPS termination, but requests without the key cost a few µs of CPU and return 401 — they cannot reach an upstream LLM and cannot drain billing-significant resources.

```bash
# Client side
curl -X POST https://ai-gateway-xxxx-uc.a.run.app/api/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "X-API-Key: $GATEWAY_API_KEY" \
  -d '{"prompt":"Hi"}'
```

### 7b. Cloud Run resource caps (per-revision)

The deploy flags above cap pathological behaviour: at most 2 instances × 1 vCPU × 60 s timeout. Worst-case sustained: 2 × 86 400 s × 30 days = ~5 M vCPU-s. That **would** exceed free tier — the API key gate above is what makes that scenario unreachable in practice.

A single legitimate LLM round-trip uses ~1.5 GiB-s, so you can serve ~120 k requests/month before paying for compute. (The dominant cost remains the upstream LLM provider's per-token billing — that's separate.)

### 7c. Billing budget alert (last line of defence)

Terraform creates a $1 monthly budget on the project with email alerts at 50/90/100% of spend. **The point is to stay at $0** — any alert means free tier was missed and something needs investigation.

```bash
# Manual setup (if you skipped Terraform):
gcloud billing budgets create \
  --billing-account=$(gcloud billing accounts list --format='value(name)' --limit=1) \
  --display-name="ai-gateway-free-tier-guardrail" \
  --budget-amount=1USD \
  --threshold-rule=percent=50 \
  --threshold-rule=percent=90 \
  --threshold-rule=percent=100
```

> Budget alerts **notify** — they don't stop billing. For a real kill switch, wire a Pub/Sub topic on the budget to a Cloud Function that calls `cloudbilling.projects.updateBillingInfo` to detach billing. We don't ship that by default because it can render the service unreachable.

### 7d. Image storage doesn't drift

Artifact Registry's free tier is 0.5 GB. `infra/terraform/artifact_registry.tf` declares a cleanup policy that keeps `latest` + the 3 most-recent tagged versions and deletes anything else after 1 day. At ~120 MB per image, you cannot exceed the free allotment without disabling the policy.

## Alternatives

| Platform | Free allowance | Cold start | Tradeoff |
|---|---|---|---|
| **Google Cloud Run** (this guide) | 2 M req/month, scales to zero | 2–5 s | Best balance for I/O-bound APIs |
| **Oracle Cloud Always Free** | 4 ARM vCPUs / 24 GiB RAM VM, always on | none | Self-managed VM (systemd, nginx, certs) |
| **Azure Container Apps** | 180 k vCPU-s/month | 2–5 s | Similar to Cloud Run; tighter Microsoft ecosystem fit |
| **Fly.io** | 3 shared-cpu-1x VMs | none | Now requires a credit card on file |
| **Render Web Service** | 750 hours/month, sleeps after 15 min idle | 30–60 s | Long cold start is painful for an API gateway |

For the Oracle path, build the image with the same `Dockerfile`, `docker compose up` on the VM, and front it with Caddy for free automatic HTTPS.
