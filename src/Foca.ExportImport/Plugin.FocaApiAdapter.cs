#if FOCA_API
using System;
using System.IO;
using System.Windows.Forms;
using PluginsAPI;
using PluginsAPI.Elements;

namespace Foca
{
	internal static class PluginDiag
	{
		private static readonly string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocaExportImport.plugin.log");
		public static void Log(string message)
		{
			try { File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message + Environment.NewLine); } catch { }
		}
	}

	public class Plugin
	{
		private string _name = "Export Import Project";
		private string _description = "Exporta e importa proyectos FOCA a y desde archivos foca";
		private readonly Export export;

		public Export exportItems { get { return this.export; } }

		public string name
		{
			get { return this._name; }
			set { this._name = value; }
		}

		public string description
		{
			get { return this._description; }
			set { this._description = value; }
		}

		public Plugin()
		{
			try
			{
				PluginDiag.Log("Plugin ctor start");
				this.export = new Export();

				var hostPanel = new Panel { Dock = DockStyle.Fill, Visible = false };
				var pluginPanel = new PluginPanel(hostPanel, false);
				this.export.Add(pluginPanel);
				PluginDiag.Log("PluginPanel added");

				var root = new ToolStripMenuItem(this._name);
				var exportItem = new ToolStripMenuItem("Export Project (foca)");
				exportItem.Click += (EventHandler)((s, e) => { MessageBox.Show("Export placeholder cargado correctamente."); });
				var importItem = new ToolStripMenuItem("Import Project (foca)");
				importItem.Click += (EventHandler)((s, e) => { MessageBox.Show("Import placeholder cargado correctamente."); });
				root.DropDownItems.Add(exportItem);
				root.DropDownItems.Add(importItem);

				var pluginMenu = new PluginToolStripMenuItem(root);
				this.export.Add(pluginMenu);
				PluginDiag.Log("Menu added");
			}
			catch (Exception ex)
			{
				PluginDiag.Log("Plugin ctor error: " + ex);
				throw;
			}
		}
	}
}
#endif
