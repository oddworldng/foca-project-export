using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using PluginsAPI;
using PluginsAPI.Elements;
using Foca.ExportImport.UI;
using Foca.ExportImport.Services;

namespace Foca
{
	public class Plugin
	{
		private string _name = "Export Import Project";
		private string _description = "Exporta e importa proyectos FOCA a y desde archivos foca";
		private readonly Export export;

		private static bool resolverHooked;

		internal static class PluginDiag
		{
			private static readonly string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocaExportImport.plugin.log");
			public static void Log(string message)
			{
				try { File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message + Environment.NewLine); } catch { }
			}
		}
		private static void EnsureAssemblyResolver()
		{
			if (resolverHooked) return;
			resolverHooked = true;
			AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
			{
				try
				{
					var requestedName = new AssemblyName(args.Name).Name;
					var baseDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory;
					string candidate = null;
					if (string.Equals(requestedName, "Foca.ExportImport.Core", StringComparison.OrdinalIgnoreCase))
					{
						candidate = Path.Combine(baseDir, "Foca.ExportImport.Core.dll");
					}
					else if (string.Equals(requestedName, "Newtonsoft.Json", StringComparison.OrdinalIgnoreCase))
					{
						var jsonPath = Path.Combine(baseDir, "Newtonsoft.Json.dll");
						if (File.Exists(jsonPath)) candidate = jsonPath;
					}
					if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
					{
						return Assembly.LoadFrom(candidate);
					}
				}
				catch { }
				return null;
			};
		}

		// Eliminado adaptador de contexto para no ejecutar lógica durante importación

		public Export exportItems { get { return this.export; } }
		public string name { get { return this._name; } set { this._name = value; } }
		public string description { get { return this._description; } set { this._description = value; } }

		public Plugin()
		{
			try
			{
				PluginDiag.Log("Plugin ctor start");
				EnsureAssemblyResolver();
				this.export = new Export();
				var panel = new PluginPanel(new Panel { Dock = DockStyle.Fill, Visible = false }, false);
				this.export.Add(panel);

				var root = new ToolStripMenuItem(this._name);
				// Buscar el icono relativo al DLL del host
				var baseDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory;
				var iconPath = Path.Combine(baseDir, "img", "icon.png");
				if (File.Exists(iconPath))
				{
					try { root.Image = Image.FromFile(iconPath); } catch { }
				}
				var exportItem = new ToolStripMenuItem("Export Project (foca)");
				exportItem.Click += (s, e) =>
				{
				// El contexto se configurará explícitamente desde FOCA en futuras versiones
					using (var form = new ExportForm())
					{
						form.ShowDialog();
					}
				};
				var importItem = new ToolStripMenuItem("Import Project (foca)");
				importItem.Click += (s, e) =>
				{
				// El contexto se configurará explícitamente desde FOCA en futuras versiones
					using (var form = new ImportForm())
					{
						form.ShowDialog();
					}
				};
				root.DropDownItems.Add(exportItem);
				root.DropDownItems.Add(importItem);
				this.export.Add(new PluginToolStripMenuItem(root));
				PluginDiag.Log("Plugin ctor end ok");
			}
			catch (Exception ex)
			{
				PluginDiag.Log("Plugin ctor error: " + ex);
				throw;
			}
		}
	}
}
