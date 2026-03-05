# ReportConversion

**SAP BusinessObjects → Power BI Migration Platform**

A .NET 8 Web API that automates the end-to-end migration of SAP BusinessObjects reports (Crystal Reports & Web Intelligence) to Microsoft Power BI, supporting up to 10,000 reports with AI-assisted analysis and classification via Azure OpenAI (GPT-4o).

---

## Solution Structure

```
ReportConversion/
├── ReportConversion.API/              — ASP.NET Core Web API (controllers, middleware, JWT auth)
├── ReportConversion.Application/      — Business logic: services, interfaces, settings, models
├── ReportConversion.Domain/           — Domain entities and enums
├── ReportConversion.Infrastructure/   — Dapper repositories, SAP BO / Power BI / Azure clients
├── ReportConversion.Database/         — SQL Server 2022 table schemas and stored procedures
└── ReportConversion.ConsoleRunner/    — Menu-driven CLI companion app
```

---

## Technology Stack

| Concern | Technology |
|---|---|
| Framework | ASP.NET Core Web API (.NET 8) |
| Database | SQL Server 2022 |
| ORM | Dapper |
| AI Service | Azure OpenAI — GPT-4o (grouping) + text-embedding-3-large (duplicates) |
| Cloud Storage | Azure Blob Storage |
| Background Jobs | Hangfire with SQL Server job store |
| Logging | Serilog (Console + SQL Server sink) |
| Auth | JWT Bearer Token |
| HTTP Resilience | Polly (retry + exponential back-off) |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server 2022 (or SQL Server 2019+)
- Azure subscription with:
  - Azure OpenAI resource (GPT-4o and text-embedding-3-large deployments)
  - Azure Blob Storage account
- SAP BusinessObjects BI Platform 4.x (accessible REST API)
- Power BI Premium / Embedded workspace (optional — needed for live upload)

---

## Setup Instructions

### 1. Database

Execute the SQL scripts in order against your SQL Server instance:

```sql
-- 1. Create all tables, constraints, and indexes
sqlcmd -S localhost -d ReportConversionDB -i ReportConversion.Database/Tables/001_CreateTables.sql

-- 2. Seed KPI groups
sqlcmd -S localhost -d ReportConversionDB -i ReportConversion.Database/Tables/002_SeedKpiGroups.sql

-- 3. Create stored procedures
sqlcmd -S localhost -d ReportConversionDB -i ReportConversion.Database/StoredProcedures/sp_UpsertReport.sql
sqlcmd -S localhost -d ReportConversionDB -i ReportConversion.Database/StoredProcedures/sp_GetReportsByUsageStatus.sql
sqlcmd -S localhost -d ReportConversionDB -i ReportConversion.Database/StoredProcedures/sp_GetReportsByKpiGroup.sql
sqlcmd -S localhost -d ReportConversionDB -i ReportConversion.Database/StoredProcedures/sp_GetDuplicatePairs.sql
sqlcmd -S localhost -d ReportConversionDB -i ReportConversion.Database/StoredProcedures/sp_UpdateMigrationStatus.sql
sqlcmd -S localhost -d ReportConversionDB -i ReportConversion.Database/StoredProcedures/sp_GetMigrationSummary.sql
```

### 2. Configuration

Edit `ReportConversion.API/appsettings.json` (and `ReportConversion.ConsoleRunner/appsettings.json`) with your environment values:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=ReportConversionDB;..."
  },
  "Jwt": {
    "Key": "YOUR_MIN_32_CHAR_SECRET_KEY"
  },
  "SapBo": {
    "BaseUrl": "http://your-sap-bo-server:8080",
    "Username": "your-username",
    "Password": "your-password"
  },
  "AzureOpenAi": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key",
    "ChatDeploymentName": "gpt-4o",
    "EmbeddingDeploymentName": "text-embedding-3-large"
  },
  "AzureBlob": {
    "ConnectionString": "DefaultEndpointsProtocol=https;...",
    "ContainerName": "powerbi-reports"
  },
  "PowerBi": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-app-client-id",
    "ClientSecret": "your-client-secret",
    "WorkspaceId": "your-pbi-workspace-id",
    "OutputDirectory": "output/pbix"
  }
}
```

Key configurable thresholds:

| Setting | Default | Description |
|---|---|---|
| `StaleReport:StaleDaysThreshold` | `180` | Days of inactivity before a report is marked Stale |
| `DuplicateDetection:SimilarityThreshold` | `0.92` | Cosine similarity score to flag duplicates |
| `Migration:OutputDirectory` | `output/pbix` | Local directory for generated .pbix files |

### 3. Run the API

```bash
cd ReportConversion.API
dotnet run
```

The API starts at `https://localhost:7000` (or the configured port).

Swagger UI is available at: `https://localhost:7000/swagger`

Hangfire Dashboard: `https://localhost:7000/hangfire`

### 4. Run the Console App

```bash
cd ReportConversion.ConsoleRunner
dotnet run
```

---

## API Usage

### Authentication

All endpoints (except `/api/auth/token`) require a JWT Bearer token.

```http
POST /api/auth/token
Content-Type: application/json

{
  "username": "admin",
  "password": "admin"
}
```

Use the returned token as: `Authorization: Bearer <token>`

---

### Metadata Endpoints

#### Trigger metadata extraction (async background job)
```http
POST /api/metadata/extract
Authorization: Bearer <token>
```
Returns: `{ "data": "hangfire-job-id" }`

#### List all reports (paginated)
```http
GET /api/metadata/reports?page=1&pageSize=50&filter=sales
Authorization: Bearer <token>
```

#### Get single report
```http
GET /api/metadata/reports/42
Authorization: Bearer <token>
```

#### List stale/unused reports
```http
GET /api/metadata/stale
Authorization: Bearer <token>
```

---

### Analysis Endpoints

#### Trigger KPI grouping via Azure OpenAI
```http
POST /api/analysis/group-by-kpi
Authorization: Bearer <token>
```

#### List KPI groups with report counts
```http
GET /api/analysis/kpi-groups
Authorization: Bearer <token>
```

#### List KPI registry
```http
GET /api/analysis/kpi-registry
Authorization: Bearer <token>
```

#### Trigger duplicate detection (embeddings)
```http
POST /api/analysis/find-duplicates
Authorization: Bearer <token>
```

#### List duplicate pairs
```http
GET /api/analysis/duplicates?minSimilarity=0.92
Authorization: Bearer <token>
```

---

### Migration Endpoints

#### Start migration (all reports)
```http
POST /api/migration/start
Authorization: Bearer <token>
Content-Type: application/json

{}
```

#### Start migration (specific report IDs)
```http
POST /api/migration/start
Authorization: Bearer <token>
Content-Type: application/json

{
  "reportIds": [1, 5, 12, 44]
}
```

#### Get overall migration status
```http
GET /api/migration/status
Authorization: Bearer <token>
```

#### Get specific job status
```http
GET /api/migration/status/7
Authorization: Bearer <token>
```

#### Get migration logs for a report
```http
GET /api/migration/logs/42
Authorization: Bearer <token>
```

---

## Standard API Response Format

All endpoints return a consistent wrapper:

```json
{
  "success": true,
  "message": "Reports retrieved successfully",
  "data": { ... },
  "error": null
}
```

Paginated endpoints return:

```json
{
  "success": true,
  "message": "Reports retrieved successfully",
  "data": [ ... ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 4200,
  "totalPages": 84
}
```

---

## Architecture

The solution follows Clean Architecture with strict layer separation:

```
Controllers  →  Services  →  Repositories  →  Database
     ↓               ↓
  Middleware      Clients (SAP BO, Azure OpenAI, Power BI, Blob)
```

- **Domain** — Entities and enums only. No dependencies.
- **Application** — Interfaces + service business logic. Depends on Domain.
- **Infrastructure** — Dapper repositories and external clients. Depends on Application.
- **API** — Controllers, middleware, and DI bootstrapping. Depends on all layers.

---

## Background Jobs (Hangfire)

Long-running operations are queued as Hangfire background jobs:

| Operation | Endpoint | Estimated Duration (10k reports) |
|---|---|---|
| Metadata Extraction | `POST /api/metadata/extract` | 2–4 hours |
| KPI Grouping | `POST /api/analysis/group-by-kpi` | 30–60 minutes |
| Duplicate Detection | `POST /api/analysis/find-duplicates` | 1–2 hours |
| Full Migration | `POST /api/migration/start` | 4–8 hours |

Monitor job progress via the Hangfire Dashboard at `/hangfire`.

---

## KPI Groups

Reports are classified into one of 8 KPI domains by GPT-4o:

| KPI Group | Description |
|---|---|
| Financial Performance | P&L, budgets, forecasts, cost centre analysis |
| Sales & Revenue | Pipeline, revenue tracking, growth metrics |
| Operations & Logistics | Fulfilment, SLAs, logistics performance |
| HR & Workforce | Headcount, payroll, performance reviews |
| Customer Analytics | Retention, satisfaction, NPS, segmentation |
| Inventory & Supply Chain | Stock, procurement, supplier scorecards |
| Compliance & Audit | Regulatory reporting, audit trails, risk |
| Other / Unclassified | Reports not matching any primary domain |

---

## Power BI Element Mapping

| SAP Report Element | Power BI Equivalent |
|---|---|
| Table / Vertical Table | Table Visual |
| Chart (Bar/Line/Pie) | Bar / Line / Pie Chart Visual |
| Cross-tab | Matrix Visual |
| Graph / Scatter | Scatter / Line Chart Visual |
| Image | Image Visual |

Generated `.pbix` template files are saved to the configured `OutputDirectory` and uploaded to Azure Blob Storage at:
```
reports/{kpi-group}/{report-id}.pbix
```

---

## Logging

Structured logs are written to:
- **Console** — all environments
- **SQL Server** (`AppLogs` table) — auto-created on first run

Log level is configurable via `Serilog:MinimumLevel` in `appsettings.json`.
