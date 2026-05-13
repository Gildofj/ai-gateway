terraform {
  required_version = ">= 1.7"

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 6.0"
    }
  }

  # Uncomment after creating a GCS bucket (see README §State backend) to share state.
  # backend "gcs" {
  #   bucket = "ai-gateway-tfstate"
  #   prefix = "infra"
  # }
}
