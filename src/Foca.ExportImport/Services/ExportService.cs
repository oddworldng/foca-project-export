using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using Foca.ExportImport.Models;

namespace Foca.ExportImport.Services
{
	public sealed class ExportService
	{
		private readonly DatabaseService databaseService;
		private readonly ZipAndHashService zipAndHashService;
		private readonly SchemaIntrospectionService schemaService = new SchemaIntrospectionService();

		public ExportService(DatabaseService db, ZipAndHashService zip)
		{
			databaseService = db;
			zipAndHashService = zip;
		}

		public void ExportProject(Guid projectId, string projectName, string evidenceFolder, string destinationFocaPath, IProgress<(int percent, string status)> progress, TableExportFormat format = TableExportFormat.Jsonl, bool includeBinaries = true)
		{
			string tempRoot = Path.Combine(Path.GetTempPath(), "foca_export_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(tempRoot);
			string dbDir = Path.Combine(tempRoot, "db", "tables");
			Directory.CreateDirectory(dbDir);
			string filesDir = Path.Combine(tempRoot, "files");
			if (includeBinaries) Directory.CreateDirectory(filesDir);
			string metaDir = Path.Combine(tempRoot, "meta");
			Directory.CreateDirectory(metaDir);

			try
			{
				progress.Report((1, "Exportando tablas"));
				var tables = ExportTables(projectId, dbDir, progress, format);

				progress.Report((70, "Exportando metadatos del proyecto"));
				ExportProjectConfig(projectId, metaDir);

				int fileCount = 0;
				if (includeBinaries)
				{
					progress.Report((75, "Resolviendo ficheros del proyecto desde BD"));
					fileCount = ExportProjectFilesFromDatabase(projectId, evidenceFolder, filesDir, Path.Combine(metaDir, "files.jsonl"), progress);
				}

				progress.Report((90, "Escribiendo manifest"));
				var manifest = new Manifest
				{
					foca_export_version = "1.0",
					foca_app_version = "3.x",
					created_utc = DateTime.UtcNow,
					project_id = projectId.ToString(),
					project_name = projectName,
					db_provider = "SQLServer",
					db_version = "",
					tables = tables.ToArray(),
					file_count = fileCount,
					hash_algorithm = "SHA256",
					author = "Andres Nacimiento",
					author_website = "https://andresnacimiento.com/",
					author_email = "info@andresnacimiento.com"
				};
				WriteJson(Path.Combine(tempRoot, "manifest.json"), manifest);

				progress.Report((95, "Empaquetando .foca"));
				zipAndHashService.CreateZipFromFolder(tempRoot, destinationFocaPath, CompressionLevel.Optimal);

				progress.Report((100, "Exportación completada"));
			}
			finally
			{
				try { Directory.Delete(tempRoot, true); } catch { }
			}
		}

		private List<string> ExportTables(Guid projectId, string outputDir, IProgress<(int, string)> progress, TableExportFormat format)
		{
			var exported = new List<string>();
			using (var conn = databaseService.OpenConnection())
			{
				var tables = schemaService.GetTopologicalOrderProjectTables(conn);
				int idx = 0;
				foreach (var table in tables)
				{
					idx++;
					var columns = schemaService.GetColumns(conn, table);
					if (!columns.Contains("ProjectId")) continue;
					string filePath = Path.Combine(outputDir, table + (format == TableExportFormat.Csv ? ".csv" : ".jsonl"));
					ExportSingleTable(conn, table, columns, projectId, filePath, format);
					exported.Add(table);
					int pct = 5 + (int)((double)idx / Math.Max(1, tables.Count) * 60);
					progress.Report((pct, $"Exportada tabla {table} ({idx}/{tables.Count})"));
				}
			}
			return exported;
		}

		private sealed class TableInfo
		{
			public List<string> Columns = new List<string>();
			public string OrderByColumn;
		}

		private List<string> DiscoverProjectTables(SqlConnection conn)
		{
			var result = new List<string>();
			using (var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'", conn))
			using (var r = cmd.ExecuteReader())
			{
				while (r.Read()) result.Add(r.GetString(0));
			}
			return result;
		}

		private TableInfo GetTableColumns(SqlConnection conn, string table)
		{
			var info = new TableInfo();
			using (var cmd = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t ORDER BY ORDINAL_POSITION", conn))
			{
				cmd.Parameters.AddWithValue("@t", table);
				using (var r = cmd.ExecuteReader())
				{
					while (r.Read()) info.Columns.Add(r.GetString(0));
				}
			}
			info.OrderByColumn = info.Columns.Contains("Id") ? "Id" : info.Columns[0];
			return info;
		}

		private void ExportSingleTable(SqlConnection conn, string table, List<string> columns, Guid projectId, string filePath, TableExportFormat format)
		{
			const int BatchSize = 10000;
			long offset = 0;
			bool firstBatch = true;
			ITableWriter writer = null;
			string orderBy = columns.Contains("Id") ? "Id" : columns[0];
			try
			{
				while (true)
				{
					using (var cmd = new SqlCommand($"SELECT {string.Join(",", columns)} FROM [" + table + "] WHERE ProjectId = @pid ORDER BY [" + orderBy + "] OFFSET @off ROWS FETCH NEXT @take ROWS ONLY", conn))
					{
						cmd.Parameters.AddWithValue("@pid", projectId);
						cmd.Parameters.AddWithValue("@off", offset);
						cmd.Parameters.AddWithValue("@take", BatchSize);
						using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
						{
							if (firstBatch)
							{
								writer = TableWriterFactory.Create(format, filePath, columns);
								firstBatch = false;
							}
							int rows = 0;
							while (reader.Read())
							{
								var values = new object[columns.Count];
								reader.GetValues(values);
								writer.WriteRow(values);
								rows++;
							}
							if (rows < BatchSize) break;
							offset += rows;
						}
					}
				}
			}
			finally
			{
				writer?.Dispose();
			}
		}

		private int ExportProjectFilesFromDatabase(Guid projectId, string evidenceRoot, string destFolder, string filesIndexPath, IProgress<(int, string)> progress)
		{
			var fileRecords = DiscoverProjectFileRecords(projectId);
			int total = fileRecords.Count;
			int count = 0;
			using (var indexWriter = new StreamWriter(new FileStream(filesIndexPath, FileMode.Create, FileAccess.Write, FileShare.Read)))
			{
				for (int i = 0; i < fileRecords.Count; i++)
				{
					var rec = fileRecords[i];
					var originalRel = NormalizeRelative(rec.RelativePath);
					var fileName = string.IsNullOrWhiteSpace(rec.FileName) ? Path.GetFileName(originalRel) : rec.FileName;
					var fullSource = Path.GetFullPath(Path.Combine(evidenceRoot, originalRel));
					if (!File.Exists(fullSource)) continue;
					var hash = zipAndHashService.ComputeSha256(fullSource);
					var subDir = Path.Combine(destFolder, hash.Substring(0, 2), hash);
					Directory.CreateDirectory(subDir);
					var dest = Path.Combine(subDir, fileName);
					File.Copy(fullSource, dest, overwrite: true);
					var fi = new FileInfo(fullSource);
					var json = Newtonsoft.Json.JsonConvert.SerializeObject(new { path = dest.Replace("\\", "/"), sha256 = hash, size = fi.Length, original_rel = originalRel.Replace("\\", "/"), file_name = fileName });
					indexWriter.WriteLine(json);
					count++;
					if (i % 10 == 0)
					{
						int pct = 80 + (int)((double)i / Math.Max(1, total) * 10);
						progress.Report((pct, $"Copiados {i}/{total} ficheros del proyecto"));
					}
				}
			}
			return count;
		}

		private string NormalizeRelative(string rel)
		{
			if (string.IsNullOrWhiteSpace(rel)) return string.Empty;
			var p = rel.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
			return p;
		}

		private sealed class FileRecord
		{
			public string RelativePath;
			public string FileName;
		}

		private List<FileRecord> DiscoverProjectFileRecords(Guid projectId)
		{
			var results = new List<FileRecord>();
			using (var conn = databaseService.OpenConnection())
			{
				// Buscar tablas que contengan columnas de ruta y ProjectId
				var candidateTables = new List<(string table, string pathCol, string nameCol)>();
				using (var cmd = new SqlCommand(@"SELECT c.TABLE_NAME, c.COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS c WHERE c.COLUMN_NAME IN ('FilePath','LocalPath','Path','RelativePath','FullPath','Ruta','Archivo')", conn))
				using (var r = cmd.ExecuteReader())
				{
					var temp = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
					while (r.Read())
					{
						string t = r.GetString(0), c = r.GetString(1);
						if (!temp.ContainsKey(t)) temp[t] = new List<string>();
						temp[t].Add(c);
					}
					foreach (var kv in temp)
					{
						if (TableHasColumn(conn, kv.Key, "ProjectId"))
						{
							string pathCol = SelectFirst(kv.Value, new[] { "RelativePath", "LocalPath", "FilePath", "Path", "FullPath", "Ruta" });
							string nameCol = ColumnIfExists(conn, kv.Key, "FileName") ?? ColumnIfExists(conn, kv.Key, "Name") ?? ColumnIfExists(conn, kv.Key, "Archivo");
							if (pathCol != null) candidateTables.Add((kv.Key, pathCol, nameCol));
						}
					}
				}

				foreach (var c in candidateTables)
				{
					using (var cmd = new SqlCommand($"SELECT [{c.pathCol}] AS RP, {(c.nameCol!=null?"["+c.nameCol+"]":"NULL")} AS FN FROM [" + c.table + "] WHERE ProjectId = @pid", conn))
					{
						cmd.Parameters.AddWithValue("@pid", projectId);
						using (var r = cmd.ExecuteReader())
						{
							while (r.Read())
							{
								var rel = r.IsDBNull(0) ? null : r.GetString(0);
								var name = r.IsDBNull(1) ? null : r.GetString(1);
								if (string.IsNullOrWhiteSpace(rel)) continue;
								results.Add(new FileRecord { RelativePath = rel, FileName = name });
							}
						}
					}
				}
			}
			return results;
		}

		private bool TableHasColumn(SqlConnection conn, string table, string column)
		{
			using (var cmd = new SqlCommand("SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t AND COLUMN_NAME = @c", conn))
			{
				cmd.Parameters.AddWithValue("@t", table);
				cmd.Parameters.AddWithValue("@c", column);
				var o = cmd.ExecuteScalar();
				return o != null;
			}
		}

		private string ColumnIfExists(SqlConnection conn, string table, string col)
		{
			return TableHasColumn(conn, table, col) ? col : null;
		}

		private string SelectFirst(List<string> candidates, string[] order)
		{
			foreach (var o in order)
			{
				foreach (var c in candidates) if (string.Equals(c, o, StringComparison.OrdinalIgnoreCase)) return c;
			}
			return null;
		}

		private void ExportProjectConfig(Guid projectId, string metaDir)
		{
			var data = new Dictionary<string, object>();
			using (var conn = databaseService.OpenConnection())
			{
				// Buscar tabla(s) de proyecto
				var projectTables = new List<string>();
				using (var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' AND TABLE_NAME LIKE '%Project%';", conn))
				using (var r = cmd.ExecuteReader())
				{
					while (r.Read()) projectTables.Add(r.GetString(0));
				}
				foreach (var t in projectTables)
				{
					if (!TableHasColumn(conn, t, "Id")) continue;
					using (var cmd = new SqlCommand($"SELECT * FROM [" + t + "] WHERE Id = @id", conn))
					{
						cmd.Parameters.AddWithValue("@id", projectId);
						using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
						{
							if (r.Read())
							{
								var tableData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
								for (int i = 0; i < r.FieldCount; i++)
								{
									var name = r.GetName(i);
									var val = r.IsDBNull(i) ? null : r.GetValue(i);
									tableData[name] = val;
								}
								data[t] = tableData;
								break;
							}
						}
					}
				}
			}
			var configPath = Path.Combine(metaDir, "config.json");
			WriteJson(configPath, data);
		}

		private static void WriteJson<T>(string path, T data)
		{
			var json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
			File.WriteAllText(path, json);
		}
	}
}
