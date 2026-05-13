variable "project_id" {
  type        = string
  description = "GCP project ID hosting the gateway."
}

variable "region" {
  type        = string
  description = "Region for Artifact Registry and Cloud Run."
  default     = "us-central1"
}

variable "github_owner" {
  type        = string
  description = "GitHub owner/organization that may impersonate the deployer SA (case-sensitive — must match the OIDC claim)."
}

variable "github_repo" {
  type        = string
  description = "GitHub repository name (case-sensitive)."
}

variable "service_name" {
  type        = string
  description = "Cloud Run service name."
  default     = "ai-gateway"
}

variable "artifact_repository" {
  type        = string
  description = "Artifact Registry repository name (created here, consumed by the deploy workflow)."
  default     = "ai-gateway"
}

variable "secret_names" {
  type        = list(string)
  description = "Secret Manager containers to create. Values (secret versions) are managed outside Terraform — see README."
  default     = ["OPENAI_API_KEY", "GOOGLE_API_KEY", "ANTHROPIC_API_KEY", "GATEWAY_API_KEY"]
}

variable "deployer_account_id" {
  type        = string
  description = "Account ID (left side of @) for the deployer service account."
  default     = "github-deployer"
}

variable "wif_pool_id" {
  type        = string
  description = "Workload Identity Pool ID."
  default     = "github"
}

variable "wif_provider_id" {
  type        = string
  description = "Workload Identity Pool Provider ID."
  default     = "github"
}

variable "billing_account_id" {
  type        = string
  description = "Billing account ID (e.g. 0X0X0X-XXXXXX-XXXXXX). Run `gcloud billing accounts list` to find it. Required for the free-tier guardrail budget."
}

variable "alert_email" {
  type        = string
  description = "Email that receives budget alerts at 50/90/100% of the monthly cap."
}

variable "monthly_budget_usd" {
  type        = number
  description = "Hard monthly cap in USD. The point is to STAY at $0 — alerts fire as soon as anything is billed."
  default     = 1
}
