using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ETherRealTime
{
    public partial class NewFolder : Form
    {
        public string folder_text = "";

        public NewFolder()
        {
            InitializeComponent();
        }
        public NewFolder(String folder_path)
        {
            InitializeComponent();
            folder_text = folder_path;
            tbExistingFolder.Text = folder_text;
        }
        private void btCancel_Click(object sender, EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
            Close();
        }

        private void btOK_Click(object sender, EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.OK;
            folder_text = tbExistingFolder.Text + tbFolderPath.Text;
            Close();
        }
    }
}
