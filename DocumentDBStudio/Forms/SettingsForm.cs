using System;
using System.Windows.Forms;
using Microsoft.Azure.DocumentDBStudio.Properties;
using Microsoft.Azure.DocumentDBStudio.Util;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.DocumentDBStudio.Forms
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
            AccountSettings = new AccountSettings();
        }

        internal string AccountEndpoint { get; set; }

        internal AccountSettings AccountSettings { get; set; }

        private void SettingsForm_Load(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(AccountEndpoint))
            {
                // load from change settings.
                cbDevFabric.Enabled = false;
                tbAccountName.Enabled = false;
                tbAccountName.Text = AccountEndpoint;
                tbAccountSecret.Text = AccountSettings.MasterKey;

                if (AccountSettings.MasterKey == Constants.LocalEmulatorMasterkey)
                {
                    cbDevFabric.Checked = true;
                }

                if (AccountSettings.ConnectionMode == ConnectionMode.Gateway)
                {
                    radioButtonGateway.Checked = true;
                }
                else if (AccountSettings.Protocol == Protocol.Https)
                {
                    radioButtonDirectHttp.Checked = true;
                }
                else if (AccountSettings.Protocol == Protocol.Tcp)
                {
                    radioButtonDirectTcp.Checked = true;
                }

                cbNameBased.Checked = AccountSettings.IsNameBased;
                tbAccountName.Text = AccountEndpoint;
                tbAccountSecret.Text = AccountSettings.MasterKey;
            }
            else
            {
                radioButtonGateway.Checked = true;

                // disable name based url for now.
                cbNameBased.Checked = false;
                ApplyDevFabricSettings();
            }

            cbNameBased.Visible = true;
            cbDevFabric.Visible = false;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(tbAccountName.Text) || string.IsNullOrEmpty(tbAccountSecret.Text))
            {
                MessageBox.Show("Please input the valid account settings", Constants.ApplicationName);
                DialogResult = DialogResult.None;
            }
            AccountEndpoint = tbAccountName.Text;
            AccountSettings.MasterKey = tbAccountSecret.Text;

            if (radioButtonGateway.Checked)
            {
                AccountSettings.ConnectionMode = ConnectionMode.Gateway;
            }
            else if (radioButtonDirectHttp.Checked)
            {
                AccountSettings.ConnectionMode = ConnectionMode.Direct;
                AccountSettings.Protocol = Protocol.Https;
            }
            else if (radioButtonDirectTcp.Checked)
            {
                AccountSettings.ConnectionMode = ConnectionMode.Direct;
                AccountSettings.Protocol = Protocol.Tcp;
            }
            AccountSettings.IsNameBased = cbNameBased.Checked;

            Settings.Default.Save();
        }

        private void cbDevFabric_CheckedChanged(object sender, EventArgs e)
        {
            ApplyDevFabricSettings();
        }

        private void ApplyDevFabricSettings()
        {
            if (cbDevFabric.Checked)
            {
                tbAccountSecret.Enabled = false;
                tbAccountName.Text = Constants.LocalEmulatorEndpoint;
                tbAccountSecret.Text = Constants.LocalEmulatorMasterkey;
            }
            else
            {
                tbAccountSecret.Enabled = true;
                tbAccountName.Text = "";
                tbAccountSecret.Text = "";
            }
        }
    }
}