# SAP BusinessObjects → Power BI Migration — Implementation Summary

> **Purpose:** This document explains the full implementation of the SAP BO to Power BI migration
> system — what each feature does, how the logic works, and which files are involved.

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Project Structure](#2-project-structure)
3. [Pipeline Flow](#3-pipeline-flow)
4. [Feature: Extract SAP BO Metadata](#4-feature-extract-sap-bo-metadata)
5. [Feature: Identify Stale / Unused Reports](#5-feature-identify-stale--unused-reports)
6. [Feature: Group Reports by KPI](#6-feature-group-reports-by-kpi)
7. [Feature: Find Duplicate Reports](#7-feature-find-duplicate-reports)
8. [Feature: Migrate Reports to Power BI](#8-feature-migrate-reports-to-power-bi)
9. [Supporting Infrastructure](#9-supporting-infrastructure)
10. [Database Schema Summary](#10-database-schema-summary)
11. [Configuration Reference](#11-configuration-reference)

---

## 1. System Overview

This system migrates enterprise reports from **SAP BusinessObjects (SAP BO)** to **Microsoft Power BI**.
It is not a simple export tool — before any migration happens, it analyses the existing report estate to:

- Classify which reports are actively used, stale, or never used
- Group reports that measure the same KPI (based on their SQL logic)
- Detect duplicate reports so redundant copies are not migrated
- Generate Power BI-compatible report templates and upload them to Azure Blob Storage

The system is exposed as both a **Console Runner** (step-by-step interactive menu) and a
**REST API** (with Hangfire background jobs for automation).

**Technologies used:**

| Layer | Technology |
|---|---|
| Runtime | .NET 8.0 / C# |
| Database | SQL Server 2022 (via Dapper) |
| AI / LLM | Azure OpenAI — GPT-4o (grouping, classification) + text-embedding-3-large (similarity) |
| Cloud Storage | Azure Blob Storage |
| Power BI Auth | Microsoft Identity (MSAL) — client credentials flow |
| Background Jobs | Hangfire (API project) |
| HTTP Resilience | Polly (exponential back-off retry) |
| Logging | Serilog (console + SQL Server sink) |

---

## 2. Project Structure

```
ReportConversion/
├── ReportConversion.ConsoleRunner/      # Interactive CLI — runs the full pipeline
├── ReportConversion.API/                # REST API with Hangfire background jobs
├── ReportConversion.Application/        # Business logic (services, interfaces, models, settings)
├── ReportConversion.Domain/             # Entities and enums (no dependencies)
├── ReportConversion.Infrastructure/     # External integrations (SAP BO, Azure, Repositories)
└── ReportConversion.Database/
    ├── Tables/                          # CREATE TABLE scripts (001–005)
    ├── StoredProcedures/                # Reporting stored procedures
    └── Queries/                         # Human-readable analysis queries
```

### Key Services (Application Layer)

| Service | Responsibility |
|---|---|
| `MetadataExtractionService` | Pulls all report metadata from SAP BO API |
| `StaleReportService` | Classifies each report as Active / Stale / NeverUsed |
| `KpiGroupingService` | Groups reports by KPI using GPT-4o SQL analysis |
| `DuplicateDetectionService` | Finds duplicate pairs using 3-tier SQL-first strategy |
| `MigrationService` | Converts reports to Power BI templates and uploads them |
| `KpiRegistryService` | Builds a searchable KPI catalogue from discovered groups |

---

## 3. Pipeline Flow

The six steps run in sequence. Each step enriches the database so the next step has better data.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 1 — Extract SAP BO Metadata                                            │
│   SAP BO REST API → Reports, Elements, SQL Queries, Data Sources → DB       │
└──────────────────────────────────┬──────────────────────────────────────────┘
                                   │
┌──────────────────────────────────▼──────────────────────────────────────────┐
│ STEP 2 — Identify Stale / Unused Reports                                    │
│   Read LastRunDate, IsScheduled, HasActiveSubscriptions from DB              │
│   → Stamp each report: Active / Stale / NeverUsed                           │
└──────────────────────────────────┬──────────────────────────────────────────┘
                                   │
┌──────────────────────────────────▼──────────────────────────────────────────┐
│ STEP 3 — Group Reports by KPI                                               │
│   Extract SQL signals (tables, columns, aggregations, joins, filters)        │
│   → Send to GPT-4o in batches → Discover KPI groups → Persist in DB         │
└──────────────────────────────────┬──────────────────────────────────────────┘
                                   │
┌──────────────────────────────────▼──────────────────────────────────────────┐
│ STEP 4 — Find Duplicate Reports                                             │
│   Tier 1: SQL fingerprint Jaccard → Tier 2: SQL embeddings                  │
│   → Tier 3: Name/Description embeddings (fallback)                          │
└──────────────────────────────────┬──────────────────────────────────────────┘
                                   │
┌──────────────────────────────────▼──────────────────────────────────────────┐
│ STEP 5 — Migrate Reports to Power BI                                        │
│   Generate PBIX template → Upload to Azure Blob → Track job status          │
└──────────────────────────────────┬──────────────────────────────────────────┘
                                   │
┌──────────────────────────────────▼──────────────────────────────────────────┐
│ STEP 6 — View Migration Summary                                             │
│   Total / Success / Failed / Pending / Success Rate                         │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Feature: Extract SAP BO Metadata

### What it does
Connects to the SAP BusinessObjects REST API, paginates through every report, and pulls all
metadata into the local SQL Server database. This is the foundation — every subsequent step
reads from this local copy.

### Logic (step by step)

```
1. Authenticate → POST /biprws/logon/long  →  receive logon token
2. Paginate reports → GET /biprws/raylight/v1/documents?offset=N&limit=100
3. For each report:
   a. Get full detail  → GET /documents/{id}
   b. Get scheduling   → GET /documents/{id}/scheduling   (enriches IsScheduled)
   c. Get elements     → GET /documents/{id}/pages/1/elements
   d. Get SQL queries  → GET /documents/{id}/dataproviders
   e. Get data sources → GET /documents/{id}/dataproviders
4. Upsert everything into the database
```

### Key fields extracted from SAP BO

| DB Field | SAP BO API Field(s) | Notes |
|---|---|---|
| `LastRunDate` | `lastSuccessfulInstanceDate` → `lastRunDate` → `lastSuccessDate` → `si_success_date` | Tries 4 field names; handles ISO-8601 and legacy `/Date(ms)/` format |
| `IsScheduled` | `isScheduled`, `hasSchedule`, `scheduleStatus` (0/1/2), + `/scheduling` endpoint | Tries document fields first, then dedicated scheduling endpoint |
| `HasActiveSubscriptions` | `hasSubscriptions`, `subscriberCount`, `publicationCount` | Tries boolean fields then count fields |
| `SqlFingerprint` | Computed locally | MD5 hash of normalised (trimmed + uppercased) SQL text — used in duplicate detection |

### Files involved

| File | Role |
|---|---|
| `Application/Services/MetadataExtractionService.cs` | Orchestrates the full extraction loop |
| `Infrastructure/SapBoClient/SapBoHttpClient.cs` | All SAP BO HTTP calls; `MapToReport()`, `TryParseDate()`, `TryGetScheduledFlag()` |
| `Application/Interfaces/ISapBoClient.cs` | Interface contract |
| `Infrastructure/Repositories/ReportRepository.cs` | `UpsertAsync()` — insert or update report |
| `Infrastructure/Repositories/ReportSqlRepository.cs` | Stores SQL queries with fingerprints |

---

## 5. Feature: Identify Stale / Unused Reports

### What it does
Reads each report's usage signals from the database and stamps it with one of three statuses:
**Active**, **Stale**, or **NeverUsed**. This helps the team decide which reports are worth
migrating and which should be retired.

### Classification Logic

The decision tree evaluates three conditions per report:

```
For each report:

  LastRunDate IS NULL
  AND IsScheduled = false
  AND HasActiveSubscriptions = false
        → NeverUsed  (report has never been run and has no automation)

  (LastRunDate IS NULL  OR  LastRunDate < (today − 180 days))
  AND IsScheduled = false
  AND HasActiveSubscriptions = false
        → Stale  (ran a long time ago or never, with no ongoing usage)

  Anything else (recently run, OR scheduled, OR has active subscribers)
        → Active
```

> **Important:** `NeverUsed` is checked before `Stale`. Any report with `LastRunDate = NULL`
> will always land in `NeverUsed` regardless of the 180-day threshold.

### Why all reports showed as "Never Used" (fixed)

The original code read `LastRunDate` from a single hard-coded SAP BO field name
(`lastSuccessfulInstanceDate`) which is absent in many SAP BO versions.
`IsScheduled` and `HasActiveSubscriptions` were never mapped at all.
All three conditions were always true → every report got `NeverUsed`.

**Fix applied in `SapBoHttpClient.cs`:**
- `LastRunDate` now tries 4 field name fallbacks + handles the legacy `/Date(ms)/` epoch format
- `IsScheduled` reads `isScheduled`, `hasSchedule`, `scheduleStatus` (numeric 0/1/2 and string)
  and also calls the `/scheduling` endpoint for a definitive answer
- `HasActiveSubscriptions` reads `hasSubscriptions`, `subscriberCount`, `publicationCount`

### Threshold configuration

```json
// appsettings.json
"StaleReport": {
  "StaleDaysThreshold": 180
}
```

### Files involved

| File | Role |
|---|---|
| `Application/Services/StaleReportService.cs` | Core classification logic |
| `Application/Settings/StaleReportSettings.cs` | `StaleDaysThreshold` (default 180 days) |
| `Infrastructure/SapBoClient/SapBoHttpClient.cs` | Data source fix — `MapToReport()` + helpers |
| `Infrastructure/Repositories/ReportRepository.cs` | `UpdateUsageStatusAsync()`, `GetByUsageStatusAsync()` |
| `Database/Tables/001_CreateTables.sql` | `UsageStatus` column + CHECK constraint + indexes |
| `Database/StoredProcedures/sp_GetReportsByUsageStatus.sql` | Query reports by status |

---

## 6. Feature: Group Reports by KPI

### What it does
Groups reports that measure the same business KPI by analysing their underlying SQL queries.
Instead of using predefined categories, the system lets GPT-4o discover group names
organically from the SQL logic — producing names like _"Monthly Revenue by Customer Segment"_
rather than _"Financial Performance"_.

### Logic (4 phases)

#### Phase 1 — SQL Signal Extraction (local, no LLM)
For each report's SQL queries, a structured summary is extracted:

```
Tables      → FROM / JOIN identifiers (schema-qualified, deduplicated)
Columns     → SELECT clause identifiers (excluding reserved words & aggregate functions)
Aggregations → SUM, COUNT, AVG, MAX, MIN, GROUP BY, HAVING, PARTITION BY
Joins       → INNER JOIN, LEFT JOIN, RIGHT JOIN, FULL OUTER JOIN, CROSS JOIN
Filters     → WHERE clause identifiers + BETWEEN, IN, LIKE, DATE_FILTER patterns
```

The summary is capped at 500 characters to keep LLM prompts within token budget.
It is also persisted in `ReportSqlAnalysis` for full auditability.

#### Phase 2 — LLM Group Discovery (GPT-4o)
SQL summaries are batched (100 reports per call) and sent to GPT-4o with this instruction:

> *"You are a senior data architect. Identify which reports measure the same KPI by examining
> their tables, columns, aggregations, joins, and filters. Name groups from the SQL content —
> not generic labels. Return JSON: `{ groups: [ { name, description, reason, reportIds } ] }`"*

The `reason` field asks GPT-4o to cite the **specific SQL evidence** (table names, aggregation
columns, join patterns) explaining why the reports belong together.

#### Phase 3 — Cross-batch Group Merging
Because large repositories are processed in batches, the same group name can appear in
multiple batch responses. Groups with identical names are merged (report ID lists combined)
before being saved.

#### Phase 4 — Persistence
For each discovered group:
- **`KpiGroups`** table: `Name`, `Description`, `Reason`, `CreatedAt`
- **`ReportKpiGroupMapping`** table: one row per `(ReportId, KpiGroupId)` pair
- If a group with the same name already exists (re-run), the `Reason` is refreshed
  but the group is not duplicated

### New table added: ReportKpiGroupMapping

The `KpiGroupId` column on `Reports` is a denormalised convenience field.
`ReportKpiGroupMapping` is the explicit mapping table — making it easy to query
which reports belong to which group without relying on the denormalised column.

```
ReportKpiGroupMapping
  Id          INT IDENTITY PK
  ReportId    INT FK → Reports
  KpiGroupId  INT FK → KpiGroups
  AssignedAt  DATETIME2
  UNIQUE (ReportId, KpiGroupId)
```

### New column added: KpiGroups.Reason

```sql
KpiGroups.Reason  NVARCHAR(MAX) NULL
-- Example value:
-- "Reports A, B and C are grouped together because all three reference
--  the SALES_ORDERS and CUSTOMERS tables, use SUM(AMOUNT) aggregated by
--  date period with GROUP BY MONTH, and filter on REGION = @region."
```

### Configuration

```json
// appsettings.json
"AzureOpenAi": {
  "ChatDeploymentName": "gpt-4o",
  "EmbeddingDeploymentName": "text-embedding-3-large"
}
```

### Files involved

| File | Role |
|---|---|
| `Application/Services/KpiGroupingService.cs` | All 4 phases; SQL extraction; batch loop; persistence |
| `Infrastructure/AzureOpenAIClient/AzureOpenAiHttpClient.cs` | `DiscoverKpiGroupsFromSqlAsync()` — LLM call + response parser |
| `Application/Models/SqlKpiGroup.cs` | DTO returned by LLM parser (`Name`, `Description`, `Reason`, `ReportIds`) |
| `Domain/Entities/KpiGroup.cs` | Entity with new `Reason` property |
| `Domain/Entities/ReportKpiGroupMapping.cs` | New mapping entity |
| `Application/Interfaces/IKpiGroupRepository.cs` | `InsertAsync()`, `UpdateReasonAsync()` |
| `Application/Interfaces/IReportKpiGroupMappingRepository.cs` | Full CRUD for mapping table |
| `Infrastructure/Repositories/KpiGroupRepository.cs` | SQL INSERT includes `Reason`; `UpdateReasonAsync()` |
| `Infrastructure/Repositories/ReportKpiGroupMappingRepository.cs` | Idempotent INSERT with `IF NOT EXISTS` guard |
| `Database/Tables/004_KpiGroupEnhancements.sql` | `ALTER TABLE KpiGroups ADD Reason`; `CREATE TABLE ReportKpiGroupMapping` |
| `Database/Queries/GroupReports.sql` | 4 human-readable queries for reviewing groups |
| `Database/StoredProcedures/sp_GetReportsByKpiGroup.sql` | Filterable stored procedure |

---

## 7. Feature: Find Duplicate Reports

### What it does
Identifies pairs of reports that are likely duplicates of each other and recommends
whether to **Archive** (safe to remove) or **Review** (needs manual check).

### The Problem with the Original Approach
The old service embedded only `Name + Description` for all reports. This meant:
- Two reports with **identical SQL but different names** → not detected
- Two reports with **similar-sounding names but different SQL** → falsely flagged

### New 3-Tier SQL-First Strategy

SQL is the ground truth. Metadata (name/description) is only used as a last resort.

```
Both reports have SQL?
  │
  ├── YES ──► TIER 1: SQL Fingerprint (Jaccard similarity on MD5 hash sets)
  │             Compare sets of SqlFingerprint values for each report pair
  │             Jaccard = |intersection| / |union|
  │
  │             Jaccard = 1.0       → Archive (exact SQL match, zero LLM cost)
  │             Jaccard ≥ 0.80      → Review  (partial SQL overlap)
  │             Pair recorded → skip in Tier 2
  │
  ├── YES (not caught by Tier 1) ──► TIER 2: SQL Semantic Embedding
  │             Concatenate all SQL text for each report
  │             Generate Azure OpenAI embedding (text-embedding-3-large)
  │             Cosine similarity ≥ 0.90 → Review
  │             (catches same logic written differently: aliases, ordering, etc.)
  │
  └── NO SQL ──► TIER 3: Metadata Embedding (fallback only)
                  Generate embedding from Name + Description
                  Cosine similarity ≥ 0.92 → Review
                  (never applied to reports that have SQL)
```

### Detection Method recorded on every pair

Every duplicate pair in `DuplicateReports` records which tier found it:

| `DetectionMethod` | Meaning |
|---|---|
| `SqlFingerprint` | Tier 1 — deterministic hash match |
| `SqlEmbedding` | Tier 2 — semantically similar SQL |
| `MetadataEmbedding` | Tier 3 — similar name / description |

### Threshold configuration

```json
// appsettings.json
"DuplicateDetection": {
  "SqlFingerprintJaccardThreshold": 0.80,
  "SqlEmbeddingThreshold":          0.90,
  "MetadataEmbeddingThreshold":     0.92
}
```

### Recommended Action logic

| Condition | Action |
|---|---|
| Tier 1, Jaccard = 1.0 (all SQL identical) | **Archive** — safe to remove one copy |
| Tier 1, Jaccard < 1.0 (partial SQL overlap) | **Review** — could be different date ranges / filters |
| Tier 2 (SQL embedding) | **Review** — semantically similar but not byte-for-byte identical |
| Tier 3 (metadata embedding) | **Review** — similar names, manual confirmation needed |

### Files involved

| File | Role |
|---|---|
| `Application/Services/DuplicateDetectionService.cs` | Full 3-tier orchestration |
| `Domain/Enums/DetectionMethod.cs` | New enum: `SqlFingerprint`, `SqlEmbedding`, `MetadataEmbedding` |
| `Domain/Entities/DuplicateReport.cs` | Entity — added `DetectionMethod` property |
| `Application/Settings/DuplicateDetectionSettings.cs` | Three per-tier thresholds |
| `Application/Interfaces/IReportSqlRepository.cs` | Added `GetAllAsync()` — bulk load in one DB call |
| `Infrastructure/Repositories/ReportSqlRepository.cs` | Implements `GetAllAsync()` |
| `Infrastructure/Repositories/DuplicateReportRepository.cs` | INSERT includes `DetectionMethod` |
| `Database/Tables/005_DuplicateReportEnhancements.sql` | `ALTER TABLE DuplicateReports ADD DetectionMethod` |
| `Database/Queries/DuplicateReports.sql` | 4 human-readable queries for reviewing duplicates |
| `Database/StoredProcedures/sp_GetDuplicatePairs.sql` | Filterable stored procedure |

---

## 8. Feature: Migrate Reports to Power BI

### What it does
Takes each report and its visual elements from the database, generates a Power BI-compatible
template file (PBIX), uploads it to Azure Blob Storage under the report's KPI group folder,
and tracks the job status with full logging.

### Logic (step by step)

```
1. Load report record + visual elements from DB
2. Create or resume a MigrationJob record (Status = InProgress)
3. Call PowerBiRestClient.CreateReportAsync():
   → Generates a PBIX template file in the output directory
   → File includes: report name, SAP BO type, KPI group, original ID,
     migration timestamp, and a visual element mapping table
     (e.g.  Table → Table Visual,  Chart → Bar/Line/Pie Chart)
4. Upload the .pbix file to Azure Blob Storage:
   → Path: reports/{kpi-group-slug}/{reportId}.pbix
5. Update MigrationJob: Status = Success, OutputPath, BlobPath
6. On any failure: Status = Failed + error message logged
```

### Visual element mapping (SAP BO → Power BI)

| SAP BO Element | Power BI Equivalent |
|---|---|
| `table`, `crosstab`, `vtable` | Table Visual |
| `chart`, `bar`, `line`, `pie` | Bar/Line/Pie Chart |
| `matrix`, `cross-tab` | Matrix Visual |
| `graph`, `scatter` | Scatter/Line Chart |
| `image` | Image |
| anything else | Custom Visual |

### Migration modes

The Console Runner offers two modes:
- **All** — migrates every report in the database sequentially
- **IDs** — migrates a comma-separated list of specific report IDs

### Job tracking

Every migration is tracked in `MigrationJobs` and `MigrationLogs`:

```
MigrationJobs:  ReportId, Status (Pending/InProgress/Success/Failed),
                StartedAt, CompletedAt, OutputPath, BlobPath, ErrorMessage

MigrationLogs:  MigrationJobId, ReportId, LogLevel (Info/Error),
                Message, Details (stack trace), Timestamp
```

### Files involved

| File | Role |
|---|---|
| `Application/Services/MigrationService.cs` | Orchestration — loops reports, manages job state, error handling |
| `Infrastructure/PowerBiClient/PowerBiRestClient.cs` | PBIX template generation; Azure AD auth; Power BI REST API upload |
| `Application/Interfaces/IPowerBiClient.cs` | `CreateReportAsync()`, `UploadReportAsync()`, `PublishReportAsync()` |
| `Infrastructure/AzureOpenAIClient/AzureBlobStorageClient.cs` | Uploads files to Azure Blob Storage |
| `Application/Interfaces/IBlobStorageClient.cs` | `UploadAsync()`, `ExistsAsync()`, `DeleteAsync()` |
| `Infrastructure/Repositories/MigrationJobRepository.cs` | Job CRUD + `GetSummaryAsync()` |
| `Infrastructure/Repositories/MigrationLogRepository.cs` | Append-only log insert |
| `Database/StoredProcedures/sp_UpdateMigrationStatus.sql` | Atomic status update |
| `Database/StoredProcedures/sp_GetMigrationSummary.sql` | Aggregated success/fail counts |

---

## 9. Supporting Infrastructure

### Azure OpenAI Integration (`AzureOpenAiHttpClient.cs`)

Two capabilities used by different features:

| Method | Used by | Purpose |
|---|---|---|
| `GetEmbeddingAsync(text)` | Duplicate Detection (Tier 2 & 3) | Returns a float vector for cosine similarity comparison |
| `DiscoverKpiGroupsFromSqlAsync(summaries)` | KPI Grouping | Sends SQL summaries to GPT-4o; returns group names, descriptions, reasons, and report ID lists |

### HTTP Resilience (Polly)
All outbound HTTP clients (SAP BO, Power BI) use a retry policy:
- Retries on transient errors and HTTP 429 (Too Many Requests)
- Exponential back-off: 2s → 4s → 8s (3 attempts)

### Dependency Injection structure

```
Program.cs
  ├── services.AddApplicationServices()   ← registers all 6 services
  └── services.AddInfrastructureServices() ← registers all repositories + HTTP clients
```

---

## 10. Database Schema Summary

### Tables

| Table | Purpose |
|---|---|
| `KpiGroups` | KPI group definitions — `Name`, `Description`, `Reason` |
| `Reports` | One row per SAP BO report — all metadata including `UsageStatus`, `IsScheduled`, `LastRunDate` |
| `ReportElements` | Visual elements (tables, charts, etc.) extracted from each report |
| `ReportSQLs` | SQL queries per report — `SqlText`, `SqlFingerprint`, `QueryName` |
| `ReportDataSources` | Database connections used by each report |
| `ReportSqlAnalysis` | Extracted SQL signals (tables, columns, aggregations) — used for LLM grouping input |
| `ReportKpiGroupMapping` | Explicit `(ReportId, KpiGroupId)` mapping with `AssignedAt` timestamp |
| `KpiRegistry` | Searchable KPI catalogue built from discovered groups |
| `DuplicateReports` | Duplicate pairs — `SimilarityScore`, `RecommendedAction`, `DetectionMethod` |
| `MigrationJobs` | One job per report migration — `Status`, `OutputPath`, `BlobPath` |
| `MigrationLogs` | Append-only log entries per job |

### Migration Scripts (run in order)

| Script | What it does |
|---|---|
| `001_CreateTables.sql` | Creates all core tables |
| `002_CreateStoredProcedures.sql` | Creates all stored procedures |
| `003_CreateReportSqlAnalysis.sql` | Creates `ReportSqlAnalysis` table |
| `004_KpiGroupEnhancements.sql` | Adds `Reason` to `KpiGroups`; creates `ReportKpiGroupMapping` |
| `005_DuplicateReportEnhancements.sql` | Adds `DetectionMethod` column to `DuplicateReports` |

### Analysis Queries

| Query File | What it contains |
|---|---|
| `Queries/DuplicateReports.sql` | Full pair list, summary by tier, exact-duplicates-only, reports-involved view |
| `Queries/GroupReports.sql` | Full group+report list, group summary, ungrouped reports, compact pivot |

---

## 11. Configuration Reference

All settings live in `appsettings.json` (both `ConsoleRunner` and `API` projects have identical keys).

```json
{
  "SapBo": {
    "BaseUrl":           "http://your-sap-bo-server:8080",
    "Username":          "your-username",
    "Password":          "your-password",
    "AuthType":          "secEnterprise",
    "BatchSize":         100,
    "RetryCount":        3,
    "RetryDelaySeconds": 2
  },

  "AzureOpenAi": {
    "Endpoint":                "https://your-openai.openai.azure.com/",
    "ApiKey":                  "your-api-key",
    "ChatDeploymentName":      "gpt-4o",
    "EmbeddingDeploymentName": "text-embedding-3-large",
    "ApiVersion":              "2024-02-01"
  },

  "StaleReport": {
    "StaleDaysThreshold": 180          // Reports not run in 180+ days → Stale
  },

  "DuplicateDetection": {
    "SqlFingerprintJaccardThreshold": 0.80,   // Tier 1 partial match threshold
    "SqlEmbeddingThreshold":          0.90,   // Tier 2 SQL cosine similarity threshold
    "MetadataEmbeddingThreshold":     0.92    // Tier 3 name/description threshold
  },

  "PowerBi": {
    "TenantId":        "your-tenant-id",
    "ClientId":        "your-client-id",
    "ClientSecret":    "your-client-secret",
    "WorkspaceId":     "your-workspace-id",
    "OutputDirectory": "output/pbix"
  },

  "AzureBlob": {
    "ConnectionString": "DefaultEndpointsProtocol=https;...",
    "ContainerName":    "powerbi-reports"
  },

  "Migration": {
    "OutputDirectory": "output/pbix",
    "UploadToBlob":    true
  }
}
```

---

*Document generated: March 2026*
