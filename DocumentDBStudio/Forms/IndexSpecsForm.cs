using System;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Azure.Documents;

namespace Microsoft.Azure.DocumentDBStudio.Forms
{
    public partial class IndexSpecsForm : Form
    {
        private Index index = null;

        public IndexSpecsForm()
        {
            InitializeComponent();
        }

        public Index Index
        {
            get { return index; }
        }

        public void SetIndex(Index index)
        {
            this.index = index;

            if (index.Kind == IndexKind.Hash)
            {
                rbHash.Checked = true;
                if (((HashIndex) index).DataType == DataType.Number)
                {
                    rbNumber.Checked = true;
                }
                else
                {
                    rbString.Checked = true;
                }

                tbPrecision.Text = ((HashIndex) index).Precision.HasValue
                    ? ((HashIndex) index).Precision.Value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
            }
            else
            {
                rbRange.Checked = true;
                if (((RangeIndex) index).DataType == DataType.Number)
                {
                    rbNumber.Checked = true;
                }
                else
                {
                    rbString.Checked = true;
                }

                tbPrecision.Text = ((RangeIndex) index).Precision.HasValue
                    ? ((RangeIndex) index).Precision.Value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            short? precision = null;
            if (!string.IsNullOrEmpty(tbPrecision.Text))
            {
                short precisionValue;
                if (short.TryParse(tbPrecision.Text, out precisionValue))
                {
                    precision = precisionValue;
                }
                else
                {
                    MessageBox.Show("Please enter a valid precision value.");
                    DialogResult = DialogResult.None;
                    return;
                }
            }

            if (rbHash.Checked)
            {
                index = new HashIndex(rbNumber.Checked ? DataType.Number : DataType.String) {Precision = precision};
            }
            else
            {
                index = new RangeIndex(rbNumber.Checked ? DataType.Number : DataType.String) {Precision = precision};
            }

            DialogResult = DialogResult.OK;
            return;
        }
    }
}