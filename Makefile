RESOURCE_GROUP ?= nextech-rg
LOCATION       ?= eastus
ACR_NAME       ?= nextechregistry
CONTAINER_APP  ?= tom-nextech-api
SWA_NAME       ?= nextech-web
IMAGE          := $(ACR_NAME).azurecr.io/nextech-api:latest

.DEFAULT_GOAL := help
.PHONY: help \
	run run-api run-web \
	test test-api test-api-unit test-api-integration test-web test-e2e \
	infra-providers infra-provision infra-outputs infra-deploy-api infra-deploy-web infra-deploy

help:
	@grep -E '^[a-zA-Z0-9_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  %-22s %s\n", $$1, $$2}'

run-api: ## Start the API locally on http://localhost:5000
	cd api && dotnet run --project src/Nextech.Api

run-web: ## Start the Angular dev server on http://localhost:4200
	cd web && npx ng serve

run: ## Run API and frontend together (Ctrl+C stops both)
	$(MAKE) -j2 run-api run-web

test-api-unit: ## Run API unit tests
	cd api && dotnet test tests/Nextech.Api.UnitTests

test-api-integration: ## Run API integration tests
	cd api && dotnet test tests/Nextech.Api.IntegrationTests

test-api: ## Run all API tests (unit + integration)
	cd api && dotnet test

test-web: ## Run Angular unit tests (Karma, headless)
	cd web && npx ng test --watch=false --browsers=ChromeHeadless

test-e2e: ## Run Playwright E2E tests
	cd web && npx playwright test

test: test-api test-web test-e2e ## Run all tests

infra-providers: ## Register required Azure resource providers
	az provider register --namespace Microsoft.App --wait
	az provider register --namespace Microsoft.ContainerRegistry --wait
	az provider register --namespace Microsoft.Insights --wait

infra-provision: ## Create resource group and deploy Bicep template
	az group create --name $(RESOURCE_GROUP) --location $(LOCATION)
	az deployment group create --resource-group $(RESOURCE_GROUP) --template-file infra/main.bicep

infra-outputs: ## Print deployment outputs (API URL, web URL, ACR name)
	az deployment group show \
		--resource-group $(RESOURCE_GROUP) \
		--name main \
		--query "properties.outputs" -o json

infra-deploy-api: ## Build and push Docker image, update Container App
	az acr login --name $(ACR_NAME)
	docker buildx build --platform linux/amd64 -t $(IMAGE) --load api/
	docker push $(IMAGE)
	az containerapp update \
		--name $(CONTAINER_APP) \
		--resource-group $(RESOURCE_GROUP) \
		--image $(IMAGE)

infra-deploy-web: ## Build Angular app and deploy to Static Web App (requires NG_APP_AI_CONNECTION_STRING env var)
	bash scripts/build-web.sh
	swa deploy web/dist/web/browser \
		--deployment-token "$$(az staticwebapp secrets list --name $(SWA_NAME) --resource-group $(RESOURCE_GROUP) --query 'properties.apiKey' -o tsv)" \
		--env production

infra-deploy: infra-provision infra-deploy-api infra-deploy-web ## Full deploy: provision + API + frontend
