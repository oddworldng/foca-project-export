using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using Foca.ExportImport.Models;

namespace Foca.ExportImport.Services
{
	public sealed class ImportService
	{
		private readonly DatabaseService databaseService;
		private readonly ZipAndHashService zipAndHashService;
		private readonly SchemaIntrospectionService schemaService = new SchemaIntrospectionService();

		public ImportService(DatabaseService db, ZipAndHashService zip)
		{
			databaseService = db;
			zipAndHashService = zip;
		}

		public void ImportProject(string sourceFocaPath, string destinationEvidenceFolder, bool overwrite, IProgress<(int percent, string status)> progress)
		{
			string tempRoot = Path.Combine(Path.GetTempPath(), "foca_import_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(tempRoot);
			try
			{
				progress.Report((5, "Extrayendo fichero .foca (ZIP)"));
				zipAndHashService.ExtractZipToFolder(sourceFocaPath, tempRoot);

				progress.Report((10, "Leyendo manifest"));
				var manifestPath = Path.Combine(tempRoot, "manifest.json");
				if (!File.Exists(manifestPath)) throw new IOException("manifest.json no encontrado");
				var manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<Manifest>(File.ReadAllText(manifestPath));

				ValidateManifest(manifest);

				var tablesDir = Path.Combine(tempRoot, "db", "tables");
				progress.Report((20, "Preparando importación por dependencias"));

				using (var conn = databaseService.OpenConnection())
				{
					var topo = schemaService.GetTopologicalOrderProjectTables(conn);
					if (overwrite)
					{
						progress.Report((22, "Borrando datos previos del proyecto (overwrite)"));
						using (var tx = conn.BeginTransaction())
						{
							try
							{
								for (int i = topo.Count - 1; i >= 0; i--)
								{
									var t = topo[i];
									if (!schemaService.HasProjectId(conn, t)) continue;
									using (var del = new SqlCommand($"DELETE FROM [" + t + "] WHERE ProjectId = @pid", conn, tx))
									{
										del.Parameters.AddWithValue("@pid", manifest.project_id);
										del.ExecuteNonQuery();
									}
								}
								tx.Commit();
							}
							catch
							{
								tx.Rollback();
								throw;
							}
						}
					}

					progress.Report((30, "Importando tablas por orden topológico"));
					int processed = 0;
					foreach (var table in topo)
					{
						var ext = File.Exists(Path.Combine(tablesDir, table + ".jsonl")) ? ".jsonl" : File.Exists(Path.Combine(tablesDir, table + ".csv")) ? ".csv" : null;
						if (ext == null) { processed++; continue; }
						var columns = schemaService.GetColumns(conn, table);
						var key = schemaService.ChooseMergeKey(conn, table, columns);
						InsertData(conn, null, table, columns, Path.Combine(tablesDir, table + ext), ext, key);
						processed++;
						int pct = 30 + (int)((double)processed / Math.Max(1, topo.Count) * 40);
						progress.Report((pct, $"Importada tabla {table} ({processed}/{topo.Count})"));
					}
				}

				progress.Report((70, "Restaurando ficheros"));
				var filesDir = Path.Combine(tempRoot, "files");
				RestoreFiles(filesDir, Path.Combine(tempRoot, "meta", "files.jsonl"), destinationEvidenceFolder, progress);

				progress.Report((100, "Importación completada"));
			}
			finally
			{
				try { Directory.Delete(tempRoot, true); } catch { }
			}
		}

		private void ValidateManifest(Manifest manifest)
		{
			if (manifest == null) throw new InvalidDataException("Manifest inválido");
			if (string.IsNullOrWhiteSpace(manifest.foca_export_version)) throw new InvalidDataException("Versión de export desconocida");
			if (!string.Equals(manifest.hash_algorithm, "SHA256", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Hash no soportado");
		}

		private void ImportTables(string tablesDir, Manifest manifest, bool overwrite, IProgress<(int, string)> progress)
		{
			if (!Directory.Exists(tablesDir)) return;
			var files = Directory.GetFiles(tablesDir, "*.*", SearchOption.TopDirectoryOnly);
			using (var conn = databaseService.OpenConnection())
			using (var tx = conn.BeginTransaction())
			{
				try
				{
					int processed = 0;
					foreach (var file in files)
					{
						var tableName = Path.GetFileNameWithoutExtension(file);
						var ext = Path.GetExtension(file).ToLowerInvariant();
						var columns = ReadHeaderOrInferColumns(conn, tx, tableName, ext);
						if (overwrite)
						{
							if (columns.Contains("ProjectId"))
							{
								using (var del = new SqlCommand($"DELETE FROM [" + tableName + "] WHERE ProjectId = @pid", conn, tx))
								{
									del.Parameters.AddWithValue("@pid", manifest.project_id);
									del.ExecuteNonQuery();
								}
							}
						}

						var key = MergeKeyResolver.GetNaturalKey(tableName, columns);
						InsertData(conn, tx, tableName, columns, file, ext, key);

						processed++;
						int pct = 20 + (int)((double)processed / Math.Max(1, files.Length) * 50);
						progress.Report((pct, $"Importada tabla {tableName} ({processed}/{files.Length})"));
					}
					tx.Commit();
				}
				catch
				{
					tx.Rollback();
					throw;
				}
			}
		}

		private List<string> ReadHeaderOrInferColumns(SqlConnection conn, SqlTransaction tx, string tableName, string ext)
		{
			var columns = new List<string>();
			using (var cmd = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t ORDER BY ORDINAL_POSITION", conn, tx))
			{
				cmd.Parameters.AddWithValue("@t", tableName);
				using (var r = cmd.ExecuteReader())
				{
					while (r.Read()) columns.Add(r.GetString(0));
				}
			}
			return columns;
		}

		private void InsertData(SqlConnection conn, SqlTransaction tx, string tableName, List<string> columns, string filePath, string ext, IReadOnlyList<string> naturalKey)
		{
			const int BatchSize = 1000;
			var buffer = new List<Dictionary<string, object>>(BatchSize);
			Action flush = () =>
			{
				if (buffer.Count == 0) return;
				BulkUpsert(conn, tx, tableName, columns, buffer, naturalKey);
				buffer.Clear();
			};

			using (var sr = new StreamReader(filePath))
			{
				if (ext == ".csv")
				{
					// leer header
					var header = sr.ReadLine();
					string line;
					while ((line = sr.ReadLine()) != null)
					{
						var values = ParseCsvLine(line, columns.Count);
						buffer.Add(Map(columns, values));
						if (buffer.Count >= BatchSize) flush();
					}
				}
				else
				{
					string line;
					while ((line = sr.ReadLine()) != null)
					{
						var dict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(line);
						buffer.Add(dict);
						if (buffer.Count >= BatchSize) flush();
					}
				}
			}
			flush();
		}

		private void BulkUpsert(SqlConnection conn, SqlTransaction tx, string tableName, List<string> columns, List<Dictionary<string, object>> rows, IReadOnlyList<string> naturalKey)
		{
			if (naturalKey == null)
			{
				foreach (var row in rows)
				{
					InsertSingle(conn, tx, tableName, columns, row);
				}
				return;
			}

			foreach (var row in rows)
			{
				if (!ExistsByKey(conn, tx, tableName, naturalKey, row))
				{
					InsertSingle(conn, tx, tableName, columns, row);
				}
			}
		}

		private bool ExistsByKey(SqlConnection conn, SqlTransaction tx, string tableName, IReadOnlyList<string> keyCols, Dictionary<string, object> row)
		{
			using (var cmd = new SqlCommand($"SELECT 1 FROM [" + tableName + "] WHERE " + string.Join(" AND ", BuildEquals(keyCols)), conn, tx))
			{
				foreach (var c in keyCols)
				{
					cmd.Parameters.AddWithValue("@" + c, row.ContainsKey(c) ? (row[c] ?? (object)DBNull.Value) : DBNull.Value);
				}
				var o = cmd.ExecuteScalar();
				return o != null;
			}
		}

		private void InsertSingle(SqlConnection conn, SqlTransaction tx, string tableName, List<string> columns, Dictionary<string, object> row)
		{
			using (var cmd = new SqlCommand($"INSERT INTO [" + tableName + "] (" + string.Join(",", columns) + ") VALUES (" + string.Join(",", BuildParams(columns)) + ")", conn, tx))
			{
				foreach (var c in columns)
				{
					object v = row.ContainsKey(c) ? row[c] : DBNull.Value;
					cmd.Parameters.AddWithValue("@" + c, v ?? (object)DBNull.Value);
				}
				cmd.ExecuteNonQuery();
			}
		}

		private IEnumerable<string> BuildParams(List<string> columns)
		{
			foreach (var c in columns) yield return "@" + c;
		}

		private IEnumerable<string> BuildEquals(IReadOnlyList<string> columns)
		{
			foreach (var c in columns) yield return "[" + c + "] = @" + c;
		}

		private Dictionary<string, object> Map(List<string> columns, List<string> values)
		{
			var d = new Dictionary<string, object>(columns.Count);
			for (int i = 0; i < columns.Count; i++) d[columns[i]] = (object)values[i] ?? DBNull.Value;
			return d;
		}

		private List<string> ParseCsvLine(string line, int expected)
		{
			var res = new List<string>(expected);
			bool inQ = false; var sb = new System.Text.StringBuilder();
			for (int i = 0; i < line.Length; i++)
			{
				char ch = line[i];
				if (inQ)
				{
					if (ch == '"')
					{
						if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
						else { inQ = false; }
					}
					else { sb.Append(ch); }
				}
				else
				{
					if (ch == ',') { res.Add(sb.ToString()); sb.Clear(); }
					else if (ch == '"') { inQ = true; }
					else { sb.Append(ch); }
				}
			}
			res.Add(sb.ToString());
			while (res.Count < expected) res.Add(string.Empty);
			return res;
		}

		private void RestoreFiles(string sourceFilesDir, string filesIndexPath, string destinationEvidenceFolder, IProgress<(int, string)> progress)
		{
			if (!Directory.Exists(sourceFilesDir)) return;
			Directory.CreateDirectory(destinationEvidenceFolder);
			string[] indexedLines = File.Exists(filesIndexPath) ? File.ReadAllLines(filesIndexPath) : new string[0];
			var expected = new List<(string src, string sha, string originalRel, string fileName)>();
			foreach (var line in indexedLines)
			{
				var rec = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(line);
				if (rec == null) continue;
				string path = rec.ContainsKey("path") ? Convert.ToString(rec["path"]) : null;
				string sha = rec.ContainsKey("sha256") ? Convert.ToString(rec["sha256"]) : null;
				string orr = rec.ContainsKey("original_rel") ? Convert.ToString(rec["original_rel"]) : null;
				string fn = rec.ContainsKey("file_name") ? Convert.ToString(rec["file_name"]) : null;
				if (path == null || sha == null) continue;
				expected.Add((path.Replace('/', Path.DirectorySeparatorChar), sha, orr, fn));
			}

			for (int i = 0; i < expected.Count; i++)
			{
				var item = expected[i];
				var src = Path.Combine(sourceFilesDir, new string(item.src.SkipWhile(c => c == Path.DirectorySeparatorChar).ToArray()));
				var dest = string.IsNullOrWhiteSpace(item.originalRel)
					? Path.Combine(destinationEvidenceFolder, string.IsNullOrWhiteSpace(item.fileName) ? Path.GetFileName(src) : item.fileName)
					: Path.GetFullPath(Path.Combine(destinationEvidenceFolder, item.originalRel.Replace('/', Path.DirectorySeparatorChar)));
				Directory.CreateDirectory(Path.GetDirectoryName(dest));
				File.Copy(src, dest, overwrite: true);
				var actual = zipAndHashService.ComputeSha256(dest);
				if (!string.Equals(item.sha, actual, StringComparison.OrdinalIgnoreCase))
					throw new IOException("Hash de fichero no coincide: " + dest);
				if (i % 25 == 0)
				{
					int pct = 70 + (int)((double)i / Math.Max(1, expected.Count) * 25);
					progress.Report((pct, $"Restaurados {i}/{expected.Count} ficheros del proyecto"));
				}
			}
		}
	}
}
