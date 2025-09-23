using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Newtonsoft.Json;

namespace Foca.ExportImport.Services
{
	public enum TableExportFormat
	{
		Csv,
		Jsonl
	}

	public interface ITableWriter : IDisposable
	{
		void WriteHeader(IReadOnlyList<string> columnNames);
		void WriteRow(object[] values);
	}

	public static class TableWriterFactory
	{
		public static ITableWriter Create(TableExportFormat format, string filePath, IReadOnlyList<string> columnNames)
		{
			switch (format)
			{
				case TableExportFormat.Csv:
					return new CsvTableWriter(filePath, columnNames);
				case TableExportFormat.Jsonl:
					return new JsonlTableWriter(filePath, columnNames);
				default:
					throw new NotSupportedException();
			}
		}
	}

	internal sealed class CsvTableWriter : ITableWriter
	{
		private readonly StreamWriter writer;
		private readonly IReadOnlyList<string> columns;

		public CsvTableWriter(string filePath, IReadOnlyList<string> columnNames)
		{
			// UTF-8 with BOM
			writer = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			columns = columnNames;
			WriteHeader(columnNames);
		}

		public void WriteHeader(IReadOnlyList<string> columnNames)
		{
			WriteCsvLine(columnNames);
		}

		public void WriteRow(object[] values)
		{
			var stringValues = new string[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				stringValues[i] = Format(values[i]);
			}
			WriteCsvLine(stringValues);
		}

		private void WriteCsvLine(IReadOnlyList<string> fields)
		{
			for (int i = 0; i < fields.Count; i++)
			{
				if (i > 0) writer.Write(',');
				writer.Write(Escape(fields[i] ?? string.Empty));
			}
			writer.WriteLine();
		}

		private static string Escape(string value)
		{
			bool mustQuote = value.Contains("\"") || value.Contains(",") || value.Contains("\n") || value.Contains("\r");
			if (mustQuote)
			{
				return '"' + value.Replace("\"", "\"\"") + '"';
			}
			return value;
		}

		private static string Format(object value)
		{
			if (value == null || value is DBNull) return string.Empty;
			if (value is DateTime dt) return dt.ToString("o");
			return Convert.ToString(value);
		}

		public void Dispose()
		{
			writer?.Dispose();
		}
	}

	internal sealed class JsonlTableWriter : ITableWriter
	{
		private readonly StreamWriter writer;
		private readonly IReadOnlyList<string> columns;

		public JsonlTableWriter(string filePath, IReadOnlyList<string> columnNames)
		{
			writer = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			columns = columnNames;
		}

		public void WriteHeader(IReadOnlyList<string> columnNames)
		{
			// No header in JSONL
		}

		public void WriteRow(object[] values)
		{
			var dict = new Dictionary<string, object>(columns.Count);
			for (int i = 0; i < columns.Count; i++)
			{
				dict[columns[i]] = values[i];
			}
			var json = JsonConvert.SerializeObject(dict, Formatting.None);
			writer.WriteLine(json);
		}

		public void Dispose()
		{
			writer?.Dispose();
		}
	}
}
