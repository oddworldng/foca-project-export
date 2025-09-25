using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Foca.ExportImport.Services
{
    // Intenta deducir el contexto activo leyendo FOCA.exe.config y consultando la BD
    public sealed class AutoFocaContext : IFocaContext
    {
        private readonly string connectionString;
        private Guid? cachedProjectId;
        private string cachedProjectName;
        private string cachedEvidenceRoot;

        public AutoFocaContext()
        {
            connectionString = ResolveConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("No se pudo resolver la cadena de conexión desde el config de FOCA.");
            }
        }

        public Guid GetActiveProjectId()
        {
            if (cachedProjectId.HasValue) return cachedProjectId.Value;
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var table = PickProjectTable(conn);
                var idCol = ColumnIfExists(conn, table, "Id") ?? ColumnIfExists(conn, table, table + "Id") ?? "Id";
                var orderCol = ColumnIfExists(conn, table, "LastOpened") ?? ColumnIfExists(conn, table, "LastOpenDate") ?? ColumnIfExists(conn, table, "UpdatedAt") ?? idCol;
                var sql = "SELECT TOP 1 [" + idCol + "] FROM [" + table + "] ORDER BY [" + orderCol + "] DESC";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    var obj = cmd.ExecuteScalar();
                    if (obj == null) throw new InvalidOperationException("No se encontró proyecto en la BD de FOCA.");
                    cachedProjectId = (obj is Guid g) ? g : Guid.Parse(obj.ToString());
                }
            }
            return cachedProjectId.Value;
        }

        public string GetActiveProjectName()
        {
            if (!string.IsNullOrEmpty(cachedProjectName)) return cachedProjectName;
            var pid = GetActiveProjectId();
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var table = PickProjectTable(conn);
                var idCol = ColumnIfExists(conn, table, "Id") ?? ColumnIfExists(conn, table, table + "Id") ?? "Id";
                var nameCol = ColumnIfExists(conn, table, "Name") ?? ColumnIfExists(conn, table, "ProjectName") ?? "Name";
                var sql = "SELECT [" + nameCol + "] FROM [" + table + "] WHERE [" + idCol + "] = @id";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", pid);
                    var obj = cmd.ExecuteScalar();
                    cachedProjectName = obj?.ToString();
                }
            }
            return cachedProjectName;
        }

        public string GetConnectionString()
        {
            return connectionString;
        }

        public string GetEvidenceRootFolder()
        {
            if (!string.IsNullOrEmpty(cachedEvidenceRoot)) return cachedEvidenceRoot;
            try
            {
                var pid = GetActiveProjectId();
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var candidates = new[] { "Documents", "Files", "Evidence", "DownloadedFiles", "DocumentFiles" };
                    foreach (var table in candidates)
                    {
                        if (!TableExists(conn, table)) continue;
                        var col = ColumnIfAny(conn, table, new[] { "RelativePath", "LocalPath", "FilePath", "Path", "FullPath", "Ruta" });
                        var pidCol = ColumnIfAny(conn, table, new[] { "ProjectId", "IdProject", "ProyectoId" });
                        if (col == null || pidCol == null) continue;
                        var paths = new List<string>();
                        using (var cmd = new SqlCommand($"SELECT TOP 50 [{col}] FROM [" + table + "] WHERE [" + pidCol + "] = @id AND [" + col + "] IS NOT NULL", conn))
                        {
                            cmd.Parameters.AddWithValue("@id", pid);
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read()) { var v = r.GetString(0); if (!string.IsNullOrWhiteSpace(v)) paths.Add(v); }
                            }
                        }
                        if (paths.Count > 0)
                        {
                            cachedEvidenceRoot = CommonPrefixDirectory(paths);
                            if (!string.IsNullOrEmpty(cachedEvidenceRoot)) return cachedEvidenceRoot;
                        }
                    }
                }
            }
            catch { }
            cachedEvidenceRoot = AppDomain.CurrentDomain.BaseDirectory;
            return cachedEvidenceRoot;
        }

        private static string ResolveConnectionString()
        {
            foreach (ConnectionStringSettings cs in ConfigurationManager.ConnectionStrings)
            {
                if (cs == null) continue;
                var s = cs.ConnectionString ?? string.Empty;
                var provider = (cs.ProviderName ?? string.Empty).ToLowerInvariant();
                if (provider.Contains("sqlclient") || s.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var key = "provider connection string=";
                    var idx = s.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        var rest = s.Substring(idx + key.Length).Trim().Trim('"');
                        return rest;
                    }
                    return s;
                }
            }
            return string.Empty;
        }

        private static string PickProjectTable(SqlConnection conn)
        {
            var candidates = new[] { "Projects", "Project", "FOCAProjects", "Proyecto", "Proyectos" };
            foreach (var c in candidates) if (TableExists(conn, c)) return c;
            using (var cmd = new SqlCommand("SELECT TOP 1 TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' AND TABLE_NAME LIKE '%Project%';", conn))
            {
                var o = cmd.ExecuteScalar();
                return o?.ToString() ?? "Projects";
            }
        }

        private static bool TableExists(SqlConnection conn, string table)
        {
            using (var cmd = new SqlCommand("SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' AND TABLE_NAME=@t", conn))
            {
                cmd.Parameters.AddWithValue("@t", table);
                var o = cmd.ExecuteScalar();
                return o != null;
            }
        }

        private static string ColumnIfExists(SqlConnection conn, string table, string col)
        {
            using (var cmd = new SqlCommand("SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=@t AND COLUMN_NAME=@c", conn))
            {
                cmd.Parameters.AddWithValue("@t", table);
                cmd.Parameters.AddWithValue("@c", col);
                var o = cmd.ExecuteScalar();
                return o != null ? col : null;
            }
        }

        private static string ColumnIfAny(SqlConnection conn, string table, string[] cols)
        {
            foreach (var c in cols)
            {
                var r = ColumnIfExists(conn, table, c);
                if (r != null) return r;
            }
            return null;
        }

        private static string CommonPrefixDirectory(List<string> paths)
        {
            if (paths == null || paths.Count == 0) return null;
            var split = paths.Select(p => (p ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)).ToArray();
            string prefix = split[0];
            foreach (var s in split.Skip(1))
            {
                int len = Math.Min(prefix.Length, s.Length);
                int i = 0;
                for (; i < len; i++) if (char.ToUpperInvariant(prefix[i]) != char.ToUpperInvariant(s[i])) break;
                prefix = prefix.Substring(0, i);
                if (string.IsNullOrEmpty(prefix)) break;
            }
            if (string.IsNullOrEmpty(prefix)) return null;
            var dir = prefix;
            try { if (!Directory.Exists(dir)) dir = Path.GetDirectoryName(dir); } catch { }
            return dir;
        }
    }
}


