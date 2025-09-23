using System;
using System.Collections.Generic;

namespace Foca.ExportImport.Services
{
	public static class MergeKeyResolver
	{
		// Devuelve lista de columnas que forman la clave natural para merge; null si desconocida
		public static IReadOnlyList<string> GetNaturalKey(string tableName, IReadOnlyList<string> columnNames)
		{
			var name = tableName?.ToLowerInvariant() ?? string.Empty;
			var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var c in columnNames) set.Add(c);

			if (set.Contains("ProjectId") && set.Contains("SourceUrl") && set.Contains("Sha256") && name.Contains("document"))
				return new[] { "ProjectId", "SourceUrl", "Sha256" };

			if (set.Contains("ProjectId") && set.Contains("Sha256"))
				return new[] { "ProjectId", "Sha256" };

			if (set.Contains("ProjectId") && set.Contains("Email"))
				return new[] { "ProjectId", "Email" };

			if (set.Contains("ProjectId") && set.Contains("Path"))
				return new[] { "ProjectId", "Path" };

			if (set.Contains("ProjectId") && (set.Contains("Domain") || set.Contains("Host") || set.Contains("Ip")))
				return new[] { "ProjectId", set.Contains("Domain") ? "Domain" : set.Contains("Host") ? "Host" : "Ip" };

			if (set.Contains("ProjectId") && set.Contains("Name"))
				return new[] { "ProjectId", "Name" };

			return null;
		}
	}
}
