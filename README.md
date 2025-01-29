# Europa - End-to-End Encrypted File Sharing

<div align="center">

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-brightgreen.svg)](https://docs.microsoft.com/en-us/aspnet/core/)
[![Docker](https://img.shields.io/badge/docker-%230db7ed.svg?style=for-the-badge&logo=docker&logoColor=white)](https://www.docker.com/)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](https://github.com/Migairu/europa)

</div>

Europa is a high-security, end-to-end encrypted file-sharing platform built with ASP.NET Core 8. It provides advanced encryption for file transfers while maintaining simplicity and ease of use.

![Europa Screenshot](https://assets.migairu.com/images/714e83a9-cc8f-4199-b747-0678aaf54164.avif)

## ✨ Features

- **End-to-End Encryption**: AES-256-GCM encryption with PBKDF2 key derivation
- **Zero Knowledge**: Files are encrypted client-side; the server never sees the unencrypted content
- **No Registration Required**: Share files instantly without creating an account
- **Large File Support**: Upload files up to 2GB with chunked upload support
- **Multi-File Support**: Upload multiple files as an encrypted ZIP archive
- **Automatic Cleanup**: Files are automatically deleted after expiration
- **Rate Limiting**: Built-in protection against abuse
- **Mobile Friendly**: Responsive design works on all devices

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 SDK
- SQL Server (or SQL Server Express)
- Azure Storage Account
- Visual Studio 2022 or VS Code

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/Migairu/europa.git
   ```

2. Configure your connection strings in `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "your_sql_connection_string"
     },
     "AzureStorageConfig": {
       "ConnectionString": "your_azure_storage_connection_string"
     }
   }
   ```

3. Run database migrations:
   ```bash
   dotnet ef database update
   ```

4. Start the application:
   ```bash
   dotnet run
   ```

## 🐳 Docker Deployment

### Prerequisites for Docker
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed on your machine
- WSL 2 (Windows Subsystem for Linux) if using Windows
- At least 4GB of RAM available
- Virtualization enabled in BIOS

### Step-by-Step Docker Setup

1. **Install Docker Desktop**:
   - Download Docker Desktop from [https://www.docker.com/products/docker-desktop/](https://www.docker.com/products/docker-desktop/)
   - Run the installer
   - Restart your computer after installation
   - Verify installation by running:
     ```bash
     docker --version
     ```

2. **Configure SSL Certificate** (required for HTTPS):
   ```powershell
   # Create directory for certificates
   mkdir $env:USERPROFILE/.aspnet/https -Force

   # Generate development certificate
   dotnet dev-certs https -ep $env:USERPROFILE/.aspnet/https/aspnetapp.pfx -p Your_Password123

   # Trust the certificate
   dotnet dev-certs https --trust
   ```

3. **Build the Docker Image**:
   ```bash
   # Navigate to project directory
   cd europa

   # Build the image
   docker build -t europa .
   ```

4. **Run the Container**:
   ```powershell
   docker run -p 8080:80 -p 8443:443 `
       -e ASPNETCORE_URLS="http://+:80;https://+:443" `
       -e ASPNETCORE_HTTPS_PORT=8443 `
       -e ASPNETCORE_Kestrel__Certificates__Default__Password="Your_Password123" `
       -e ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx `
       -e ConnectionStrings__DefaultConnection="your_sql_connection_string" `
       -e AzureStorageConfig__ConnectionString="your_azure_storage_connection_string" `
       -v $env:USERPROFILE/.aspnet/https:/https/ `
       europa
   ```

5. **Access the Application**:
   - HTTP: [http://localhost:8080](http://localhost:8080)
   - HTTPS: [https://localhost:8443](https://localhost:8443)

### Using Docker Compose (Alternative Method)

1. **Create docker-compose.yml**:
   ```yaml
   version: '3.8'

   services:
     webapp:
       build:
         context: .
         dockerfile: Dockerfile
       ports:
         - "8080:80"
         - "8443:443"
       environment:
         - ASPNETCORE_ENVIRONMENT=Production
         - ASPNETCORE_URLS=https://+:443;http://+:80
         - ASPNETCORE_HTTPS_PORT=8443
         - ASPNETCORE_Kestrel__Certificates__Default__Password=Your_Password123
         - ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx
         - ConnectionStrings__DefaultConnection=your_sql_connection_string
         - AzureStorageConfig__ConnectionString=your_azure_storage_connection_string
       volumes:
         - ${USERPROFILE}/.aspnet/https:/https/

   volumes:
     sqldata:
   ```

2. **Run with Docker Compose**:
   ```bash
   docker-compose up --build
   ```

## 🔒 Security Features

- PBKDF2 with 100,000 iterations for key derivation
- AES-256-GCM for file encryption
- Secure random IV and salt generation
- Anti-forgery token protection
- Rate limiting and DOS protection
- HTTPS enforcement
- Strict CSP headers
- XSS protection headers
- Auto-expiring file links

## 🛠️ Built With

- ASP.NET Core 8.0 MVC
- Entity Framework Core
- Azure Blob Storage
- Hangfire (Background Jobs)
- JavaScript Crypto APIs
- Bootstrap 5
- SQL Server

## 📝 API Documentation

Europa provides a simple API for file operations:

- `POST /api/upload/init` - Initialize chunked upload
- `POST /api/upload/chunk` - Upload file chunk
- `POST /api/upload/finalize` - Complete upload
- `GET /d/{id}` - Download file page
- `GET /download-file/{fileId}` - Download encrypted file

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📜 License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.

## 🌟 Acknowledgments

- [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/)
- [Azure Blob Storage](https://azure.microsoft.com/services/storage/blobs/)
- [Web Crypto API](https://developer.mozilla.org/docs/Web/API/Web_Crypto_API)
- [Hangfire](https://www.hangfire.io/)

## 📧 Contact

Juan Miguel Giraldo - [@jgiraldo29](https://x.com/jgiraldo29)
 
Project Link: [https://github.com/Migairu/europa](https://github.com/Migairu/europa)

---

<div align="center">

Made with ❤️ by [Migairu Corp.](https://www.migairu.com)

</div>
