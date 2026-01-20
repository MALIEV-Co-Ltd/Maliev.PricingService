# Maliev Pricing Service

[![Build Status](https://img.shields.io/badge/Build-Passing-success)](https://github.com/MALIEV-Co-Ltd/Maliev.PricingService)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Database](https://img.shields.io/badge/Database-PostgreSQL%2018-blue)](https://www.postgresql.org/)

Automated pricing calculation engine for the Maliev manufacturing ecosystem.

**Role in MALIEV Architecture**: The central authority for product and service pricing. It provides instant quotation capabilities by consuming geometry analysis data from GeometryService and applying rule-based and ML algorithms, while maintaining a complete audit trail for compliance and historical analysis.

---

## 🏗️ Architecture & Tech Stack

- **Framework**: ASP.NET Core 10.0 (C# 14)
- **Database**: PostgreSQL 18 with Entity Framework Core 10.x
- **Messaging**: RabbitMQ via MassTransit
- **Pricing Engine**: Deterministic rules-based and ML-enhanced (Microsoft.ML) calculation engines
- **API Documentation**: OpenAPI 3.1 + Scalar UI
- **Observability**: OpenTelemetry (Metrics, Traces, Logging)

---

## ⚖️ Constitution Rules

This service strictly adheres to the platform development mandates:

### Banned Libraries
To maintain high performance and low complexity, the following are **NOT** used:
- ❌ **AutoMapper**: Explicit manual mapping only.
- ❌ **FluentValidation**: Standard Data Annotations (`[Required]`, `[Range]`) only.
- ❌ **FluentAssertions**: Standard xUnit `Assert` methods only.
- ❌ **In-memory Test DB**: All integration tests use **Testcontainers** with real PostgreSQL 18.

### Mandatory Practices
- ✅ **TreatWarningsAsErrors**: Enabled in all `.csproj` files.
- ✅ **XML Documentation**: Required on all public methods and properties.
- ✅ **No Secrets in Code**: All sensitive configuration injected via environment variables.
- ✅ **No Test Config in Program.cs**: Test configuration in test fixtures only.
- ✅ **IAM Integration**: Self-registers permissions with the IAM Service using GCP-style naming: `{service}.{resource}.{action}`.

---

## ✨ Key Features

- **Instant Quotation Engine**: Real-time price calculation based on 3D geometry metrics and material costs.
- **Full Audit Trail**: Every calculation is logged with full input context and 7-year retention for regulatory compliance.
- **Event-Driven Integration**: Consumes geometry analysis results and publishes calculated prices for downstream services.
- **Temporal Pricing**: Support for versioned pricing configurations with effective date ranges.
- **ML Integration**: Reserved capacity for ML-enhanced price optimization and success prediction.

---

## 🚀 Quick Start

### Prerequisites
- .NET 10.0 SDK
- Docker Desktop (for infrastructure)
- PostgreSQL 18 (Alpine)

### Local Development Setup

1. **Clone the repository**
```bash
git clone https://github.com/MALIEV-Co-Ltd/Maliev.PricingService.git
cd Maliev.PricingService
```

2. **Spin up Infrastructure**
```bash
docker run --name pricing-db -e POSTGRES_PASSWORD=YOUR_PASSWORD -p 5432:5432 -d postgres:18-alpine
docker run --name pricing-rabbitmq -p 5672:5672 -p 15672:15672 -d rabbitmq:3-management-alpine
```

3. **Configure Environment**
```powershell
# Windows PowerShell
$env:ConnectionStrings__PricingDbContext="YOUR_POSTGRES_CONNECTION_STRING"
$env:ConnectionStrings__Messaging="amqp://guest:guest@localhost:5672"
```

4. **Apply Migrations & Run**
```bash
dotnet ef database update --project Maliev.PricingService.Data
dotnet run --project Maliev.PricingService.Api
```

The service will be available at `http://localhost:5000/pricing`. Access the interactive documentation at `http://localhost:5000/pricing/scalar`.

---

## 📡 API Endpoints

All endpoints are prefixed with `/pricing/v1/`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/calculate` | Calculate prices for ad-hoc requests |
| GET | `/configurations` | Manage pricing rule configurations |
| GET | `/audit` | Access pricing calculation audit trail |

---

## 🏥 Health & Monitoring

Standardized health probes for Kubernetes orchestration:
- **Liveness**: `GET /pricing/liveness`
- **Readiness**: `GET /pricing/readiness` (Checks DB and RabbitMQ connectivity)
- **Metrics**: `GET /pricing/metrics` (Prometheus format)

---

## 🧪 Testing

We prioritize reliable tests over mock-heavy unit tests.

```bash
# Run all tests using Testcontainers
dotnet test --verbosity normal
```

- **Integration Tests**: Use real PostgreSQL 18 containers.
- **Contract Tests**: Ensure API stability for consumers.

---

## 📦 Deployment

Infrastructure management is handled via GitOps patterns.

- **Docker Image**: `REGION-docker.pkg.dev/PROJECT_ID/REPOSITORY/maliev-pricing-service:{sha}`
- **Environments**: Development, Staging, Production

---

## 📄 License

Proprietary - © 2026 MALIEV Co., Ltd. All rights reserved.
