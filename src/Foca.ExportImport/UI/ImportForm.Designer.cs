using System.Windows.Forms;

namespace Foca.ExportImport.UI
{
	partial class ImportForm
	{
		private ProgressBar progressBar;
		private ProgressBar progressStep;
		private Button btnStart;
		private Button btnCancel;
		private Label lblStatus;
		private Label lblStep;
		private TextBox txtLog;

		private void InitializeComponent()
		{
			this.progressBar = new ProgressBar();
			this.progressStep = new ProgressBar();
			this.btnStart = new Button();
			this.btnCancel = new Button();
			this.lblStatus = new Label();
			this.lblStep = new Label();
			this.txtLog = new TextBox();
			this.SuspendLayout();
			// 
			// progressBar
			// 
			this.progressBar.Location = new System.Drawing.Point(12, 12);
			this.progressBar.Name = "progressBar";
			this.progressBar.Size = new System.Drawing.Size(620, 18);
			// 
			// progressStep
			// 
			this.progressStep.Location = new System.Drawing.Point(12, 36);
			this.progressStep.Name = "progressStep";
			this.progressStep.Size = new System.Drawing.Size(620, 10);
			// 
			// btnStart
			// 
			this.btnStart.Location = new System.Drawing.Point(12, 52);
			this.btnStart.Name = "btnStart";
			this.btnStart.Size = new System.Drawing.Size(120, 28);
			this.btnStart.Text = "Importar";
			this.btnStart.UseVisualStyleBackColor = true;
			this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
			// 
			// btnCancel
			// 
			this.btnCancel.Location = new System.Drawing.Point(138, 52);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(120, 28);
			this.btnCancel.Text = "Cancelar";
			this.btnCancel.Enabled = false;
			this.btnCancel.UseVisualStyleBackColor = true;
			this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
			// 
			// lblStatus
			// 
			this.lblStatus.AutoSize = true;
			this.lblStatus.Location = new System.Drawing.Point(12, 88);
			this.lblStatus.Name = "lblStatus";
			this.lblStatus.Size = new System.Drawing.Size(0, 13);
			// 
			// lblStep
			// 
			this.lblStep.AutoSize = true;
			this.lblStep.Location = new System.Drawing.Point(12, 106);
			this.lblStep.Name = "lblStep";
			this.lblStep.Size = new System.Drawing.Size(0, 13);
			// 
			// txtLog
			// 
			this.txtLog.Location = new System.Drawing.Point(12, 126);
			this.txtLog.Multiline = true;
			this.txtLog.ReadOnly = true;
			this.txtLog.ScrollBars = ScrollBars.Vertical;
			this.txtLog.Size = new System.Drawing.Size(620, 220);
			this.txtLog.Name = "txtLog";
			// 
			// ImportForm
			// 
			this.AutoScaleMode = AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(644, 360);
			this.Controls.Add(this.txtLog);
			this.Controls.Add(this.lblStep);
			this.Controls.Add(this.lblStatus);
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.btnStart);
			this.Controls.Add(this.progressStep);
			this.Controls.Add(this.progressBar);
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.StartPosition = FormStartPosition.CenterParent;
			this.Text = "Importar proyecto desde .foca";
			this.ResumeLayout(false);
			this.PerformLayout();
		}
	}
}
