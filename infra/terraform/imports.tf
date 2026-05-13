# One-shot import blocks for adopting the resources that already exist in GCP.
# After the first successful `terraform apply`, delete this file.

# ---- APIs ----------------------------------------------------------------

import {
  to = google_project_service.apis["artifactregistry.googleapis.com"]
  id = "gildofj/artifactregistry.googleapis.com"
}
import {
  to = google_project_service.apis["iam.googleapis.com"]
  id = "gildofj/iam.googleapis.com"
}
import {
  to = google_project_service.apis["iamcredentials.googleapis.com"]
  id = "gildofj/iamcredentials.googleapis.com"
}
import {
  to = google_project_service.apis["logging.googleapis.com"]
  id = "gildofj/logging.googleapis.com"
}
import {
  to = google_project_service.apis["run.googleapis.com"]
  id = "gildofj/run.googleapis.com"
}
import {
  to = google_project_service.apis["secretmanager.googleapis.com"]
  id = "gildofj/secretmanager.googleapis.com"
}
import {
  to = google_project_service.apis["sts.googleapis.com"]
  id = "gildofj/sts.googleapis.com"
}

# ---- Artifact Registry ---------------------------------------------------

import {
  to = google_artifact_registry_repository.images
  id = "projects/gildofj/locations/us-central1/repositories/ai-gateway"
}

# ---- Deployer SA ---------------------------------------------------------

import {
  to = google_service_account.deployer
  id = "projects/gildofj/serviceAccounts/github-deployer@gildofj.iam.gserviceaccount.com"
}

# ---- Secrets -------------------------------------------------------------

import {
  to = google_secret_manager_secret.providers["OPENAI_API_KEY"]
  id = "projects/gildofj/secrets/OPENAI_API_KEY"
}
import {
  to = google_secret_manager_secret.providers["GOOGLE_API_KEY"]
  id = "projects/gildofj/secrets/GOOGLE_API_KEY"
}
import {
  to = google_secret_manager_secret.providers["ANTHROPIC_API_KEY"]
  id = "projects/gildofj/secrets/ANTHROPIC_API_KEY"
}

# ---- Workload Identity Federation ---------------------------------------

import {
  to = google_iam_workload_identity_pool.github
  id = "projects/gildofj/locations/global/workloadIdentityPools/github"
}

import {
  to = google_iam_workload_identity_pool_provider.github
  id = "projects/gildofj/locations/global/workloadIdentityPools/github/providers/github"
}
