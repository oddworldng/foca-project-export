using System;
using System.ComponentModel;
using System.Windows.Forms;
using Foca.ExportImport.Models;
using Foca.ExportImport.Services;

namespace Foca.ExportImport.UI
{
	public partial class ExportForm : Form
	{
		private readonly BackgroundWorker worker;
		private ExportService exportService;
		private string exportPath;

		public ExportForm()
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

				// Inicializar servicios con la cadena de conexión del contexto
				exportService = new ExportService(
					new DatabaseService(FocaContext.Current.GetConnectionString()),
					new ZipAndHashService());

				using (var sfd = new SaveFileDialog { Filter = "FOCA export (*.foca)|*.foca", Title = "Guardar proyecto como .foca", OverwritePrompt = true })
				{
					var projectName = FocaContext.Current.GetActiveProjectName();
					sfd.FileName = string.IsNullOrWhiteSpace(projectName) ? "export.foca" : projectName + ".foca";
					if (sfd.ShowDialog(this) != DialogResult.OK) return;
					exportPath = sfd.FileName;
				}

				progressBar.Value = 0;
				progressStep.Value = 0;
				lblStatus.Text = "Iniciando exportación...";
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

			// Subpasos y mensajes detallados
			Action<int, string> step = (p, m) => bw.ReportProgress(p, m);

			var projectId = FocaContext.Current.GetActiveProjectId();
			var projectName = FocaContext.Current.GetActiveProjectName();
			var evidence = FocaContext.Current.GetEvidenceRootFolder();

			if (bw.CancellationPending) { e.Cancel = true; return; }
			step(3, "Preparando estructura temporal");

			// Ejecutar exportación real
			exportService.ExportProject(
				projectId,
				projectName,
				evidence,
				exportPath,
				progress,
				TableExportFormat.Jsonl,
				includeBinaries: true);
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
				if (msg.StartsWith("Exportada tabla"))
				{
					var p = progressStep.Value + 5; if (p > 100) p = 100; progressStep.Value = p;
				}
				if (msg.StartsWith("Copiados"))
				{
					progressStep.Value = Math.Min(100, progressStep.Value + 1);
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
