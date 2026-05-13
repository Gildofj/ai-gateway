output "github_secrets" {
  description = "Paste these into GitHub repo → Settings → Secrets and variables → Actions."
  value = {
    GCP_PROJECT_ID          = var.project_id
    GCP_WIF_SERVICE_ACCOUNT = google_service_account.deployer.email
    GCP_WIF_PROVIDER        = local.wif_provider_resource
  }
}

output "artifact_repository" {
  description = "Full Artifact Registry image path prefix."
  value       = "${var.region}-docker.pkg.dev/${var.project_id}/${google_artifact_registry_repository.images.repository_id}"
}

output "deployer_service_account" {
  description = "Email of the GitHub Actions deployer SA."
  value       = google_service_account.deployer.email
}

output "runtime_service_account" {
  description = "Cloud Run runtime SA (default Compute Engine SA) that reads secrets at request time."
  value       = local.runtime_sa
}
