using System;
using System.ComponentModel;
using System.Windows.Forms;
using Foca.ExportImport.Services;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using Foca.ExportImport.Models;

namespace Foca.ExportImport.UI
{
	public partial class ImportForm : Form
	{
		private readonly BackgroundWorker worker;
		private ImportService importService;
		private string destinationRoot;
		private string selectedFocaPath;

		public ImportForm()
		{
			InitializeComponent();

			// Servicios se inicializan al pulsar Start; si no hay contexto, intentamos autoconfigurar desde FOCA.exe.config

			worker = new BackgroundWorker
			{
				WorkerReportsProgress = true,
				WorkerSupportsCancellation = true
			};
			worker.DoWork += Worker_DoWork;
			worker.ProgressChanged += Worker_ProgressChanged;
			worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
		}

		private void btnStart_Click(object sender, EventArgs e)
		{
			if (!worker.IsBusy)
			{
				// Validar/auto-configurar contexto
				IFocaContext ctx = null;
				try { ctx = FocaContext.Current; }
				catch { }
				if (ctx == null)
				{
					try { FocaContext.Configure(new AutoFocaContext()); ctx = FocaContext.Current; }
					catch (Exception ex)
					{
						MessageBox.Show(this, "No hay proyecto activo de FOCA o el contexto no está configurado. Abre un proyecto en FOCA e inténtalo de nuevo.\r\n\r\n" + ex.Message, "FOCA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						return;
					}
				}

				// Si aún no se ha seleccionado .foca, pedirlo y pre-rellenar datos
				if (string.IsNullOrEmpty(selectedFocaPath))
				{
					if (!ChooseFocaAndPrefill()) return;
					lblStatus.Text = "Archivo cargado. Revisa nombre y carpeta y pulsa Importar de nuevo.";
					return;
				}

				// Validar nombre y carpeta
				if (string.IsNullOrWhiteSpace(txtProjectName.Text))
				{
					MessageBox.Show(this, "Especifica el nombre del proyecto.", "FOCA", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
				if (string.IsNullOrWhiteSpace(txtProjectFolder.Text))
				{
					MessageBox.Show(this, "Especifica la carpeta de evidencias del proyecto.", "FOCA", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
				destinationRoot = txtProjectFolder.Text;

				// Inicializar servicios con la cadena de conexión del contexto
				importService = new ImportService(
					new DatabaseService(FocaContext.Current.GetConnectionString()),
					new ZipAndHashService());

				progressBar.Value = 0;
				progressStep.Value = 0;
				lblStatus.Text = "Iniciando importación...";
				lblStep.Text = string.Empty;
				txtLog.Clear();
				btnStart.Enabled = false;
				btnCancel.Enabled = true;
				worker.RunWorkerAsync();
			}
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			if (worker.IsBusy)
			{
				worker.CancelAsync();
			}
		}

		private void Worker_DoWork(object sender, DoWorkEventArgs e)
		{
			var bw = (BackgroundWorker)sender;
			var progress = new Progress<(int percent, string status)>(tuple =>
			{
				bw.ReportProgress(tuple.percent, tuple.status);
			});

			Action<int, string> step = (p, m) => bw.ReportProgress(p, m);

			if (string.IsNullOrEmpty(selectedFocaPath)) { e.Cancel = true; return; }

			if (bw.CancellationPending) { e.Cancel = true; return; }
			step(5, "Extrayendo fichero .foca (ZIP)");

			importService.ImportProject(selectedFocaPath, destinationRoot, txtProjectName.Text, overwrite: false, progress);
		}

		private bool ChooseFocaAndPrefill()
		{
			using (var ofd = new OpenFileDialog { Filter = "FOCA export (*.foca)|*.foca", Title = "Selecciona un fichero .foca" })
			{
				if (ofd.ShowDialog(this) != DialogResult.OK) return false;
				selectedFocaPath = ofd.FileName;
				try
				{
					using (var zip = ZipFile.OpenRead(selectedFocaPath))
					{
						var entry = zip.GetEntry("manifest.json");
						if (entry != null)
						{
							using (var sr = new StreamReader(entry.Open()))
							{
								var manifest = JsonConvert.DeserializeObject<Manifest>(sr.ReadToEnd());
								if (manifest != null && !string.IsNullOrWhiteSpace(manifest.project_name))
									txtProjectName.Text = manifest.project_name;
							}
						}
					}
				}
				catch { }
				// Ruta por defecto desde el contexto
				try { var def = FocaContext.Current.GetEvidenceRootFolder(); if (!string.IsNullOrWhiteSpace(def)) txtProjectFolder.Text = def; } catch { }
				return true;
			}
		}

		private void btnBrowseFolder_Click(object sender, EventArgs e)
		{
			using (var fbd = new FolderBrowserDialog { Description = "Selecciona la carpeta raíz de evidencias destino del proyecto" })
			{
				if (!string.IsNullOrWhiteSpace(txtProjectFolder.Text)) fbd.SelectedPath = txtProjectFolder.Text;
				else { try { var def = FocaContext.Current.GetEvidenceRootFolder(); if (!string.IsNullOrWhiteSpace(def)) fbd.SelectedPath = def; } catch { } }
				if (fbd.ShowDialog(this) == DialogResult.OK)
				{
					txtProjectFolder.Text = fbd.SelectedPath;
				}
			}
		}

		private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			progressBar.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
			var msg = e.UserState as string ?? string.Empty;
			lblStatus.Text = msg;
			lblStep.Text = msg;
			if (!string.IsNullOrEmpty(msg))
			{
				txtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\r\n");
				if (msg.StartsWith("Importada tabla") || msg.StartsWith("Restaurados"))
				{
					var p = progressStep.Value + 2; if (p > 100) p = 100; progressStep.Value = p;
				}
			}
		}

		private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			btnStart.Enabled = true;
			btnCancel.Enabled = false;
			if (e.Error != null)
			{
				MessageBox.Show(this, e.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			else if (e.Cancelled)
			{
				lblStatus.Text = "Cancelado";
			}
			else
			{
				lblStatus.Text = "Completado";
				progressStep.Value = 100;
			}
		}
	}
}
