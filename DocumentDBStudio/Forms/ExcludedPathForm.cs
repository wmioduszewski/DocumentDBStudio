using System;
using System.Windows.Forms;

namespace Microsoft.Azure.DocumentDBStudio.Forms
{
    public partial class ExcludedPathForm : Form
    {
        public ExcludedPathForm()
        {
            InitializeComponent();
        }

        public String ExcludedPath { get; private set; }

        private void btnSaveExcludedPath_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(tbExcludedPath.Text))
            {
                MessageBox.Show("Please input the valid path");
                DialogResult = DialogResult.None;
                return;
            }
            ExcludedPath = tbExcludedPath.Text;
        }
    }
}