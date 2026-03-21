# Duplicate Detection Methods

There are three types of duplicate detection available in the system. They run as a **three-tier pipeline** — each tier is a fallback for the previous one.

---

## Tier 1 — SQL Fingerprint (Jaccard Similarity)

### How it works
- Extracts all SQL queries from a report and tokenises them into a set of "fingerprint tokens" — words, keywords, table names, etc.
- Computes an **MD5 hash** of each token set to create a "fingerprint"
- Compares two reports' fingerprint sets using **Jaccard similarity**:

```
Score = |Intersection| / |Union|
```

i.e. what fraction of tokens both reports share.

### What it catches
- Reports that are **byte-for-byte identical** or only differ in whitespace/formatting (score = 1.0)
- Reports with the **same underlying queries** but different column aliases or minor reordering

### Characteristics
| Property | Value |
|---|---|
| Input | SQL tokens (hashed) |
| Algorithm | Jaccard similarity |
| AI cost | None (deterministic) |
| Speed | Very fast |
| Best for | Exact / near-exact SQL duplicates |

### Limitation
Misses reports where the SQL logic is equivalent but written differently (e.g. JOIN vs subquery, different parameter names).

---

## Tier 2 — SQL Embedding (Cosine Similarity)

### How it works
- Concatenates the full SQL text of each report into a single string
- Sends it to **Azure OpenAI `text-embedding-3-large`** to get a high-dimensional vector (embedding) that captures the *semantic meaning* of the SQL
- Compares two reports' vectors using **cosine similarity**:

```
Score = (A · B) / (|A| × |B|)
```

Measures the angle between vectors; 1.0 = identical direction.

### What it catches
- Reports whose SQL is **semantically equivalent** but syntactically different (different aliases, different JOIN styles, restructured subqueries)
- Reports that query the **same business data** even with different table/column names if the AI model understands the semantic context

### When it is used
Both reports have SQL, but Tier 1 fingerprint similarity falls below the exact-match threshold.

### Characteristics
| Property | Value |
|---|---|
| Input | Full SQL text |
| Algorithm | Cosine similarity |
| AI cost | Yes (Azure OpenAI API) |
| Speed | Slower |
| Best for | Semantically equivalent SQL |

### Limitation
Requires SQL to be present; costs AI tokens per comparison.

---

## Tier 3 — Metadata Embedding (Cosine Similarity)

### How it works
- Concatenates the report's **Name + Description** into a text string (no SQL involved)
- Sends it to **Azure OpenAI `text-embedding-3-large`** to get a semantic embedding vector
- Compares vectors using **cosine similarity**, same as Tier 2

### What it catches
- Reports that have **no SQL at all** (e.g. dashboard / visualisation-only reports)
- Reports where the name and description clearly describe the same business purpose even if the underlying queries differ slightly
- Duplicate reports created by copy-paste where only minor name variations exist (e.g. "Sales Report Q1" vs "Q1 Sales Report")

### When it is used
A report has no SQL to compare — falls back entirely on metadata.

### Characteristics
| Property | Value |
|---|---|
| Input | Report name + description |
| Algorithm | Cosine similarity |
| AI cost | Yes (Azure OpenAI API) |
| Speed | Slower |
| Best for | Reports with no SQL |

---

## Summary Comparison

| | SQL Fingerprint | SQL Embedding | Metadata Embedding |
|---|---|---|---|
| **Input** | SQL tokens (hashed) | Full SQL text | Report name + description |
| **Algorithm** | Jaccard similarity | Cosine similarity | Cosine similarity |
| **AI cost** | None (deterministic) | Yes (OpenAI API) | Yes (OpenAI API) |
| **Speed** | Very fast | Slower | Slower |
| **Best for** | Exact/near-exact SQL duplicates | Semantically equivalent SQL | Reports with no SQL |
| **Tier** | 1 (runs first) | 2 (fallback) | 3 (final fallback) |

---

## Pipeline Flow

```
For each pair of reports:
│
├─► Tier 1: SQL Fingerprint (Jaccard)
│       Score ≥ threshold? → Mark as duplicate (SqlFingerprint)
│       Score < threshold ──────────────────────────────────────►
│                                                                │
│                                                    Tier 2: SQL Embedding (Cosine)
│                                                        Both have SQL?
│                                                        Score ≥ threshold? → Mark as duplicate (SqlEmbedding)
│                                                        Score < threshold ──────────────────────────────────►
│                                                                                                             │
│                                                                                              Tier 3: Metadata Embedding (Cosine)
│                                                                                                  Score ≥ threshold? → Mark as duplicate (MetadataEmbedding)
│                                                                                                  Score < threshold → Not a duplicate
```

The system runs all three tiers in sequence — each tier only applies to pairs not already identified by an earlier tier, making it both **comprehensive** and **cost-efficient**.
