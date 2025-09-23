using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace Foca.ExportImport.Services
{
	public sealed class SchemaIntrospectionService
	{
		public List<string> GetColumns(SqlConnection conn, string table)
		{
			var cols = new List<string>();
			using (var cmd = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t ORDER BY ORDINAL_POSITION", conn))
			{
				cmd.Parameters.AddWithValue("@t", table);
				using (var r = cmd.ExecuteReader())
				{
					while (r.Read()) cols.Add(r.GetString(0));
				}
			}
			return cols;
		}

		public List<string[]> GetPrimaryKey(SqlConnection conn, string table)
		{
			var keys = new List<string[]>();
			using (var cmd = new SqlCommand(@"
SELECT i.name, c.name
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
JOIN sys.tables t ON t.object_id = i.object_id
WHERE i.is_primary_key = 1 AND t.name = @t
ORDER BY ic.key_ordinal", conn))
			{
				cmd.Parameters.AddWithValue("@t", table);
				using (var r = cmd.ExecuteReader())
				{
					var list = new List<string>();
					string current = null;
					while (r.Read())
					{
						string idx = r.GetString(0);
						string col = r.GetString(1);
						if (current == null) current = idx;
						if (current != idx && list.Count > 0)
						{
							keys.Add(list.ToArray());
							list = new List<string>();
							current = idx;
						}
						list.Add(col);
					}
					if (list.Count > 0) keys.Add(list.ToArray());
				}
			}
			return keys;
		}

		public List<string[]> GetUniqueIndexes(SqlConnection conn, string table)
		{
			var uks = new List<string[]>();
			using (var cmd = new SqlCommand(@"
SELECT i.name, c.name
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
JOIN sys.tables t ON t.object_id = i.object_id
WHERE i.is_unique = 1 AND t.name = @t
ORDER BY i.index_id, ic.key_ordinal", conn))
			{
				cmd.Parameters.AddWithValue("@t", table);
				using (var r = cmd.ExecuteReader())
				{
					var list = new List<string>();
					string current = null;
					while (r.Read())
					{
						string idx = r.GetString(0);
						string col = r.GetString(1);
						if (current == null) current = idx;
						if (current != idx && list.Count > 0)
						{
							uks.Add(list.ToArray());
							list = new List<string>();
							current = idx;
						}
						list.Add(col);
					}
					if (list.Count > 0) uks.Add(list.ToArray());
				}
			}
			return uks;
		}

		public List<(string, string)> GetForeignKeyEdges(SqlConnection conn)
		{
			var edges = new List<(string, string)>();
			using (var cmd = new SqlCommand(@"
SELECT tr.name AS referencing_table, td.name AS referenced_table
FROM sys.foreign_keys fk
JOIN sys.tables tr ON tr.object_id = fk.parent_object_id
JOIN sys.tables td ON td.object_id = fk.referenced_object_id", conn))
			using (var r = cmd.ExecuteReader())
			{
				while (r.Read()) edges.Add((r.GetString(0), r.GetString(1)));
			}
			return edges;
		}

		public bool HasProjectId(SqlConnection conn, string table)
		{
			using (var cmd = new SqlCommand("SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t AND COLUMN_NAME = 'ProjectId'", conn))
			{
				cmd.Parameters.AddWithValue("@t", table);
				var o = cmd.ExecuteScalar();
				return o != null;
			}
		}

		public List<string> GetTables(SqlConnection conn)
		{
			var list = new List<string>();
			using (var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'", conn))
			using (var r = cmd.ExecuteReader())
			{
				while (r.Read()) list.Add(r.GetString(0));
			}
			return list;
		}

		public List<string> GetProjectScopedTables(SqlConnection conn)
		{
			var res = new List<string>();
			foreach (var t in GetTables(conn)) if (HasProjectId(conn, t)) res.Add(t);
			return res;
		}

		public List<string> GetTopologicalOrderProjectTables(SqlConnection conn)
		{
			var scoped = new HashSet<string>(GetProjectScopedTables(conn), StringComparer.OrdinalIgnoreCase);
			var edgesAll = GetForeignKeyEdges(conn);
			var edges = new List<(string, string)>();
			foreach (var e in edgesAll)
			{
				if (scoped.Contains(e.Item1) && scoped.Contains(e.Item2)) edges.Add(e);
			}
			// Kahn
			var indeg = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (var t in scoped) indeg[t] = 0;
			foreach (var e in edges) indeg[e.Item1] = indeg[e.Item1] + 1;
			var q = new Queue<string>();
			foreach (var kv in indeg) if (kv.Value == 0) q.Enqueue(kv.Key);
			var order = new List<string>();
			while (q.Count > 0)
			{
				var n = q.Dequeue();
				order.Add(n);
				for (int i = edges.Count - 1; i >= 0; i--)
				{
					var e = edges[i];
					if (string.Equals(e.Item2, n, StringComparison.OrdinalIgnoreCase))
					{
						indeg[e.Item1]--;
						edges.RemoveAt(i);
						if (indeg[e.Item1] == 0) q.Enqueue(e.Item1);
					}
				}
			}
			// Añadir restantes si hubiese ciclos
			foreach (var t in scoped) if (!order.Contains(t)) order.Add(t);
			return order;
		}

		public IReadOnlyList<string> ChooseMergeKey(SqlConnection conn, string table, IReadOnlyList<string> columns)
		{
			bool HasAll(string[] set)
			{
				foreach (var c in set) if (!Contains(columns, c)) return false; return true;
			}
			var uks = GetUniqueIndexes(conn, table);
			foreach (var uk in uks)
			{
				if (Array.Exists(uk, c => string.Equals(c, "ProjectId", StringComparison.OrdinalIgnoreCase)) && HasAll(uk))
					return uk;
			}
			var pks = GetPrimaryKey(conn, table);
			foreach (var pk in pks)
			{
				if (Array.Exists(pk, c => string.Equals(c, "ProjectId", StringComparison.OrdinalIgnoreCase)) && HasAll(pk))
					return pk;
			}
			return null;
		}

		private bool Contains(IReadOnlyList<string> cols, string name)
		{
			for (int i = 0; i < cols.Count; i++) if (string.Equals(cols[i], name, StringComparison.OrdinalIgnoreCase)) return true; return false;
		}
	}
}
