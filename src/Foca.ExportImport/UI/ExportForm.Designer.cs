using System.Windows.Forms;

namespace Foca.ExportImport.UI
{
	partial class ExportForm
	{
		private ProgressBar progressBar;
		private ProgressBar progressStep;
		private Button btnStart;
		private Button btnCancel;
		private Label lblStatus;
		private Label lblStep;
		private TextBox txtLog;
		private ComboBox cmbProjects;
		private Label lblProject;

		private void InitializeComponent()
		{
			this.progressBar = new ProgressBar();
			this.progressStep = new ProgressBar();
			this.btnStart = new Button();
			this.btnCancel = new Button();
			this.lblStatus = new Label();
			this.lblStep = new Label();
			this.txtLog = new TextBox();
			this.cmbProjects = new ComboBox();
			this.lblProject = new Label();
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
			this.btnStart.Location = new System.Drawing.Point(12, 88);
			this.btnStart.Name = "btnStart";
			this.btnStart.Size = new System.Drawing.Size(120, 28);
			this.btnStart.Text = "Exportar";
			this.btnStart.UseVisualStyleBackColor = true;
			this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
			// 
			// btnCancel
			// 
			this.btnCancel.Location = new System.Drawing.Point(138, 88);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(120, 28);
			this.btnCancel.Text = "Cancelar";
			this.btnCancel.Enabled = false;
			this.btnCancel.UseVisualStyleBackColor = true;
			this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
			// 
			// cmbProjects
			// 
			this.cmbProjects.DropDownStyle = ComboBoxStyle.DropDownList;
			this.cmbProjects.Location = new System.Drawing.Point(70, 52);
			this.cmbProjects.Name = "cmbProjects";
			this.cmbProjects.Size = new System.Drawing.Size(562, 21);
			// 
			// lblProject
			// 
			this.lblProject.AutoSize = true;
			this.lblProject.Location = new System.Drawing.Point(12, 55);
			this.lblProject.Name = "lblProject";
			this.lblProject.Size = new System.Drawing.Size(52, 13);
			this.lblProject.Text = "Proyecto";
			// 
			// lblStatus
			// 
			this.lblStatus.AutoSize = true;
			this.lblStatus.Location = new System.Drawing.Point(12, 124);
			this.lblStatus.Name = "lblStatus";
			this.lblStatus.Size = new System.Drawing.Size(0, 13);
			// 
			// lblStep
			// 
			this.lblStep.AutoSize = true;
			this.lblStep.Location = new System.Drawing.Point(12, 142);
			this.lblStep.Name = "lblStep";
			this.lblStep.Size = new System.Drawing.Size(0, 13);
			// 
			// txtLog
			// 
			this.txtLog.Location = new System.Drawing.Point(12, 162);
			this.txtLog.Multiline = true;
			this.txtLog.ReadOnly = true;
			this.txtLog.ScrollBars = ScrollBars.Vertical;
			this.txtLog.Size = new System.Drawing.Size(620, 184);
			this.txtLog.Name = "txtLog";
			// 
			// ExportForm
			// 
			this.AutoScaleMode = AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(644, 358);
			this.Controls.Add(this.lblProject);
			this.Controls.Add(this.cmbProjects);
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
			this.Text = "Exportar proyecto a .foca";
			this.Shown += new System.EventHandler(this.ExportForm_Shown);
			this.ResumeLayout(false);
			this.PerformLayout();
		}
	}
}
