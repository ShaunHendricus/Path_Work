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
    public partial class Help : Form
    {
        public Help()
        {
            InitializeComponent();

            try
            {
                rtbHelp.LoadFile("Help.rtf");
            }
            catch
            {
                rtbHelp.Text = "Help file not found.";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
