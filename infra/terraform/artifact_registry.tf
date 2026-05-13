resource "google_artifact_registry_repository" "images" {
  location      = var.region
  repository_id = var.artifact_repository
  format        = "DOCKER"
  description   = "AI Gateway container images"

  # Free tier is 0.5 GB total. .NET aspnet:alpine images sit at ~120 MB each,
  # so we keep `latest` + the 3 most-recent SHA tags and let everything else age out.
  cleanup_policy_dry_run = false

  cleanup_policies {
    id     = "keep-latest-tag"
    action = "KEEP"
    condition {
      tag_state    = "TAGGED"
      tag_prefixes = ["latest"]
    }
  }

  cleanup_policies {
    id     = "keep-recent-versions"
    action = "KEEP"
    most_recent_versions {
      keep_count = 3
    }
  }

  cleanup_policies {
    id     = "delete-stale"
    action = "DELETE"
    condition {
      older_than = "86400s" # 1 day — old versions not covered by the KEEP rules
    }
  }

  depends_on = [google_project_service.apis]
}
