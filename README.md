# foca-project-export

**FOCA plugin** to export and import complete FOCA projects as a single **`.foca`** archive:
- **DB entities & relations** (documents, authors, emails, paths, software, domains, servers, …)
- **Downloaded evidence files** (PDF/DOCX/XLSX/PPTX/…)
- **Versioned manifest** + **SHA-256** integrity checks
- **Merge/Overwrite** import modes

> Targets FOCA Open Source 3.x (C# / .NET Framework 4.8). Works with SQL Server Express/Developer and LocalDB.

---

## Features

- **Export to `.foca` (ZIP container)**
  - `/manifest.json` (format & app version, timestamps, project id/name, DB provider)
  - `/db/tables/*.csv` (or JSONL) UTF-8 with BOM
  - `/files/…` evidence files + per-file SHA-256
  - `/meta/config.json` (search engines, extensions, options)
  - `/logs/session.log` (optional)

- **Import from `.foca`**
  - Manifest/schema validation; **Merge** (de-dup by natural keys/hashes) or **Overwrite**
  - Batched inserts with transactions and progress
  - Restores evidence files and re-links rows

- **FOCA UI integration**
  - `Project → Export → Export Project as .foca…`
  - `Project → Import → Import Project from .foca…`
  - Progress dialog, cancel, and summary

---

## Requirements

- Windows 10/11 • FOCA Open Source 3.x • .NET Framework 4.8  
- SQL Server Express/Developer or LocalDB (uses FOCA’s existing connection)

---

## Install

1. Download the latest release ZIP.
2. Copy `FocaProjectExport.dll` (and `lib/` if present) into FOCA’s **Plugins** directory.
3. Restart FOCA. New menu items appear under **Project → Export/Import**.

---

## Usage

### Export
1. Open a project in FOCA.
2. `Project → Export → Export Project as .foca…`
3. Choose destination, **Include evidence files** (default ON), click **Start**.
4. Review summary (rows/files, total size, hashes).

### Import
1. `Project → Import → Import Project from .foca…`
2. Pick the `.foca` file → choose **Merge** or **Overwrite**.
3. Review summary and open the project.

---

## `.foca` Format (v1.0)

