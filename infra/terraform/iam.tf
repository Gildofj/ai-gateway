resource "google_service_account" "deployer" {
  account_id   = var.deployer_account_id
  display_name = "GitHub Actions deployer"
  description  = "Used by the GitHub Actions deploy workflow via Workload Identity Federation."

  depends_on = [google_project_service.apis]
}

locals {
  # Roles required for the deploy pipeline: build runs on a GitHub-hosted runner,
  # then pushes directly to Artifact Registry and calls `gcloud run deploy`. No
  # Cloud Build / GCS source upload involved → those roles are gone.
  deployer_roles = toset([
    "roles/artifactregistry.writer",
    "roles/run.admin",
    "roles/iam.serviceAccountUser",
    "roles/secretmanager.secretAccessor",
    "roles/secretmanager.admin",
    "roles/logging.logWriter",
  ])
}

resource "google_project_iam_member" "deployer_roles" {
  for_each = local.deployer_roles

  project = var.project_id
  role    = each.value
  member  = "serviceAccount:${google_service_account.deployer.email}"
}
