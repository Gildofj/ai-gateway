locals {
  required_apis = toset([
    "artifactregistry.googleapis.com",
    "billingbudgets.googleapis.com",
    "iam.googleapis.com",
    "iamcredentials.googleapis.com",
    "logging.googleapis.com",
    "monitoring.googleapis.com",
    "run.googleapis.com",
    "secretmanager.googleapis.com",
    "sts.googleapis.com",
  ])
}

resource "google_project_service" "apis" {
  for_each = local.required_apis

  service            = each.value
  disable_on_destroy = false
}
