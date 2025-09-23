using System;
using System.Windows.Forms;

namespace Foca.ExportImport
{
	public interface IFocaPlugin
	{
		string Name { get; }
		string Description { get; }
		string Author { get; }
		string Version { get; }
		void Initialize();
	}

	public sealed class FocaExportImportPlugin : IFocaPlugin
	{
		public string Name => "Export/Import .foca";
		public string Description => "Exporta e importa proyectos FOCA a/desde .foca";
		public string Author => "Andrés Nacimiento";
		public string Version => "1.0.0";

		public void Initialize()
		{
			// En runtime con FOCA usar FocaExportImportPluginApi (FOCA_API) para registrar menús.
			Application.ApplicationExit += (s, e) => { };
		}

		public void OnExport()
		{
			using (var form = new UI.ExportForm())
			{
				form.ShowDialog();
			}
		}

		public void OnImport()
		{
			using (var form = new UI.ImportForm())
			{
				form.ShowDialog();
			}
		}
	}
}
