using System;

namespace Foca.ExportImport.Services
{
	public interface IFocaContext
	{
		int GetActiveProjectId();
		string GetActiveProjectName();
		string GetConnectionString();
		string GetEvidenceRootFolder();
	}

	public static class FocaContext
	{
		private static IFocaContext current;
		public static void Configure(IFocaContext context) { current = context; }
		public static IFocaContext Current
		{
			get
			{
				if (current == null) throw new InvalidOperationException("FocaContext no configurado. Debe inyectarse desde FOCA.");
				return current;
			}
		}
	}
}
