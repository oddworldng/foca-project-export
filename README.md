# foca-project-export

**FOCA plugin** to export and import complete FOCA projects as a single **`.foca`** archive:
- **DB entities & relations** (documents, authors, emails, paths, software, domains, servers, …)
- **Downloaded evidence files** (PDF/DOCX/XLSX/PPTX/…)
- **Versioned manifest** + **SHA-256** integrity checks
- **Merge/Overwrite** import modes

> Targets FOCA Open Source 3.x (C# / .NET Framework 4.7.1). Works with SQL Server Express/Developer and LocalDB.

> Compatible with FOCA Open Source v3.4.7.1.

---

## Features

- **Export to `.foca` (ZIP container)**
  - `/manifest.json` (format & app version, timestamps, project id/name, DB provider,
    author fields)
  - `/db/tables/*.jsonl` (default; UTF-8) or `.csv` (UTF-8 with BOM)
  - `/files/…` evidence files + per-file SHA-256
  - `/meta/config.json` (project metadata/settings)
  - `/logs/session.log` (optional)

- **Import from `.foca`**
  - Manifest/schema validation; **Merge** (de-dup by natural keys/hashes) or **Overwrite**
  - Batched inserts with transactions and progress
  - Restores evidence files to project evidence root and re-links rows

- **FOCA UI integration**
  - `Project → Export → Export Project as .foca…`
  - `Project → Import → Import Project from .foca…`
  - Progress dialog, cancel, and summary

- **Project-scoped only**
  - Exports/imports strictly the active project: rows filtered by `ProjectId`
    and evidence files resolved from DB paths belonging to that project only

---

## Requirements

- Windows 10/11
- FOCA Open Source v3.4.7.1
- .NET Framework 4.7.1
- SQL Server Express/Developer or LocalDB (uses FOCA’s existing connection)

---

## Install

1. Build the solution `foca-project-export.sln` (Debug or Release).
2. Copy `Foca.ExportImport.dll` (and `Newtonsoft.Json.dll` if required) into FOCA’s **Plugins** directory.
3. Start FOCA. New menu items appear under **Project → Export/Import**.

---

## Usage

### Export
1. Open a project in FOCA.
2. `Project → Export → Export Project as .foca…`
3. Choose destination, evidence files are included by default, click **Start**.
4. Review summary (rows/files, total size, hashes).

### Import
1. `Project → Import → Import Project from .foca…`
2. Pick the `.foca` file → choose **Merge** or **Overwrite**.
3. Review summary and open the project.

---

## `.foca` Format (v1.0)

- Container: ZIP with extension `.foca`
- Structure:
  - `/manifest.json`
  - `/db/tables/*.jsonl` (default) or `*.csv`
  - `/files/<sha256-prefix>/<sha256>/<filename>`
  - `/meta/config.json`
  - `/logs/session.log` (optional)

Minimal manifest:

```json
{
  "foca_export_version": "1.0",
  "foca_app_version": "3.x",
  "created_utc": "...",
  "project_id": "...",
  "project_name": "...",
  "db_provider": "SQLServer",
  "db_version": "...",
  "tables": ["Documents", "Authors", "Emails", "Paths", "Software", "Domains", "Servers"],
  "file_count": 0,
  "hash_algorithm": "SHA256",
  "author": "Andres Nacimiento",
  "author_website": "https://andresnacimiento.com/",
  "author_email": "info@andresnacimiento.com"
}
```

Notes:
- Only data/files from the active project are included (DB filtered by `ProjectId`).
- Evidence files’ original relative paths are stored in `/meta/files.jsonl` to restore
  exact destinations on import with SHA-256 validation.

---

## Development

- Target framework: .NET Framework 4.7.1
- Default table export: JSONL (streaming); CSV available (UTF-8 with BOM)
- Inside FOCA, the plugin uses the host’s connection and services, and registers menu
  entries under Project/Plugins following the official example.

