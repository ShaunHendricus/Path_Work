namespace ETherRealTime
{
	partial class CScanViewer
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.pbCScanBitmap = new System.Windows.Forms.PictureBox();
			this.hScrollBar1 = new System.Windows.Forms.HScrollBar();
			this.vScrollBar1 = new System.Windows.Forms.VScrollBar();
			this.nudCScanZoom = new System.Windows.Forms.NumericUpDown();
			this.label1 = new System.Windows.Forms.Label();
			this.btScreenShot = new System.Windows.Forms.Button();
			this.btConvertCScanFile = new System.Windows.Forms.Button();
			this.btConvertCScanFileCSV = new System.Windows.Forms.Button();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.rbChan1 = new System.Windows.Forms.RadioButton();
			this.rbChan2 = new System.Windows.Forms.RadioButton();
			this.rbChanMix = new System.Windows.Forms.RadioButton();
			((System.ComponentModel.ISupportInitialize)(this.pbCScanBitmap)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.nudCScanZoom)).BeginInit();
			this.SuspendLayout();
			// 
			// pbCScanBitmap
			// 
			this.pbCScanBitmap.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.pbCScanBitmap.Location = new System.Drawing.Point(12, 12);
			this.pbCScanBitmap.Name = "pbCScanBitmap";
			this.pbCScanBitmap.Size = new System.Drawing.Size(973, 469);
			this.pbCScanBitmap.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
			this.pbCScanBitmap.TabIndex = 0;
			this.pbCScanBitmap.TabStop = false;
			// 
			// hScrollBar1
			// 
			this.hScrollBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.hScrollBar1.Location = new System.Drawing.Point(12, 484);
			this.hScrollBar1.Name = "hScrollBar1";
			this.hScrollBar1.Size = new System.Drawing.Size(963, 11);
			this.hScrollBar1.TabIndex = 1;
			this.hScrollBar1.Scroll += new System.Windows.Forms.ScrollEventHandler(this.ScrollBar_Scroll);
			// 
			// vScrollBar1
			// 
			this.vScrollBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.vScrollBar1.Location = new System.Drawing.Point(988, 12);
			this.vScrollBar1.Name = "vScrollBar1";
			this.vScrollBar1.Size = new System.Drawing.Size(11, 469);
			this.vScrollBar1.TabIndex = 2;
			this.vScrollBar1.Scroll += new System.Windows.Forms.ScrollEventHandler(this.ScrollBar_Scroll);
			// 
			// nudCScanZoom
			// 
			this.nudCScanZoom.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.nudCScanZoom.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.nudCScanZoom.Location = new System.Drawing.Point(203, 500);
			this.nudCScanZoom.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
			this.nudCScanZoom.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
			this.nudCScanZoom.Name = "nudCScanZoom";
			this.nudCScanZoom.Size = new System.Drawing.Size(48, 29);
			this.nudCScanZoom.TabIndex = 3;
			this.nudCScanZoom.Tag = "0";
			this.nudCScanZoom.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
			this.nudCScanZoom.ValueChanged += new System.EventHandler(this.nudCScanZoom_ValueChanged);
			// 
			// label1
			// 
			this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label1.AutoSize = true;
			this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.label1.Location = new System.Drawing.Point(257, 502);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(60, 24);
			this.label1.TabIndex = 4;
			this.label1.Text = "Zoom";
			// 
			// btScreenShot
			// 
			this.btScreenShot.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btScreenShot.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.btScreenShot.Location = new System.Drawing.Point(323, 498);
			this.btScreenShot.Name = "btScreenShot";
			this.btScreenShot.Size = new System.Drawing.Size(75, 35);
			this.btScreenShot.TabIndex = 6;
			this.btScreenShot.Text = "SNAP!";
			this.btScreenShot.UseVisualStyleBackColor = true;
			this.btScreenShot.Click += new System.EventHandler(this.btScreenShot_Click);
			// 
			// btConvertCScanFile
			// 
			this.btConvertCScanFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btConvertCScanFile.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.btConvertCScanFile.Location = new System.Drawing.Point(404, 498);
			this.btConvertCScanFile.Name = "btConvertCScanFile";
			this.btConvertCScanFile.Size = new System.Drawing.Size(323, 35);
			this.btConvertCScanFile.TabIndex = 69;
			this.btConvertCScanFile.Text = "Convert C Scan (*.csn) to Excel (*.xml)";
			this.btConvertCScanFile.UseVisualStyleBackColor = true;
			this.btConvertCScanFile.Click += new System.EventHandler(this.btConvertCScanFile_Click);
			// 
			// btConvertCScanFileCSV
			// 
			this.btConvertCScanFileCSV.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btConvertCScanFileCSV.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.btConvertCScanFileCSV.Location = new System.Drawing.Point(733, 498);
			this.btConvertCScanFileCSV.Name = "btConvertCScanFileCSV";
			this.btConvertCScanFileCSV.Size = new System.Drawing.Size(252, 35);
			this.btConvertCScanFileCSV.TabIndex = 70;
			this.btConvertCScanFileCSV.Text = "Convert C Scan (*.csn) to CSV";
			this.btConvertCScanFileCSV.UseVisualStyleBackColor = true;
			this.btConvertCScanFileCSV.Click += new System.EventHandler(this.btConvertCScanFileCSV_Click);
			// 
			// label2
			// 
			this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(539, 536);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(100, 13);
			this.label2.TabIndex = 71;
			this.label2.Text = "Can be VERY slow!";
			// 
			// label3
			// 
			this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(848, 536);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(30, 13);
			this.label3.TabIndex = 72;
			this.label3.Text = "Fast!";
			// 
			// rbChan1
			// 
			this.rbChan1.AutoSize = true;
			this.rbChan1.Checked = true;
			this.rbChan1.Location = new System.Drawing.Point(12, 498);
			this.rbChan1.Name = "rbChan1";
			this.rbChan1.Size = new System.Drawing.Size(44, 17);
			this.rbChan1.TabIndex = 73;
			this.rbChan1.TabStop = true;
			this.rbChan1.Text = "Ch1";
			this.rbChan1.UseVisualStyleBackColor = true;
			this.rbChan1.CheckedChanged += new System.EventHandler(this.rbChan1_CheckedChanged);
			// 
			// rbChan2
			// 
			this.rbChan2.AutoSize = true;
			this.rbChan2.Location = new System.Drawing.Point(12, 517);
			this.rbChan2.Name = "rbChan2";
			this.rbChan2.Size = new System.Drawing.Size(44, 17);
			this.rbChan2.TabIndex = 74;
			this.rbChan2.TabStop = true;
			this.rbChan2.Text = "Ch2";
			this.rbChan2.UseVisualStyleBackColor = true;
			this.rbChan2.CheckedChanged += new System.EventHandler(this.rbChan2_CheckedChanged);
			// 
			// rbChanMix
			// 
			this.rbChanMix.AutoSize = true;
			this.rbChanMix.Location = new System.Drawing.Point(12, 535);
			this.rbChanMix.Name = "rbChanMix";
			this.rbChanMix.Size = new System.Drawing.Size(41, 17);
			this.rbChanMix.TabIndex = 75;
			this.rbChanMix.TabStop = true;
			this.rbChanMix.Text = "Mix";
			this.rbChanMix.UseVisualStyleBackColor = true;
			this.rbChanMix.CheckedChanged += new System.EventHandler(this.rbChanMix_CheckedChanged);
			// 
			// CScanViewer
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1008, 558);
			this.Controls.Add(this.rbChanMix);
			this.Controls.Add(this.rbChan2);
			this.Controls.Add(this.rbChan1);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.btConvertCScanFileCSV);
			this.Controls.Add(this.btConvertCScanFile);
			this.Controls.Add(this.btScreenShot);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.nudCScanZoom);
			this.Controls.Add(this.vScrollBar1);
			this.Controls.Add(this.hScrollBar1);
			this.Controls.Add(this.pbCScanBitmap);
			this.Name = "CScanViewer";
			this.Text = "CScanViewer";
			this.Load += new System.EventHandler(this.CScanViewer_Load);
			this.ResizeEnd += new System.EventHandler(this.CScanViewer_ResizeEnd);
			((System.ComponentModel.ISupportInitialize)(this.pbCScanBitmap)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.nudCScanZoom)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.PictureBox pbCScanBitmap;
		private System.Windows.Forms.HScrollBar hScrollBar1;
		private System.Windows.Forms.VScrollBar vScrollBar1;
		private System.Windows.Forms.NumericUpDown nudCScanZoom;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Button btScreenShot;
		private System.Windows.Forms.Button btConvertCScanFile;
		private System.Windows.Forms.Button btConvertCScanFileCSV;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.RadioButton rbChan1;
		private System.Windows.Forms.RadioButton rbChan2;
		private System.Windows.Forms.RadioButton rbChanMix;
	}
}