# cloud-native-image-processing

Cloud-native image library: users sign in with **ASP.NET Core Identity**, upload images, optionally apply processing (e.g. grayscale), and get **AI-generated descriptions** (Azure Computer Vision). Images and metadata live in **Azure Blob Storage** and **PostgreSQL**; processing is driven by **Event Hubs** workers.

## Features

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

## Architecture (summary)

| Area               | Choice                                                                                                                                           |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| UI                 | React (Vite) SPA — [`frontend/README.md`](frontend/README.md)                                                                                    |
| API & workers      | .NET 10, Clean Architecture — [`backend/src/README.md`](backend/src/README.md)                                                                   |
| Containers         | Docker images for API, workers, and frontend (build/push details in [`devops/README.md`](devops/README.md))                                      |
| Local stack        | **Docker Compose** — `docker-compose.yml`, `docker-compose.backend.yml`, `docker-compose-infra.yml` (see [`devops/README.md`](devops/README.md)) |
| Production compute | **Azure Kubernetes Service (AKS)** — workloads deployed with **Helm** (`devops/helm/cloud-native-image-processing`)                              |
| Data               | PostgreSQL (EF Core), **Azure Managed Redis**, Azure Blob Storage                                                                                |
| Messaging          | Azure Event Hubs (`image-processing`, `ai-description`; local emulator in Compose)                                                               |
| Production edge    | Azure API Management, Front Door, WAF/DDoS (typical reference design)                                                                            |
| Notifications      | Azure Logic Apps (email after upload/processing)                                                                                                 |
| Observability      | Azure Monitor                                                                                                                                    |
| AI                 | Azure Computer Vision (image description)                                                                                                        |

## Operations and deployment

**Use a single guide:** [`devops/README.md`](devops/README.md). It covers Terraform for Azure, Helm for Kubernetes, Docker Compose (shell exports from Terraform for ACR and public URL, plus image tag), Key Vault + workload identity + CSI, and post-apply steps—including the [end-to-end secrets flow](devops/README.md#end-to-end-secrets-flow-terraform-key-vault-workload-identity-csi-helm-environment-variables) diagram.

For UI or backend development details only, see the component READMEs linked in the table above.
