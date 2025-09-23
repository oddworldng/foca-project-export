using System;

namespace Foca.ExportImport.Models
{
	public sealed class Manifest
	{
		public string foca_export_version { get; set; }
		public string foca_app_version { get; set; }
		public DateTime created_utc { get; set; }
		public string project_id { get; set; }
		public string project_name { get; set; }
		public string db_provider { get; set; }
		public string db_version { get; set; }
		public string[] tables { get; set; }
		public int file_count { get; set; }
		public string hash_algorithm { get; set; }

		// Campos informativos del autor del plugin
		public string author { get; set; }
		public string author_website { get; set; }
		public string author_email { get; set; }
	}
}
