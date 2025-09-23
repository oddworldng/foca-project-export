# Plugin FOCA: Export/Import .foca

## Instalación
- Compila la solución `foca-project-export.sln` (Debug o Release).
- Copia `plugins/Foca.ExportImport.dll` (y `Newtonsoft.Json.dll` si se requiere) a la carpeta de plugins de FOCA.
- Inicia FOCA; verás las opciones en `Project > Export/Import`.

## Uso
- Export: selecciona destino `.foca` (único formato), incluye binarios por defecto, progreso granular.
- Import: selecciona `.foca`, elige Merge u Overwrite, valida hashes y restaura evidencias a sus rutas originales.

## Formato `.foca`
Estructura ZIP:
- `/manifest.json` (incluye campos `author`, `author_website`, `author_email`)
- `/db/tables/*.jsonl` (UTF-8; por defecto) o `*.csv` (UTF-8 con BOM)
- `/files/<sha256-prefix>/<sha256>/<archivo>`
- `/meta/config.json`, `/meta/files.jsonl`
- `/logs/session.log` (opcional)

Manifest mínimo (extendido):
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

## Integración con FOCA
- El plugin reutiliza la conexión y el `ProjectId` desde los servicios del host de FOCA.
- Los menús se registran en `Project > Export/Import` siguiendo el ejemplo oficial.

## Requisitos
- Windows 10/11, FOCA v3.4.7.1, .NET Framework 4.7.1, SQL Server/LocalDB.

## Pruebas
- Exporta un proyecto pequeño y uno grande; importa en BD vacía y compara conteos y hashes.
- Casos borde: archivos perdidos, permisos, falta de espacio, cancelación a mitad.

## Rendimiento
- Export/Import por lotes, streaming de tablas y binarios, compresión ZIP óptima.

## Licencia
- MIT (compatible con FOCA).
