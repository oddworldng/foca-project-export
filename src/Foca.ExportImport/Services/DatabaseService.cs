using System.Data.SqlClient;

namespace Foca.ExportImport.Services
{
	public sealed class DatabaseService
	{
		private readonly string connectionString;

		public DatabaseService(string connectionString)
		{
			this.connectionString = connectionString;
		}

		public SqlConnection OpenConnection()
		{
			var conn = new SqlConnection(connectionString);
			conn.Open();
			return conn;
		}
	}
}
