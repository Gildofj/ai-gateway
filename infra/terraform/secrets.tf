# Secret *containers* are managed here; their *values* (versions) are added outside
# Terraform so they never touch the state file. Bootstrap each one with:
#
#   echo -n 'sk-...'     | gcloud secrets versions add OPENAI_API_KEY    --data-file=-
#   echo -n 'AIza...'    | gcloud secrets versions add GOOGLE_API_KEY    --data-file=-
#   echo -n 'sk-ant-...' | gcloud secrets versions add ANTHROPIC_API_KEY --data-file=-
#
# Until a real key is added, set "placeholder" (the ProviderRegistry skips placeholder keys).

resource "google_secret_manager_secret" "providers" {
  for_each = toset(var.secret_names)

  secret_id = each.value

  replication {
    auto {}
  }

  depends_on = [google_project_service.apis]
}

# Cloud Run's runtime SA needs to read each secret at request time to mount it as env var.
resource "google_secret_manager_secret_iam_member" "runtime_access" {
  for_each = google_secret_manager_secret.providers

  secret_id = each.value.id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${local.runtime_sa}"
}
