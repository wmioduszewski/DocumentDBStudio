using System;
using System.Windows.Forms;
using Microsoft.Azure.Documents;

namespace Microsoft.Azure.DocumentDBStudio.Forms
{
    public partial class IncludedPathForm : Form
    {
        private IncludedPath includedPath = null;

        public IncludedPathForm()
        {
            InitializeComponent();
        }

        public IncludedPath IncludedPath
        {
            get { return includedPath; }
        }

        public void SetIncludedPath(IncludedPath includedPath)
        {
            this.includedPath = includedPath;

            // init the path
            tbIncludedPathPath.Text = includedPath.Path;
            lbIndexes.Items.Clear();

            foreach (Index index in includedPath.Indexes)
            {
                lbIndexes.Items.Add(index);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(tbIncludedPathPath.Text))
            {
                MessageBox.Show("Please input the valid path");
                DialogResult = DialogResult.None;
                return;
            }

            includedPath = new IncludedPath();

            includedPath.Path = tbIncludedPathPath.Text;

            foreach (object item in lbIndexes.Items)
            {
                Index index = item as Index;
                includedPath.Indexes.Add(index);
            }

            DialogResult = DialogResult.OK;
            return;
        }

        private void btnAddIndexSpec_Click(object sender, EventArgs e)
        {
            IndexSpecsForm dlg = new IndexSpecsForm();
            dlg.StartPosition = FormStartPosition.CenterParent;
            DialogResult dr = dlg.ShowDialog(this);
            if (dr == DialogResult.OK)
            {
                lbIndexes.Items.Add(dlg.Index);
            }
        }

        private void btnRemoveIndexSpec_Click(object sender, EventArgs e)
        {
            lbIndexes.Items.RemoveAt(lbIndexes.SelectedIndex);
        }

        private void btnEditIndexSpec_Click(object sender, EventArgs e)
        {
            Index index = lbIndexes.SelectedItem as Index;

            IndexSpecsForm dlg = new IndexSpecsForm();
            dlg.StartPosition = FormStartPosition.CenterParent;

            dlg.SetIndex(index);

            DialogResult dr = dlg.ShowDialog(this);
            if (dr == DialogResult.OK)
            {
                lbIndexes.Items[lbIndexes.SelectedIndex] = dlg.Index;
            }
        }

        private void lbIndexes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbIndexes.SelectedItem != null)
            {
                btnEditIndexSpec.Enabled = true;
                btnRemoveIndexSpec.Enabled = true;
            }
            else
            {
                btnEditIndexSpec.Enabled = false;
                btnRemoveIndexSpec.Enabled = false;
            }
        }
    }
}