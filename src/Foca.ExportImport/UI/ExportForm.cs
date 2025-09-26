using System;
using System.ComponentModel;
using System.Windows.Forms;
using Foca.ExportImport.Models;
using Foca.ExportImport.Services;
using System.Data.SqlClient;

namespace Foca.ExportImport.UI
{
	public partial class ExportForm : Form
	{
		private readonly BackgroundWorker worker;
		private ExportService exportService;
		private string exportPath;
		private int selectedProjectId;
		private string selectedProjectName;
		private string selectedEvidenceRoot;

		private sealed class ProjectItem
		{
			public int Id { get; set; }
			public string Name { get; set; }
			public string Evidence { get; set; }
			public override string ToString() { return Name; }
		}

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

				// Debe haber un proyecto seleccionado
				if (cmbProjects.SelectedItem is ProjectItem sel)
				{
					selectedProjectId = sel.Id;
					selectedProjectName = sel.Name;
					selectedEvidenceRoot = string.IsNullOrWhiteSpace(sel.Evidence) ? FocaContext.Current.GetEvidenceRootFolder() : sel.Evidence;
				}
				else
				{
					MessageBox.Show(this, "Selecciona un proyecto.", "FOCA", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

				// Inicializar servicios con la cadena de conexión del contexto
				exportService = new ExportService(
					new DatabaseService(FocaContext.Current.GetConnectionString()),
					new ZipAndHashService());

				using (var sfd = new SaveFileDialog { Filter = "FOCA export (*.foca)|*.foca", Title = "Guardar proyecto como .foca", OverwritePrompt = true })
				{
					sfd.FileName = string.IsNullOrWhiteSpace(selectedProjectName) ? "export.foca" : selectedProjectName + ".foca";
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

			var projectId = selectedProjectId;
			var projectName = selectedProjectName;
			var evidence = selectedEvidenceRoot;

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

		private void ExportForm_Shown(object sender, EventArgs e)
		{
			// Intentar configurar contexto si no existe
			try { var _ = FocaContext.Current; }
			catch { try { FocaContext.Configure(new AutoFocaContext()); } catch { } }

			// Cargar proyectos desde la BD "Foca"
			try
			{
				var cs = FocaContext.Current.GetConnectionString();
				using (var conn = new SqlConnection(cs))
				{
					conn.Open();
					using (var cmd = new SqlCommand("SELECT Id, ProjectName, FolderToDownload FROM [Projects] ORDER BY ProjectDate DESC, Id DESC", conn))
					using (var r = cmd.ExecuteReader())
					{
						cmbProjects.Items.Clear();
						while (r.Read())
						{
							var item = new ProjectItem
							{
								Id = r.IsDBNull(0) ? 0 : r.GetInt32(0),
								Name = r.IsDBNull(1) ? "" : r.GetString(1),
								Evidence = r.IsDBNull(2) ? null : r.GetString(2)
							};
							cmbProjects.Items.Add(item);
						}
						if (cmbProjects.Items.Count > 0) cmbProjects.SelectedIndex = 0;
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "No se pudieron cargar los proyectos desde la BD.\r\n\r\n" + ex.Message, "FOCA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
			btnCancel.Enabled = false;
			if (e.Error != null)
			{
				btnStart.Enabled = true;
				MessageBox.Show(this, e.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			else if (e.Cancelled)
			{
				btnStart.Enabled = true;
				lblStatus.Text = "Cancelado";
			}
			else
			{
				lblStatus.Text = "Completado";
				progressStep.Value = 100;
				btnStart.Enabled = false;
				btnCancel.Visible = false;
				btnClose.Visible = true;
			}
		}

		private void btnClose_Click(object sender, EventArgs e)
		{
			this.Close();
		}
	}
}
