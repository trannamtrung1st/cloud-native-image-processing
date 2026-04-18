# cloud-native-image-processing

Cloud-native image library: users sign in with **ASP.NET Core Identity**, upload images, optionally apply processing (e.g. grayscale), and get **AI-generated descriptions** (Azure Computer Vision). Images and metadata live in **Azure Blob Storage** and **PostgreSQL**; processing is driven by **Event Hubs** workers.

## # Features

- User registration and login with local ASP.NET Core Identity
- Secure user session management and logout
- Image upload with optional processing during upload
- Built-in grayscale image processing option
- AI-generated image description
- Email notifications after upload/processing completion (Azure Logic Apps)
- Personal image library management:
  - List uploaded images
  - View image details/content
  - Delete images
  - Upload additional images anytime

## # Architecture (summary)

| Area               | Choice                                                                                                                                           |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| UI                 | React (Vite) SPA                                                                                                                                 |
| API & workers      | .NET 10, Clean Architecture                                                                                                                      |
| Containers         | Docker images for API, workers, and frontend (build/push details in [`devops/README.md`](devops/README.md))                                      |
| Local stack        | **Docker Compose** — `docker-compose.yml`, `docker-compose.backend.yml`, `docker-compose-infra.yml` (see [`devops/README.md`](devops/README.md)) |
| Production compute | **Azure Kubernetes Service (AKS)** — workloads deployed with **Helm** (`devops/helm/cloud-native-image-processing`)                              |
| Data               | PostgreSQL (EF Core), **Azure Managed Redis**, Azure Blob Storage                                                                                |
| Messaging          | Azure Event Hubs (`image-processing`, `ai-description`; local emulator in Compose)                                                               |
| Production edge    | Azure Front Door, WAF/DDoS (typical reference design)                                                                                            |
| Notifications      | Azure Logic Apps (email after upload/processing)                                                                                                 |
| Observability      | Azure Monitor (Log Analytics, AKS diagnostics + Container Insights, optional Application Insights for app telemetry)                             |
| AI                 | Azure Computer Vision (image description)                                                                                                        |

## # Operations and deployment

**Use a single guide:** [`devops/README.md`](devops/README.md) — **local** Docker Compose steps and **production** Terraform → scripts → Helm (Key Vault only, no manual cluster secrets).

For UI or backend development details only, see the component READMEs linked in the table above.

## # Demo instructions: Local stack (optional)
1. Prerequisites: Docker and Docker Compose v2 (`docker compose`)
2. From the repository root:
  ```bash
  docker compose up -d --build
  ```
3. Access the application at [http://localhost:5173](http://localhost:5173)

## # Demo instructions: Production stack
### Step 1: Create a new Azure free trial account:
1. Go to Azure Portal [https://portal.azure.com/](https://portal.azure.com/)
2. Sign up for a new account
3. Start using Azure for free
4. Enter information: personal information, billing information, payment method, etc. (required new credit card to be eligible for free trial)

### Step 2: Create app registration for GitHub Actions:
1. In Azure Portal, search for `Microsoft Entra ID`
2. Go to `App registrations`
3. Click on add new button
4. Enter information: application name (e.g. `Github Actions`)
5. Proceed to create the app registration

### Step 3: Create federated credential for GitHub Actions:
1. Go to the app registration you created in step 2
2. Go to `Certificates & Secrets`
3. Switch tab to `Federated credentials`
4. Click on add new button
5. Select scenario `Github Actions deploying Azure resources`
6. Enter information:
   - Orgnanization: `<your github username>` (e.g. `trannamtrung1st`)
   - Repository: `cloud-native-image-processing`
   - Entity type: `Environment`
   - Github environment name: `Production`
   - Credential name: `github-actions`

7. Proceed to create the federated credential
  
### Step 4: Create a new subscription:
1. In Azure Portal, click on `Subscriptions`
2. Click on add new button
3. Enter information: subscription name, plan type, etc.
4. Proceed to create the subscription

### Step 5: Assign Github Actions subscription owner permissions:
1. Go to the newly created subscription
2. Go to `Access control (IAM)`
3. Click on add new `Role assignment` button
4. Switch tab to `Privileged administrator roles`, choose `Owner` role
5. Click `Next` to `Members` step
6. Choose `User, group, or service principal`
7. Click `Select members`, search for `Github Actions`
8. Click `Next` to `Conditions` step
9. Choose `Allow user to assign all roles except privileged administrator roles Owner, UAA, RBAC (Recommended)`
10. Click `Next` to `Review + assign` step
11. Proceed to assign the role
  
### Step 6: Assign Github Actions Storage Blob Data Owner permissions:
Same as step 5, but choose `Storage Blob Data Owner` role and no need conditions
  
### Step 7: Register Storage Resource Provider permissions:
1. Go to the newly created subscription
2. Go to `Settings` -> `Resource providers`
3. Search for `Microsoft.Storage`
4. Click on `Register` button
  
### Step 8: Fork this repository to your GitHub account:
1. Go to [this repository](https://github.com/trannamtrung1st/cloud-native-image-processing)
2. Click on `Fork` button
3. Choose your GitHub account (keep the repository name as is)
4. Proceed to fork the repository

### Step 9: Enable GitHub Actions:
1. Go to the forked repository
2. Go to `Actions`
3. Click on `I understand my workflows, go ahead and enable them` button

### Step 10: Setup Actions environment:
1. Got to `Settings` -> `Environments`
2. Click on `New environment` button
3. Enter environment name: `Production`
4. Proceed to create the environment

### Step 11: Setup Actions environment variables:
1. In the environment `Production` page, find `Environment variables` section
2. Click on add new button
3. Add new variables:
   | Variable Name              | Variable Value |
   | -------------------------- | -------------- |
   | TERRAFORM_USE_REMOTE_STATE | true           |
   | USE_TERRAFORM_OUTPUTS      | true           |
   
### Step 12: Setup Actions environment secrets:
1. In the environment `Production` page, find `Environment secrets` section
2. Click on add new button
3. Add new secrets:
   | Secret Name                  | Secret Value                                                                                                             |
   | ---------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
   | AZURE_CLIENT_ID              | `Microsoft Entra ID` -> `App Registrations` -> `All applications` -> click on your app -> copy `Application (client) ID` |
   | AZURE_TENANT_ID              | `Microsoft Entra ID` -> copy `Tenant ID`                                                                                 |
   | KEYVAULT_ADMIN_PRINCIPAL_IDS | `Microsoft Entra ID` -> `Users` -> your user -> copy `Object ID`                                                         |
   | AZURE_SUBSCRIPTION_ID        | `Subscriptions` -> go to your newly created subscription -> copy `Subscription ID`                                       |
   | TERRAFORM_TFVARS             | This repository -> Copy `devops/terraform/terraform.tfvars.example` file content                                         |
   | TF_STATE_CONTAINER           | `cnip-terraform`                                                                                                         |
   | TF_STATE_KEY                 | `production/tf.state`                                                                                                    |
   | TF_STATE_RESOURCE_GROUP      | `cnip-terraform`                                                                                                         |
   | TF_STATE_STORAGE_ACCOUNT     | `<random-storage-account-name>`, from 3-24 lowercase letters and numbers (e.g. `cnipsatrungtran`)                        |
   
### Step 13: Run Terraform workflow to provision Azure resources:
1. Go to `Actions`
2. Select `Terraform (manual)` workflow
3. Choose `Run workflow` to open modal
4. For `Run terraform plan only, or plan+apply`, choose `apply`
5. Enter `apply` confirmation
6. (One time only) Click on `Bootstrap tfstate backend (resource group/storage account/container) before init`
7. Click on `Run workflow` button
8. Wait for the workflow to finish


### Step 14: Run Deploy to Azure workflow to deploy the application:
1. Go to `Actions`
2. Select `Deploy to Azure` workflow
3. Choose `Run workflow` to open modal
4. Enter `<version-number>` (e.g. `1.0.0`)
5. Click on `Run workflow` button
6. Wait for the workflow to finish

### Step 15: Access the application:
1. In Azure Portal, search `Public IP addresses`
2. Click on IP `kubernetes-...`
3. Copy the DNS label and paste it into your browser