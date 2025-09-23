using System;
using System.ComponentModel;
using System.Windows.Forms;
using Foca.ExportImport.Services;

namespace Foca.ExportImport.UI
{
	public partial class ImportForm : Form
	{
		private readonly BackgroundWorker worker;
		private readonly ImportService importService;
		private string destinationRoot;

		public ImportForm()
		{
			InitializeComponent();

			importService = new ImportService(
				new DatabaseService(FocaContext.Current.GetConnectionString()),
				new ZipAndHashService());

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

			var openFile = SelectFocaFile();
			if (string.IsNullOrEmpty(openFile)) { e.Cancel = true; return; }

			if (!SelectDestinationRoot()) { e.Cancel = true; return; }

			if (bw.CancellationPending) { e.Cancel = true; return; }
			step(5, "Extrayendo fichero .foca (ZIP)");

			importService.ImportProject(openFile, destinationRoot, overwrite: false, progress);
		}

		private string SelectFocaFile()
		{
			using (var ofd = new OpenFileDialog { Filter = "FOCA export (*.foca)|*.foca", Title = "Selecciona un fichero .foca" })
			{
				return ofd.ShowDialog(this) == DialogResult.OK ? ofd.FileName : null;
			}
		}

		private bool SelectDestinationRoot()
		{
			using (var fbd = new FolderBrowserDialog { Description = "Selecciona la carpeta raíz de evidencias destino del proyecto" })
			{
				var def = FocaContext.Current.GetEvidenceRootFolder();
				if (!string.IsNullOrWhiteSpace(def)) fbd.SelectedPath = def;
				if (fbd.ShowDialog(this) == DialogResult.OK)
				{
					destinationRoot = fbd.SelectedPath;
					return true;
				}
			}
			return false;
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
