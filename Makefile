# AI Gateway — developer Makefile
# Run `make` or `make help` to see all targets.

# ---- Configuration ----------------------------------------------------------

PROJECT_DIR  := src/AiGateway.Api
PROJECT      := $(PROJECT_DIR)/AiGateway.Api.csproj
IMAGE_NAME   := ai-gateway

# Cloud Run / GCP defaults — override via `make deploy REGION=europe-west1`
REGION       ?= us-central1
REPOSITORY   ?= ai-gateway
SERVICE      ?= ai-gateway
PORT         ?= 8080

# Image tag — git short SHA when available, otherwise "dev"
TAG          ?= $(shell git rev-parse --short HEAD 2>/dev/null || echo dev)

# Auto-load .env (keys, model overrides) into Make and child processes
ifneq (,$(wildcard ./.env))
    include .env
    export
endif

.DEFAULT_GOAL := help

# ---- Help -------------------------------------------------------------------

.PHONY: help
help: ## Show this help
	@echo "AI Gateway — make targets"
	@echo ""
	@awk 'BEGIN {FS = ":.*## "} /^[a-zA-Z_-]+:.*## / {printf "  \033[36m%-18s\033[0m %s\n", $$1, $$2}' $(MAKEFILE_LIST)
	@echo ""
	@echo "Override defaults: make deploy REGION=europe-west1 SERVICE=my-gateway"

# ---- Dev loop ---------------------------------------------------------------

.PHONY: restore build run watch format clean

restore: ## Restore NuGet packages
	dotnet restore $(PROJECT)

build: ## Build the API (Release)
	dotnet build $(PROJECT) -c Release

run: ## Run the API on http://localhost:5042
	dotnet run --project $(PROJECT)

watch: ## Run with hot reload
	dotnet watch --project $(PROJECT)

format: ## Apply dotnet format
	dotnet format $(PROJECT)

clean: ## Remove build artifacts
	dotnet clean $(PROJECT)

# ---- Container --------------------------------------------------------------

.PHONY: docker-build docker-run docker-stop docker-shell

docker-build: ## Build the production container image
	docker build -t $(IMAGE_NAME):$(TAG) -t $(IMAGE_NAME):latest .

docker-run: docker-build ## Build & run the container on $(PORT) with .env loaded
	docker run --rm -p $(PORT):8080 \
		-e OPENAI_API_KEY="$(OPENAI_API_KEY)" \
		-e GOOGLE_API_KEY="$(GOOGLE_API_KEY)" \
		-e ANTHROPIC_API_KEY="$(ANTHROPIC_API_KEY)" \
		-e GATEWAY_API_KEY="$(GATEWAY_API_KEY)" \
		--name $(IMAGE_NAME) \
		$(IMAGE_NAME):latest

docker-stop: ## Stop the running container
	-docker stop $(IMAGE_NAME)

docker-shell: ## Open a shell in a fresh container (debugging)
	docker run --rm -it --entrypoint sh $(IMAGE_NAME):latest

# ---- Cloud deploy -----------------------------------------------------------
# CI/CD lives in .github/workflows/deploy.yml — it builds on a GitHub runner
# and pushes directly to Artifact Registry. The targets below are a manual
# fallback for the same flow: build → push → deploy. They do NOT use Cloud
# Build, so they don't burn the paid E2_HIGHCPU_8 builder.

IMAGE_PATH := $(REGION)-docker.pkg.dev/$$(gcloud config get-value project)/$(REPOSITORY)/$(SERVICE)

.PHONY: deploy deploy-manual url logs logs-tail smoke

deploy: ## Manual build → push → deploy with the current git SHA as tag
	gcloud auth configure-docker $(REGION)-docker.pkg.dev --quiet
	docker build -t $(IMAGE_PATH):$(TAG) -t $(IMAGE_PATH):latest .
	docker push $(IMAGE_PATH):$(TAG)
	docker push $(IMAGE_PATH):latest
	gcloud run deploy $(SERVICE) \
		--image=$(IMAGE_PATH):$(TAG) \
		--region=$(REGION) \
		--platform=managed \
		--allow-unauthenticated \
		--port=8080 \
		--memory=512Mi --cpu=1 \
		--concurrency=80 \
		--min-instances=0 --max-instances=2 \
		--timeout=60 \
		--set-env-vars=ASPNETCORE_ENVIRONMENT=Production \
		--set-secrets=OPENAI_API_KEY=OPENAI_API_KEY:latest,GOOGLE_API_KEY=GOOGLE_API_KEY:latest,ANTHROPIC_API_KEY=ANTHROPIC_API_KEY:latest,GATEWAY_API_KEY=GATEWAY_API_KEY:latest

deploy-manual: TAG=manual
deploy-manual: deploy ## Same as deploy, but pinned to the :manual tag

url: ## Print the deployed Cloud Run URL
	@gcloud run services describe $(SERVICE) --region=$(REGION) --format='value(status.url)'

logs: ## Show last 50 Cloud Run log entries
	gcloud run services logs read $(SERVICE) --region=$(REGION) --limit=50

logs-tail: ## Tail Cloud Run logs (Ctrl+C to stop)
	gcloud run services logs tail $(SERVICE) --region=$(REGION)

smoke: ## Send a smoke-test request to the deployed service
	@URL=$$(gcloud run services describe $(SERVICE) --region=$(REGION) --format='value(status.url)') && \
	echo "POST $$URL/api/v1/chat/completions" && \
	curl -sS -X POST "$$URL/api/v1/chat/completions" \
		-H "Content-Type: application/json" \
		-d '{"prompt":"Write a haiku about deployment."}'

# ---- Terraform --------------------------------------------------------------

TF_DIR := infra/terraform

.PHONY: tf-init tf-plan tf-apply tf-output tf-fmt tf-destroy

tf-init: ## Initialize Terraform (downloads providers, sets up backend)
	cd $(TF_DIR) && terraform init

tf-plan: ## Show what Terraform will change
	cd $(TF_DIR) && terraform plan

tf-apply: ## Apply Terraform changes
	cd $(TF_DIR) && terraform apply

tf-output: ## Print the GitHub secret values from Terraform outputs
	cd $(TF_DIR) && terraform output -json github_secrets

tf-fmt: ## Format Terraform files
	cd $(TF_DIR) && terraform fmt -recursive

tf-destroy: ## Destroy Terraform-managed infra (does NOT touch Cloud Run service or secret values)
	cd $(TF_DIR) && terraform destroy
