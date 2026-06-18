# Arch-Web-Google-Like

Projet Mii M2 Architecture Web — Full-text PDF search engine built with Blazor Server.

## Quick Start

```bash
docker compose up -d
# → MongoDB on :27017, PostgreSQL on :5432, App on :8080-8082, Caddy LB on :3000
```

Upload PDF/DOC files via the **/upload** page (click G icon → Upload), then search across their text content on the home page.

---

## Architecture

```
Browser (SignalR)
    │
    ▼
Blazor Server (.NET 11 Preview 5)
├── Pages/Index.razor       — Search UI + performance benchmarking
├── Components/UploadGeneric — Drag & drop file upload
├── Controllers/FileController — REST file download (/api/file/{id})
├── Services/FileService.cs  — MongoDB full-text search engine
├── Services/PgFileService.cs — PostgreSQL 18 full-text search engine
└── Caching: PdfTextCache (MemoryCache) + RepositoryCache
    │
    ▼
Databases (dual-backend)
├── MongoDB    (collection: "files")  — byte[] documents + GridFS (planned)
└── PostgreSQL (table: "files")       — tsvector + GIN + pg_trgm full-text engine
```

### Search Strategies

| Strategy | Method | Description |
|----------|--------|-------------|
| **PG tsvector** | `SearchInContent` (PG) | PostgreSQL full-text engine — `websearch_to_tsquery` + `ts_rank` + GIN index |
| Sequential | `SearchInContent` (Mongo) | Single-threaded, baseline |
| Parallel.ForEachAsync | `SearchInContentParelle` | Parallel across files |
| Nested Parallel | `SearchInContentParelleDeep2` | Parallel across files + pages |
| Task.WhenAll | `SearchInContentTask` | Async-native with Task.WhenAll |
| **IAsyncEnumerable** | `SearchInContentAsyncEnum` | Streaming results (yield return) |

### Performance Optimizations

| Optimization | What it does | Impact |
|---|---|---|
| **PdfTextCache** | MemoryCache stores extracted PDF text per file ID | 🔥 Repeat searches skip PdfPig entirely |
| **Upload pre-warming** | PDF text extracted & cached at upload time | 🔥 First search on new files is instant |
| **PG tsvector engine** | PostgreSQL full-text search with GIN index | 🔥 Server-side ranked search, no full-table scan |
| **Search debounce (300ms)** | Waits 300ms of inactivity before searching | Saves wasted searches while typing |
| **CancellationToken** | New search cancels in-flight search | No stale results, saves CPU |
| **ETag + Cache-Control** | FileController returns 304 for unchanged files | Browser caches PDFs, fewer downloads |
| **MERGE upsert** | Single SQL statement for insert-or-update | Fewer round-trips than read-then-write |
| **pg_trgm GIN index** | Accelerates ILIKE filename searches | Fast filename filtering |

---

## Changelog

### v2.0.0 — .NET 11 + PostgreSQL 18 + Full-text Engine (2025-06)

#### 🚀 .NET 11 Preview 5 Upgrade
- **Target framework**: `net10.0` → **`net11.0`** with `<LangVersion>preview</LangVersion>`
- **NuGet packages** all bumped to latest:
  - MongoDB.Driver 2.25.0 → **3.2.1**
  - MudBlazor 6.19.1 → **8.3.0**
  - Serilog 3.1.1 → **4.2.0**
  - Swashbuckle 6.6.1 → **7.2.0**
  - PdfPig 0.1.8 → **0.1.9**
  - Added **Npgsql 9.0.2** for PostgreSQL support
- **Dockerfile**: SDK/runtime from `7.0-alpine` → **`11.0-preview-alpine`**

#### 🐘 PostgreSQL 18 Full-Text Search Engine
- **New files**: `PgFileModel.cs`, `PgConnectionModel.cs`, `IPgFileRepo.cs`, `PgFileService.cs`
- **Raw Npgsql ADO.NET** — no ORM overhead, full control over PG 18 features
- **`tsvector` full-text index** — `GENERATED ALWAYS AS (to_tsvector('french', ExtractedText || ' ' || FileName)) STORED` with GIN index
- **`websearch_to_tsquery` + `ts_rank`** — Google-style search syntax, server-side ranked results, no full-table scan
- **`MERGE` upsert** — single SQL statement replaces read-then-create-or-update (PG 15+)
- **`pg_trgm` extension** — GIN `gin_trgm_ops` index accelerates all `ILIKE` filename queries
- **`extracted_text` column** — PDF text pre-extracted via PdfPig at upload time, stored for instant tsvector indexing
- **Auto-schema bootstrap** — extensions, table, and all 3 indexes created on service startup
- **Both backends coexist** — MongoDB via `IFileRepo`, PostgreSQL via `IPgFileRepo`

#### ⚡ UX & Performance Optimizations
- **PDF text caching** (`PdfTextCache`): extracted page texts cached in `MemoryCache` keyed by file ID — repeat searches skip PdfPig entirely
- **Pre-warm at upload**: PDF text extracted & cached immediately after upload — first search on new files is instant
- **PG server-side search**: tsvector + GIN index means PostgreSQL filters and ranks natively — no C# full-table scan
- **Search debounce (300ms)**: main search bar waits 300ms of inactivity before firing, reducing wasted searches
- **CancellationToken propagation**: typing a new search term cancels any in-flight search, preventing stale results
- **Empty state UI**: "No results found" alert instead of blank page when search returns nothing
- **Search bar labels**: main search vs performance benchmarks clearly separated with `MudAlert` labels
- **Relative embed URLs**: PDF embed uses `api/file/{id}` instead of hardcoded `localhost:3000` / `localhost:7104`
- **ETag + Cache-Control**: FileController returns SHA256 ETag + `Cache-Control: max-age=3600` + `304 Not Modified` — browser caches PDFs for 1 hour
- **Caddy round-robin**: `lb_policy first` → `round_robin` with health checks across 3 replicas

#### ⚡ .NET 11 New Features Applied
- **`IAsyncEnumerable<T>` streaming search** — implemented `SearchInContentAsyncEnum` with `yield return` + `CancellationToken` on both backends
- **`[EnumeratorCancellation]`** attribute for proper cancellation propagation

#### 🐛 Bug Fixes (round 1)
- **`Parallel.ForEach` async void** → **`Parallel.ForEachAsync`** — old pattern silently swallowed exceptions and had undefined behavior
- **`FileController.GetOne`** — removed `.Result` blocking, added `await` + null-check (404) + ETag + Cache-Control
- **`CreateAsync` / `UpdateAsync`** — replaced `IsCompletedSuccessfully` anti-pattern with proper `await` + result inspection
- **`FileService` secondary constructor** — now properly injects `PdfTextCache` (was null → NRE on search)
- **`Regex.Escape`** — search keywords are now escaped before regex building (was injecting raw user input into regex)

#### 🐛 Bug Fixes (round 2 — UX-critical)
- **Missing `/health` endpoint** — Caddy was checking a non-existent route; added `MapGet("/health", ...)` returning healthy + timestamp
- **SearchBarItem `@onchange` → `@oninput`** — search only fired on blur (click away). Now fires on every keystroke so the 300ms debounce actually works
- **`OnParametersSetAsync` ran on every render** — Blazor calls this on every parameter/state change. Added `_initialized` guard so initial search only runs once
- **`MemoryStream` in FileController** — replaced `new MemoryStream(db.Content)` with `File(db.Content, mime)` directly returning byte[] — avoids copying entire file to managed heap
- **`ErrorBoundary`** — search results wrapped in `<ErrorBoundary>` so a rendering exception shows a "Retry" button instead of a blank page
- **Streaming search wired to UI** — main search `InputChanged` now uses `await foreach` + `SearchInContentAsyncEnum` so results appear incrementally as they're found, not all at once

#### 🧹 Code Quality
- **GlobalUsing.cs**: removed 11 unused/implicit imports
- **Index.razor**: removed ~100 lines of commented-out MudTable code, added empty/no-results states
- **WeatherForecastService**: removed non-existent service registration from `Program.cs`
- **FileService.cs**: extracted shared helpers `SearchPage()` / `Matches()` / `ToViewModel()` / `GetPagesText()` — DRY across all 5 strategies
- **PgFileService.cs**: complete rewrite leveraging PG 18 features — `tsvector`, `MERGE`, `pg_trgm`, `websearch_to_tsquery`
- **RepositoryCache**: unified TTL to 5 minutes (was inconsistent 10s vs 6min), removed unused Redis dependency
- **MemoryStorageUtility**: added eviction at 50 entries (was unbounded Dictionary growth)
- **Dead code marked** with `🏷️ PLANNED` comments: `CacheService.cs`, `ICacheService.cs`, `GridSfService.cs`
- **appsettings.json**: added `DatabaseProvider` switch and `PostgresDatabase` config block

#### 🐳 DevOps
- Docker Compose: PostgreSQL 16-alpine with healthcheck + persistent `pgdata` volume
- Caddy: `lb_policy round_robin` with `health_uri /health` across 3 replicas
- FileController: ETag + Cache-Control headers for CDN/browser caching

---

## Configuration

Edit `appsettings.json`:

```json
{
  "MongoDatabase": {
    "ConnectionString": "mongodb://root:123456@gl-mongo:27017",
    "DatabaseName": "google-like",
    "ThreedCollectionName": "files"
  },
  "PostgresDatabase": {
    "ConnectionString": "Host=gl-postgres;Port=5432;Database=google_like;Username=postgres;Password=postgres123",
    "DatabaseName": "google_like",
    "TableName": "files"
  },
  "DatabaseProvider": "MongoDB"
}
```

---

## Pour intégrer les fichiers dans la BDD

- Cliquer sur l'icône "G" puis cliquer sur **Upload** (ou aller sur `/upload`)
- Cliquer sur **Parcourir**
- Sélectionner le/les documents (sélection multiple possible) puis faire **Ouvrir**
- Attendre les popups vertes de validation en bas à gauche
