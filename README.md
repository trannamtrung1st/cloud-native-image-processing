# cloud-native-image-processing
A Cloud-native Image Processing Application

## Introduction
This application lets users securely sign in with Azure AD B2C, including social sign-in with Google, and manage a personal image library in the cloud.

After authentication, users can upload images and optionally select an image processing operation before saving. The current supported processing option is grayscale conversion.

## Features
- User registration and login with Azure AD B2C
- Social login support through Google (via Azure AD B2C)
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

## Architecture
- **Components**: UI, Web API, Image Processing Worker, AI Description Generation Worker
- **Frontend/UI**: React SPA
- **Backend**: .NET 10
- **API**: ASP.NET Core Web API
- **Code structure**: Clean Architecture
- **Database/Data access**: PostgreSQL, EF Core
- **Queue**: Event Grid + Queue Storage
- **Cache**: Redis
- **Image storage**: Azure Blob Storage
- **API gateway**: Azure API Management
- **CDN**: Azure Front Door
- **Security**: Azure WAF + Azure DDoS Protection
- **Monitoring**: Azure Monitor
- **AI**: Azure Computer Vision
- **Notification workflow**: Azure Logic Apps for email notifications after upload/processing completion
- **Local development**: Docker, Docker Compose
- **Deployment**: Helm + Kubernetes with cloud-native Azure services
- **Infrastructure as Code (IaC)**: Terraform to provision and automate Azure services deployment