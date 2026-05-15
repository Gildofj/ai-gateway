// Free-tier guardrail.
//
// Cloud Billing has no native hard-stop, but this combo gives us early warning:
//   - One $1 monthly budget tracked against the project
//   - Email alerts at 50 / 90 / 100% (i.e. $0.50, $0.90, $1.00 of spend)
//
// The whole point of this stack is to stay at $0 — any alert means something
// slipped past the free tier and needs investigation. If you want a real
// kill switch, wire a Pub/Sub topic + Cloud Function that calls
// `cloudbilling.projects.updateBillingInfo` to detach billing. We intentionally
// don't ship that here because it can render the service unreachable.

resource "google_monitoring_notification_channel" "budget_email" {
  display_name = "AI Gateway budget alerts"
  type         = "email"

  labels = {
    email_address = var.alert_email
  }

  depends_on = [google_project_service.apis]
}

resource "google_billing_budget" "free_tier_guardrail" {
  billing_account = var.billing_account_id
  display_name    = "ai-gateway-free-tier-guardrail"

  budget_filter {
    projects = ["projects/${local.project_number}"]
  }

  amount {
    specified_amount {
      currency_code = "BRL"
      units         = "10"
    }
  }

  threshold_rules {
    threshold_percent = 0.5
  }
  threshold_rules {
    threshold_percent = 0.9
  }
  threshold_rules {
    threshold_percent = 1.0
  }

  all_updates_rule {
    monitoring_notification_channels = [google_monitoring_notification_channel.budget_email.id]
    disable_default_iam_recipients   = true
  }

  depends_on = [google_project_service.apis]
}
