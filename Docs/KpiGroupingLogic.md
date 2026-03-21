# KPI Grouping Logic — How It Works

The KPI Grouping feature automatically classifies SAP BusinessObjects reports into meaningful
business groups based on what each report **actually measures in SQL** — not by matching
predefined category labels. It is driven by Azure OpenAI GPT-4o and works in four distinct phases.

---

## Overview

```
All Reports
    │
    ▼
Phase 1: SQL Signal Extraction  (regex-based, no AI)
    │  • Tables referenced
    │  • Columns selected
    │  • Aggregation functions (SUM, COUNT, AVG …)
    │  • Join patterns
    │  • Filter/WHERE keywords
    │
    ▼  Compact SQL Summary per report (≤ 500 chars)
    │
Phase 2: LLM Grouping  (Azure OpenAI GPT-4o)
    │  • Batches of up to 100 reports sent per call
    │  • LLM discovers group names purely from SQL patterns
    │  • Returns JSON: group name + description + reason + reportIds[]
    │
    ▼  Raw discovered groups (may overlap across batches)
    │
Phase 3: Group Merging
    │  • Groups with identical names across batches are merged
    │  • Report ID lists are de-duplicated
    │
    ▼  Final unique group list
    │
Phase 4: Persistence
       • KpiGroups table — upsert (reuse if same name exists)
       • Reports.KpiGroupId — updated per report
       • ReportKpiGroupMapping — explicit audit-trail mapping
       • ReportSqlAnalysis — technical signals stored for traceability
```

---

## Phase 1 — SQL Signal Extraction

For every report that has at least one SQL query, the service runs five regex-based extractors
directly on the SQL text. No AI is used here — it is fast, deterministic, and free.

### 1.1 Tables Referenced

```
Pattern: (?:FROM|JOIN)\s+((?:\w+\.)?(\w+))(?:\s+(?:AS\s+)?\w+)?
```

Captures every identifier that follows `FROM` or `JOIN`, including schema-qualified names
(`dbo.SalesOrders` → `DBO.SALESORDERS`). Reserved SQL words and single-character aliases
are filtered out. Up to **20 unique table names** are kept.

**Example input SQL:**
```sql
SELECT * FROM dbo.SalesOrders s
INNER JOIN dbo.Customers c ON s.CustomerId = c.Id
```
**Extracted tables:** `DBO.SALESORDERS`, `DBO.CUSTOMERS`

---

### 1.2 Columns Selected

```
Pattern: SELECT\s+(.*?)\s+FROM  →  then \b([A-Za-z_]\w+)\b per token
```

Pulls the SELECT clause (up to the first FROM), then extracts every identifier longer than
2 characters that is not a reserved word or aggregate function. Up to **15 unique column names**
are kept.

**Example:** `SELECT OrderDate, TotalAmount, CustomerId FROM …`
→ Columns: `ORDERDATE`, `TOTALAMOUNT`, `CUSTOMERID`

---

### 1.3 Aggregation Patterns

Scans for the presence of these SQL constructs using word-boundary regex:

| Detected construct | Indicates |
|---|---|
| `SUM(…)` | Totalling a numeric value |
| `COUNT(…)` | Counting rows/records |
| `AVG(…)` | Averaging (e.g. average order value) |
| `MAX(…)` / `MIN(…)` | Range analysis |
| `STDEV(…)` / `VARIANCE(…)` | Statistical spread |
| `GROUP BY` | Data grouped to a dimension |
| `HAVING` | Post-aggregation filtering |
| `ORDER BY` | Result sorting |
| `PARTITION BY` | Window function (running totals, ranks) |

---

### 1.4 Join Patterns

Identifies which types of JOINs are present:
`INNER JOIN`, `LEFT JOIN`, `RIGHT JOIN`, `FULL OUTER JOIN`, `CROSS JOIN`

Complex multi-table joins (e.g. sales → customers → products) are a strong signal that
the report spans multiple business entities and likely measures a composite KPI.

---

### 1.5 Filter Keywords (WHERE clause)

Extracts meaningful identifiers from the WHERE clause (e.g. `FISCAL_YEAR`, `DEPARTMENT_ID`,
`REGION_CODE`). Also flags special filter patterns:

| Pattern | Signal |
|---|---|
| `BETWEEN` | Range filter (e.g. date range) |
| `IN (…)` | Categorical filter |
| `LIKE` | Fuzzy text match |
| `DATEDIFF` / `DATEADD` / `YEAR(` / `MONTH(` | Time-based filtering |

---

### 1.6 Building the SQL Summary

All five signal lists are assembled into a compact text block capped at **500 characters**,
which is what gets sent to the LLM. This keeps token usage low and focuses the model on
the business-relevant signals rather than raw SQL syntax.

**Example SQL Summary output:**
```
Tables: DBO.SALESORDERS, DBO.CUSTOMERS, DBO.PRODUCTS
Columns: ORDERDATE, TOTALAMOUNT, CUSTOMERID, REGIONCODE
Aggregations: SUM, COUNT, GROUP BY, ORDER BY
Joins: INNER JOIN, LEFT JOIN
Filters: FISCAL_YEAR, REGION_CODE, DATE_FILTER
```

This summary — not the raw SQL — is what gets sent to GPT-4o.

---

## Phase 2 — LLM Grouping with GPT-4o

### 2.1 Batching

Reports are sent to GPT-4o in batches of **up to 100 at a time**.

- GPT-4o has a 128,000-token context window
- Each SQL summary is capped at ~500 characters ≈ ~125 tokens
- 100 reports × ~125 tokens = ~12,500 tokens — well within the window
- `MaxTokens = 4000` reserved for the model's JSON response

### 2.2 System Prompt (Exact Instruction to GPT-4o)

```
You are a senior data architect analysing SQL queries from a legacy SAP BusinessObjects environment.

Your task is to identify which reports measure the same or closely related KPIs by examining:
- The database tables they reference
- The columns they SELECT, GROUP BY, or filter on
- The aggregation functions used (SUM, COUNT, AVG, etc.)
- The join patterns between tables
- The filter conditions applied

Group reports that share the same core business measurement (e.g. all reports that
aggregate revenue by date from similar tables, or all reports measuring headcount
from HR tables). The group names must be derived from the SQL content — do NOT use
generic labels like "Financial Performance". Instead use specific names like
"Monthly Revenue by Customer Segment" or "Headcount Variance by Department".

Respond ONLY with valid JSON in this exact shape:
{
  "groups": [
    {
      "name": "<specific KPI name derived from SQL patterns>",
      "description": "<technical description: which tables/columns define this KPI>",
      "reason": "<explicit explanation of WHY these reports are grouped — cite the specific
                  shared tables, columns, aggregation functions, join patterns, or filter
                  fields that prove they measure the same KPI>",
      "reportIds": [<integer>, ...]
    }
  ]
}

Every report ID in the input must appear in exactly one group.
Use "Ungrouped / No SQL Pattern Match" for reports whose SQL does not clearly
indicate a shared KPI with any other report.
```

Key design decisions in this prompt:
- **No predefined categories** — the model must invent group names from SQL evidence
- **Specific names required** — e.g. "Monthly Revenue by Customer Segment", not "Finance"
- **Every report must be assigned** — ungrouped reports go to a fallback group
- **Reason field is mandatory** — cites the exact SQL evidence (tables, columns, aggregations)
- **Temperature = 0.1** — very low randomness ensures deterministic, consistent grouping

### 2.3 User Prompt Format

Each report is presented to the model as:

```
--- Report ID: 42 ---
Tables: DBO.SALESORDERS, DBO.CUSTOMERS
Columns: ORDERDATE, TOTALAMOUNT, CUSTOMERID
Aggregations: SUM, GROUP BY, ORDER BY
Joins: INNER JOIN
Filters: FISCAL_YEAR, DATE_FILTER

--- Report ID: 43 ---
Tables: DBO.INVOICES, DBO.CUSTOMERS
Columns: INVOICEDATE, INVOICEAMOUNT, CUSTOMERID
Aggregations: SUM, COUNT, GROUP BY
Joins: LEFT JOIN
Filters: FISCAL_YEAR, REGION_CODE
...
```

### 2.4 LLM Response (JSON)

```json
{
  "groups": [
    {
      "name": "Monthly Revenue by Customer",
      "description": "Reports aggregating invoice/order totals joined to customers, grouped by date",
      "reason": "Reports 42 and 43 both SUM monetary amounts (TotalAmount, InvoiceAmount) from
                 order/invoice tables joined to DBO.CUSTOMERS, filtered by FISCAL_YEAR,
                 using GROUP BY — they measure the same customer revenue KPI.",
      "reportIds": [42, 43]
    },
    {
      "name": "Ungrouped / No SQL Pattern Match",
      "description": "Reports with insufficient SQL signals to match a shared KPI",
      "reason": "No common tables, columns, or aggregation patterns found.",
      "reportIds": [99]
    }
  ]
}
```

---

## Phase 3 — Merging Groups Across Batches

When the total number of reports exceeds 100, multiple LLM calls are made. The same KPI
group name may appear in different batches (e.g. "Monthly Revenue by Customer" in batch 1
and batch 2).

The merge step uses **case-insensitive name matching**:
- If a group with the same name already exists in the merged set → add the new report IDs to it (deduplicating)
- If the name is new → create a new entry

This ensures each KPI group appears exactly once in the final output regardless of batch boundaries.

---

## Phase 4 — Persistence

Four writes happen for each discovered group:

### 4.1 KpiGroups Table — Upsert

```
IF group name already exists in DB:
    UPDATE KpiGroups SET Reason = <latest LLM reason>  (keeps reasoning fresh)
ELSE:
    INSERT INTO KpiGroups (Name, Description, Reason, CreatedAt)
```

This makes the job **safe to re-run** — existing groups are refreshed, not duplicated.

### 4.2 Reports Table — Denormalised Foreign Key

```sql
UPDATE Reports SET KpiGroupId = @groupId WHERE Id = @reportId
```

The `Reports` table has a computed column `KpiGroupName` that reads from the joined
`KpiGroups` row, giving O(1) lookups without a join.

### 4.3 ReportKpiGroupMapping Table — Explicit Audit Mapping

```sql
IF NOT EXISTS (SELECT 1 FROM ReportKpiGroupMapping WHERE ReportId = @r AND KpiGroupId = @g)
    INSERT INTO ReportKpiGroupMapping (ReportId, KpiGroupId, AssignedAt) VALUES (…)
```

This is an idempotent junction table that serves as an audit trail, queryable independently
of the denormalised `KpiGroupId` on `Reports`.

### 4.4 ReportSqlAnalysis Table — Technical Evidence

Stores the raw extracted signals (tables, columns, aggregations, joins, filters, summary)
per report so that the technical basis for every grouping decision is inspectable later.

---

## Database Schema (Relevant Tables)

### KpiGroups
| Column | Type | Notes |
|---|---|---|
| `Id` | INT PK | Auto-increment |
| `Name` | NVARCHAR(200) | UNIQUE — the KPI group name |
| `Description` | NVARCHAR(MAX) | LLM-generated technical description |
| `Reason` | NVARCHAR(MAX) | LLM-generated SQL evidence explanation |
| `CreatedAt` | DATETIME | Timestamp |

### ReportSqlAnalysis
| Column | Type | Notes |
|---|---|---|
| `ReportId` | INT UNIQUE FK | One row per report |
| `TablesReferenced` | NVARCHAR(MAX) | Comma-separated table names |
| `ColumnsUsed` | NVARCHAR(MAX) | Comma-separated column names |
| `AggregationPatterns` | NVARCHAR(500) | Detected SUM, COUNT, GROUP BY, etc. |
| `JoinPatterns` | NVARCHAR(500) | INNER JOIN, LEFT JOIN, etc. |
| `FilterPatterns` | NVARCHAR(MAX) | WHERE clause keywords |
| `SqlSummary` | NVARCHAR(MAX) | The compact text sent to GPT-4o |
| `AnalysedAt` | DATETIME | Timestamp |

### ReportKpiGroupMapping
| Column | Type | Notes |
|---|---|---|
| `ReportId` | INT FK | Unique with KpiGroupId |
| `KpiGroupId` | INT FK | Unique with ReportId |
| `AssignedAt` | DATETIME | When the LLM made this assignment |

---

## What Makes This Approach Different

| Traditional Approach | This Implementation |
|---|---|
| Predefined category list (Finance, HR, etc.) | Emergent — LLM invents group names from SQL |
| Reports classified by name/description | Reports classified by what their SQL *actually measures* |
| Static, manually maintained | Dynamic — re-run discovers new groups as reports change |
| Opaque classification | Fully auditable — every decision has a `Reason` with SQL citations |
| No fallback for edge cases | Reports with no shared SQL pattern → "Ungrouped / No SQL Pattern Match" |

---

## What Reports Are Skipped

A report is skipped in Phase 1 (not sent to the LLM) if:
- It has **no SQL queries** in the `ReportSql` table, OR
- All of its SQL text fields are empty or whitespace

These reports are counted in `ReportsWithoutSql` in the job result. They do not receive a
KPI group assignment and remain ungrouped in the database.

---

## Re-run Behaviour

The job is fully **idempotent** and safe to re-run at any time:

- Existing KPI group names are reused (not duplicated) — only the `Reason` is refreshed
- `ReportKpiGroupMapping` uses `IF NOT EXISTS` — no duplicate rows
- `ReportSqlAnalysis` uses `UPSERT` (UPDATE if exists, INSERT if not)
- New reports added since the last run will be picked up and grouped
- If the LLM produces a new group name not seen before, it is inserted as a new group
