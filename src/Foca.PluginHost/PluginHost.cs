using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PluginsAPI;
using PluginsAPI.Elements;
using Foca.ExportImport.UI;

namespace Foca
{
	public class Plugin
	{
		private string _name = "Export Import Project";
		private string _description = "Exporta e importa proyectos FOCA a y desde archivos foca";
		private readonly Export export;

		public Export exportItems { get { return this.export; } }
		public string name { get { return this._name; } set { this._name = value; } }
		public string description { get { return this._description; } set { this._description = value; } }

		public Plugin()
		{
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
				using (var form = new ExportForm())
				{
					form.ShowDialog();
				}
			};
			var importItem = new ToolStripMenuItem("Import Project (foca)");
			importItem.Click += (s, e) =>
			{
				using (var form = new ImportForm())
				{
					form.ShowDialog();
				}
			};
			root.DropDownItems.Add(exportItem);
			root.DropDownItems.Add(importItem);
			this.export.Add(new PluginToolStripMenuItem(root));
		}
	}
}
