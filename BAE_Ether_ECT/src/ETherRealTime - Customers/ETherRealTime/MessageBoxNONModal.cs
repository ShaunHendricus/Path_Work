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
public partial class MessageBoxNONModal : Form
    {
        private string live_text = "Importing row";
        public MessageBoxNONModal()
        {
            InitializeComponent();
        }
        /// <summary>
        /// Optional constructor to change the default main message on the box and also the text that remains in the Header bar.
        /// </summary>
        /// <param name="main_message">The top row of text within the control.</param>
        /// <param name="live_text_pre_number">Default text for the header bar.</param>
        public MessageBoxNONModal(string main_message, string live_text_pre_number)
        {
            InitializeComponent();
            label1.Text = main_message;
            live_text = live_text_pre_number;
        }
        public void SetHeaderRowText(string text)
        {
            this.Text = live_text + " " + text;
        }
    }
}
