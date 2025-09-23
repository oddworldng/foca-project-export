#if FOCA_API
using System;
using System.Windows.Forms;
using FOCA.PluginsAPI; // Namespace indicativo; ajustar al real del repositorio de FOCA
using Foca.ExportImport.UI;
using Foca.ExportImport.Services;

namespace Foca.ExportImport
{
	public sealed class FocaExportImportPluginApi : IPlugin // Ajustar a la interfaz real del ejemplo
	{
		public string Name => "Export/Import .foca";
		public string Description => "Exporta e importa proyectos FOCA a/desde .foca";
		public string Author => "Andrés Nacimiento";
		public string Version => "1.0.0";

		public void Initialize(IHostContext context)
		{
			// Registrar menús en Project y Plugins según API real
			context.MenuRegistrar.AddProjectExportMenu("Export Project as .foca…", OnExport);
			context.MenuRegistrar.AddProjectImportMenu("Import Project from .foca…", OnImport);
			context.MenuRegistrar.AddPluginMenu("Export/Import .foca", OnExport, OnImport);

			// Configurar contexto global
			FocaContext.Configure(new HostContextAdapter(context));
		}

		private void OnExport(object sender, EventArgs args)
		{
			using (var f = new ExportForm()) f.ShowDialog();
		}

		private void OnImport(object sender, EventArgs args)
		{
			using (var f = new ImportForm()) f.ShowDialog();
		}
	}

	internal sealed class HostContextAdapter : IFocaContext
	{
		private readonly IHostContext host;
		public HostContextAdapter(IHostContext host) { this.host = host; }
		public Guid GetActiveProjectId() => host.ProjectService.ActiveProjectId;
		public string GetActiveProjectName() => host.ProjectService.ActiveProjectName;
		public string GetConnectionString() => host.DatabaseService.ConnectionString;
		public string GetEvidenceRootFolder() => host.FileService.GetEvidenceRootForProject(host.ProjectService.ActiveProjectId);
	}
}
#endif
