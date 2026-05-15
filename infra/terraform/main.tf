provider "google" {
  project = var.project_id
  region  = var.region

  # Required for managing billing budgets via local ADC
  user_project_override = true
  billing_project       = var.project_id
}

data "google_project" "this" {}

locals {
  project_number = data.google_project.this.number
  runtime_sa     = "${local.project_number}-compute@developer.gserviceaccount.com"

  github_principal_set = "principalSet://iam.googleapis.com/projects/${local.project_number}/locations/global/workloadIdentityPools/${google_iam_workload_identity_pool.github.workload_identity_pool_id}/attribute.repository/${var.github_owner}/${var.github_repo}"

  wif_provider_resource = "projects/${local.project_number}/locations/global/workloadIdentityPools/${google_iam_workload_identity_pool.github.workload_identity_pool_id}/providers/${google_iam_workload_identity_pool_provider.github.workload_identity_pool_provider_id}"
}

resource "google_firestore_database" "database" {
  project     = var.project_id
  name        = "(default)"
  location_id = var.region
  type        = "FIRESTORE_NATIVE"

  depends_on = [google_project_service.apis]
}
