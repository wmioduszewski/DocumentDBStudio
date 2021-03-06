//-----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Azure.DocumentDBStudio.Forms;
using Microsoft.Azure.DocumentDBStudio.Properties;
using Microsoft.Azure.DocumentDBStudio.TreeNodeElems;
using Microsoft.Azure.DocumentDBStudio.Util;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.DocumentDBStudio
{
    public partial class MainForm : Form
    {
        private string _appTempPath;
        CheckBox _cbEnableScan;

        private DocumentCollection _collectionToCreate;

        private String _currentCrudName;

        private string _currentJson;
        Func<string, object, Task> _currentOperation;
        private string _currentText;
        private int _fontScale = 100;
        private string _homepage;

        private string _loadingGifPath;
        private String _offerType;
        private string _prettyJsonTemplate;

        private RequestOptions _requestOptions;
        private int defaultFontPoint = 9;

        public MainForm()
        {
            InitializeComponent();
        }

        public void ChangeAccountSettings(TreeNode thisNode, string accountEndpoint)
        {
            treeView1.SelectedNode = thisNode;

            for (int i = 0; i < Settings.Default.AccountSettingsList.Count; i = i + 2)
            {
                if (
                    string.Compare(accountEndpoint, Settings.Default.AccountSettingsList[i],
                        StringComparison.OrdinalIgnoreCase) == 0)
                {
                    AccountSettings accountSettings =
                        (AccountSettings)
                            JsonConvert.DeserializeObject(Settings.Default.AccountSettingsList[i + 1],
                                typeof (AccountSettings));

                    // Bring up account setings dialog
                    SettingsForm dlg = new SettingsForm
                    {
                        AccountEndpoint = accountEndpoint,
                        AccountSettings = accountSettings
                    };

                    DialogResult dr = dlg.ShowDialog(this);
                    if (dr == DialogResult.OK)
                    {
                        thisNode.Remove();
                        RemoveAccountFromSettings(dlg.AccountEndpoint);
                        AddAccountToSettings(dlg.AccountEndpoint, dlg.AccountSettings);
                    }

                    break;
                }
            }
        }

        public void RemoveAccountFromSettings(string accountEndpoint)
        {
            int index = -1;
            // if the account is not in tree view top level, add it!
            for (int i = 0; i < Settings.Default.AccountSettingsList.Count; i = i + 2)
            {
                if (
                    string.Compare(accountEndpoint, Settings.Default.AccountSettingsList[i],
                        StringComparison.OrdinalIgnoreCase) == 0)
                {
                    index = i;
                    break;
                }
            }

            if (index >= 0)
            {
                Settings.Default.AccountSettingsList.RemoveRange(index, 2);
                Settings.Default.Save();
            }
        }

        public FeedOptions GetFeedOptions()
        {
            FeedOptions feedOptions = new FeedOptions();

            try
            {
                feedOptions.MaxItemCount = Convert.ToInt32(toolStripTextMaxItemCount.Text, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                // Ignore the exception and use the defualt value
            }

            if (_cbEnableScan.CheckState == CheckState.Checked)
            {
                feedOptions.EnableScanInQuery = true;
            }
            else if (_cbEnableScan.CheckState == CheckState.Unchecked)
            {
                feedOptions.EnableScanInQuery = false;
            }

            return feedOptions;
        }

        public RequestOptions GetRequestOptions(bool isCollection = false)
        {
            if (_requestOptions != null)
            {
                if (tbPostTrigger.Modified)
                {
                    string postTrigger = tbPostTrigger.Text;
                    if (!string.IsNullOrEmpty(postTrigger))
                    {
                        // split by ;
                        string[] segments = postTrigger.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);
                        _requestOptions.PostTriggerInclude = segments;
                    }
                    tbPostTrigger.Modified = false;
                }

                if (tbPreTrigger.Modified)
                {
                    string preTrigger = tbPreTrigger.Text;
                    if (!string.IsNullOrEmpty(preTrigger))
                    {
                        // split by ;
                        string[] segments = preTrigger.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);
                        _requestOptions.PreTriggerInclude = segments;
                    }
                }
                if (tbAccessConditionText.Modified)
                {
                    string condition = tbAccessConditionText.Text;
                    if (!string.IsNullOrEmpty(condition))
                    {
                        _requestOptions.AccessCondition.Condition = condition;
                    }
                }
            }

            RequestOptions requestOptions = _requestOptions;

            if (isCollection)
            {
                if (requestOptions != null)
                {
                    requestOptions.OfferType = _offerType;
                }
                else
                {
                    requestOptions = new RequestOptions {OfferType = _offerType};
                }
            }

            return requestOptions;
        }

        public void CheckCurrentRelease()
        {
            Thread.Sleep(3000);

            Uri uri = new Uri("https://api.github.com/repos/mingaliu/documentdbstudio/releases");

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpRequestMessage request = new HttpRequestMessage
                    {
                        RequestUri = uri,
                        Method = HttpMethod.Get
                    };
                    request.Headers.UserAgent.Add(new ProductInfoHeaderValue("DocumentDBStudio",
                        Constants.ProductVersion));

                    HttpResponseMessage response = client.SendAsync(request).Result;

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        JArray releaseJson = JArray.Parse(response.Content.ReadAsStringAsync().Result);
                        JToken latestRelease = releaseJson.First;
                        JToken latestReleaseTag = latestRelease["tag_name"];
                        string latestReleaseString = latestReleaseTag.ToString();

                        if (
                            string.Compare(Constants.ProductVersion, latestReleaseString,
                                StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            Invoke(new MessageBoxDelegate(ShowMessage),
                                string.Format(CultureInfo.InvariantCulture,
                                    "Please update the DocumentDB studio to the latest version {0} at https://github.com/mingaliu/DocumentDBStudio/releases",
                                    latestReleaseString),
                                Constants.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
                catch (Exception)
                {
                    // ignore any exception here.
                }
            }
        }

        public void SetCrudContext(TreeNode node, string name, bool showId, string bodytext,
            Func<string, object, Task> func,
            CommandContext commandContext = null)
        {
            if (commandContext == null)
            {
                commandContext = new CommandContext();
            }
            treeView1.SelectedNode = node;

            _currentCrudName = name;
            _currentOperation = func;

            tabCrudContext.Text = name;
            tbCrudContext.Text = bodytext;

            toolStripBtnExecute.Enabled = true;
            tbCrudContext.ReadOnly = commandContext.IsDelete;

            // the whole left split panel.
            splitContainerInner.Panel1Collapsed = false;
            //the split panel inside Tab. Panel1: Id, Panel2: Edit CRUD.
            splitContainerIntabPage.Panel1Collapsed = !showId;

            tbResponse.Text = "";

            //the split panel at right bottom. Panel1: NextPage, Panel2: Browser.
            if (commandContext.IsFeed)
            {
                ButtomSplitContainer.Panel1Collapsed = false;
                ButtomSplitContainer.Panel1.Controls.Clear();
                ButtomSplitContainer.Panel1.Controls.Add(feedToolStrip);
            }
            else if (commandContext.IsCreateTrigger)
            {
                ButtomSplitContainer.Panel1Collapsed = false;
                ButtomSplitContainer.Panel1.Controls.Clear();
                ButtomSplitContainer.Panel1.Controls.Add(triggerPanel);
            }
            else
            {
                ButtomSplitContainer.Panel1Collapsed = true;
            }

            SetNextPageVisibility(commandContext);

            if (string.Compare(name, "Create documentCollection", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(name, "Replace DocumentCollection", StringComparison.OrdinalIgnoreCase) == 0)
            {
                if (string.Compare(name, "Create documentCollection", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    tbCollectionId.Enabled = true;
                    tbCollectionId.Text = "DocumentCollection Id";
                }
                else
                {
                    tbCollectionId.Enabled = false;
                    tbCollectionId.Text = (node.Tag as Resource).Id;
                }

                if (tabControl.TabPages.Contains(tabCrudContext))
                {
                    tabControl.TabPages.Insert(0, tabDocumentCollectionPolicy);
                    tabControl.TabPages.Remove(tabCrudContext);
                }
                tabControl.SelectedTab = tabDocumentCollectionPolicy;
            }
            else
            {
                if (tabControl.TabPages.Contains(tabDocumentCollectionPolicy))
                {
                    tabControl.TabPages.Remove(tabDocumentCollectionPolicy);
                    tabControl.TabPages.Insert(0, tabCrudContext);
                }
                tabControl.SelectedTab = tabCrudContext;
            }
        }

        public void SetNextPageVisibility(CommandContext commandContext)
        {
            btnExecuteNext.Enabled = commandContext.HasContinuation || !commandContext.QueryStarted;
        }

        public void SetLoadingState()
        {
            //
            webBrowserResponse.Url = new Uri(_loadingGifPath);
        }

        public void RenderFile(string fileName)
        {
            //
            webBrowserResponse.Url = new Uri(fileName);
        }

        public void SetResultInBrowser(string json, string text, bool executeButtonEnabled,
            NameValueCollection responseHeaders = null)
        {
            _currentText = text;
            _currentJson = json;
            DisplayResponseContent();

            toolStripBtnExecute.Enabled = executeButtonEnabled;

            SetResponseHeaders(responseHeaders);
        }

        public void SetStatus(string status)
        {
            tsStatus.Text = status;
        }

        public void SetResponseHeaders(NameValueCollection responseHeaders)
        {
            if (responseHeaders != null)
            {
                string headers = "";
                foreach (string key in responseHeaders.Keys)
                {
                    headers += string.Format(CultureInfo.InvariantCulture, "{0}: {1}\r\n", key, responseHeaders[key]);

                    if (string.Compare("x-ms-request-charge", key, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        tsStatus.Text = tsStatus.Text + ", RequestChange: " + responseHeaders[key];
                    }
                }
                tbResponse.Text = headers;
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            // ToolStrips don't appear to have a way to "spring" their items like status bars
            cbUrl.Width = tsAddress.Width - 40 - tsLabelUrl.Width - btnGo.Width;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(arg => CheckCurrentRelease());

            Height = Screen.GetWorkingArea(this).Height*3/4;
            Width = Screen.GetWorkingArea(this).Width/2;
            Top = 0;
            Text = Constants.ApplicationName;

            using (
                Stream stm =
                    Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream("Microsoft.Azure.DocumentDBStudio.Resources.home.html"))
            {
                using (StreamReader reader = new StreamReader(stm))
                {
                    _homepage = reader.ReadToEnd();
                }
            }
            _homepage = _homepage.Replace("&VERSION&", Constants.ProductVersion);

            DateTime t = File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location);
            DateTimeOffset dateOffset = new DateTimeOffset(t, TimeZoneInfo.Local.GetUtcOffset(t));
            _homepage = _homepage.Replace("&BUILDTIME&", t.ToString("f", CultureInfo.CurrentCulture));

            cbUrl.Items.Add("about:home");
            cbUrl.SelectedIndex = 0;
            cbUrl.KeyDown += cbUrl_KeyDown;

            btnBack.Enabled = false;

            splitContainerOuter.Panel1Collapsed = false;
            splitContainerInner.Panel1Collapsed = true;
            ButtomSplitContainer.Panel1Collapsed = true;

            KeyPreview = true;
            PreviewKeyDown += MainForm_PreviewKeyDown;

            webBrowserResponse.PreviewKeyDown += webBrowserResponse_PreviewKeyDown;
            webBrowserResponse.StatusTextChanged += webBrowserResponse_StatusTextChanged;
            webBrowserResponse.ScriptErrorsSuppressed = true;

            tabControl.SelectedTab = tabCrudContext;
            tabControl.TabPages.Remove(tabRequest);
            tabControl.TabPages.Remove(tabDocumentCollectionPolicy);

            ImageList imageList = new ImageList();
            imageList.Images.Add("Default", Resources.DocDBpng);
            imageList.Images.Add("Feed", Resources.Feedpng);
            imageList.Images.Add("Javascript", Resources.Javascriptpng);
            imageList.Images.Add("User", Resources.Userpng);
            imageList.Images.Add("Permission", Resources.Permissionpng);
            imageList.Images.Add("DatabaseAccount", Resources.DatabaseAccountpng);
            imageList.Images.Add("SystemFeed", Resources.SystemFeedpng);
            imageList.Images.Add("Attachment", Resources.Attachmentpng);
            imageList.Images.Add("Conflict", Resources.Conflictpng);
            imageList.Images.Add("Offer", Resources.SpecialOfferpng);
            treeView1.ImageList = imageList;

            InitTreeView();

            btnHome_Click(null, null);

            splitContainerIntabPage.Panel1Collapsed = true;

            toolStripBtnExecute.Enabled = false;
            btnExecuteNext.Enabled = false;
            UnpackEmbeddedResources();

            tsbViewType.Checked = true;
            btnHeaders.Checked = false;

            cbRequestOptionsApply_CheckedChanged(null, null);
            cbIndexingPolicyDefault_CheckedChanged(null, null);

            _cbEnableScan = new CheckBox();
            _cbEnableScan.Text = "EnableScanInQuery";
            _cbEnableScan.CheckState = CheckState.Indeterminate;
            ToolStripControlHost host = new ToolStripControlHost(_cbEnableScan);
            feedToolStrip.Items.Insert(1, host);

            lbIncludedPath.Items.Add(new IncludedPath {Path = "/"});
        }


        private void UnpackEmbeddedResources()
        {
            _appTempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DocumentDBStudio");

            if (!Directory.Exists(_appTempPath))
            {
                Directory.CreateDirectory(_appTempPath);
            }

            _loadingGifPath = Path.Combine(_appTempPath, "loading.gif");

            using (
                Stream stream =
                    Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream("Microsoft.Azure.DocumentDBStudio.Resources.loading.gif"))
            {
                using (FileStream fileStream = File.Create(_loadingGifPath))
                {
                    stream.CopyTo(fileStream);
                }
            }

            using (
                Stream stream =
                    Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream(
                            "Microsoft.Azure.DocumentDBStudio.Resources.prettyJSON.backbone-min.js"))
            {
                using (FileStream fileStream = File.Create(Path.Combine(_appTempPath, "backbone-min.js")))
                {
                    stream.CopyTo(fileStream);
                }
            }
            using (
                Stream stream =
                    Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream(
                            "Microsoft.Azure.DocumentDBStudio.Resources.prettyJSON.jquery-1.11.1.min.js"))
            {
                using (FileStream fileStream = File.Create(Path.Combine(_appTempPath, "jquery-1.11.1.min.js")))
                {
                    stream.CopyTo(fileStream);
                }
            }
            using (
                Stream stream =
                    Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream(
                            "Microsoft.Azure.DocumentDBStudio.Resources.prettyJSON.pretty-json.css"))
            {
                using (FileStream fileStream = File.Create(Path.Combine(_appTempPath, "pretty-json.css")))
                {
                    stream.CopyTo(fileStream);
                }
            }
            using (
                Stream stream =
                    Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream(
                            "Microsoft.Azure.DocumentDBStudio.Resources.prettyJSON.pretty-json-min.js"))
            {
                using (FileStream fileStream = File.Create(Path.Combine(_appTempPath, "pretty-json-min.js")))
                {
                    stream.CopyTo(fileStream);
                }
            }
            using (
                Stream stream =
                    Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream(
                            "Microsoft.Azure.DocumentDBStudio.Resources.prettyJSON.underscore-min.js"))
            {
                using (FileStream fileStream = File.Create(Path.Combine(_appTempPath, "underscore-min.js")))
                {
                    stream.CopyTo(fileStream);
                }
            }

            using (
                Stream stream =
                    Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream(
                            "Microsoft.Azure.DocumentDBStudio.Resources.prettyJSON.PrettyPrintJSONTemplate.html"))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    _prettyJsonTemplate = reader.ReadToEnd();
                }
            }
        }

        void webBrowserResponse_StatusTextChanged(object sender, EventArgs e)
        {
        }

        void cbUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                webBrowserResponse.Navigate(cbUrl.Text);
                e.Handled = true;
            }
        }

        void webBrowserResponse_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (webBrowserResponse.Focused)
            {
                HandlePreviewKeyDown(e.KeyCode, e.Modifiers);
            }
        }

        void MainForm_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (!webBrowserResponse.Focused && !cbUrl.Focused)
            {
                HandlePreviewKeyDown(e.KeyCode, e.Modifiers);
            }
        }

        bool HandlePreviewKeyDown(Keys key, Keys modifiers)
        {
            if (key == Keys.Back)
            {
                // Don't steal backspace from the URL combo box
                if (!cbUrl.Focused)
                {
                    return true;
                }
            }
            else if (key == Keys.F5)
            {
                return true;
            }
            else if (key == Keys.Enter)
            {
                webBrowserResponse.Navigate(cbUrl.Text);
                return true;
            }
            else if (key == Keys.W && modifiers == Keys.Control)
            {
                // Exit the app on Ctrl + W like browser tabs
                Close();
                return true;
            }
            else if (key == Keys.D && modifiers == Keys.Alt)
            {
                // Focus the URL in the address bar
                cbUrl.SelectAll();
                cbUrl.Focus();
            }
            return false;
        }

        private void tbCrudContext_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F5:
                {
                    if (toolStripBtnExecute.Enabled)
                    {
                        toolStripBtnExecute_Click(null, null);
                    }
                }
                    break;
                case Keys.A:
                    if (e.Control)
                    {
                        tbCrudContext.SelectAll();
                    }
                    break;
            }
        }

        //
        private void DisplayResponseContent()
        {
            if (tsbViewType.Checked)
            {
                PrettyPrintJson(_currentJson, _currentText);
            }
            else
            {
                string htmlResponse = "";

                if (!string.IsNullOrEmpty(_currentJson))
                {
                    htmlResponse = Helper.FormatTextAsHtml(_currentJson, false);
                }
                if (!string.IsNullOrEmpty(_currentText))
                {
                    htmlResponse += "\r\n\r\n" + Helper.FormatTextAsHtml(_currentText, false);
                }
                DisplayHtmlContentInScale(htmlResponse);
            }
        }

        void DisplayHtmlContentInScale(string htmlResponse)
        {
            if (_fontScale != 100)
            {
                // current scaled font
                float fontPt = defaultFontPoint*(_fontScale/100.0f);

                // todo: make this a well defined class
                string style = "{ font-size: " + fontPt + "pt; }";
                string s = htmlResponse.Replace("{ font-size: 9pt; }", style);
                webBrowserResponse.DocumentText = s;
            }
            else
            {
                webBrowserResponse.DocumentText = htmlResponse;
            }
        }

        private void tsButtonZoom_ButtonClick(object sender, EventArgs e)
        {
            switch (tsButtonZoom.Text)
            {
                case "100%":
                    _fontScale = 125;
                    break;
                case "125%":
                    _fontScale = 150;
                    break;
                case "150%":
                    _fontScale = 175;
                    break;
                case "175%":
                    _fontScale = 100;
                    break;
            }
            tsButtonZoom.Text = _fontScale.ToString(CultureInfo.CurrentCulture) + "%";
            tbRequest.Font = new Font(tbRequest.Font.FontFamily.Name, defaultFontPoint*(_fontScale/100.0f));
            tbResponse.Font = new Font(tbResponse.Font.FontFamily.Name, defaultFontPoint*(_fontScale/100.0f));
            Font = new Font(tbResponse.Font.FontFamily.Name, defaultFontPoint*(_fontScale/100.0f));

            // we don't support pretty print for font scaling yet.
            if (!tsbViewType.Checked)
            {
                DisplayResponseContent();
            }
        }

        private void btnHeaders_Click(object sender, EventArgs e)
        {
            if (splitContainerInner.Panel1Collapsed)
            {
                splitContainerInner.Panel1Collapsed = false;
                btnHeaders.Checked = true;
                btnHeaders.Text = "Hide Response Headers";

                tabControl.SelectedTab = tabResponse;
            }
            else
            {
                splitContainerInner.Panel1Collapsed = true;
                btnHeaders.Checked = false;
                btnHeaders.Text = "Show Response Headers";
            }
        }

        private void btnHome_Click(object sender, EventArgs e)
        {
            //
            DisplayHtmlContentInScale(_homepage);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(this, Constants.ApplicationName + "\nVersion " + Constants.ProductVersion,
                "About",
                MessageBoxButtons.OK);
        }

        private void tsbViewType_Click(object sender, EventArgs e)
        {
            if (tsbViewType.Checked)
                tsbViewType.Text = "Pretty Json View";
            else
                tsbViewType.Text = "Text View";

            if ((webBrowserResponse.Url.AbsoluteUri == "about:blank" &&
                 webBrowserResponse.DocumentTitle != "DataModelBrowserHome")
                || webBrowserResponse.Url.Scheme == "file")
            {
                DisplayResponseContent();
            }
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Bring up account setings dialog
            SettingsForm dlg = new SettingsForm();
            DialogResult dr = dlg.ShowDialog(this);
            if (dr == DialogResult.OK)
            {
                AddAccountToSettings(dlg.AccountEndpoint, dlg.AccountSettings);
            }
        }

        private void AddAccountToSettings(string accountEndpoint, AccountSettings accountSettings)
        {
            bool found = false;
            // if the account is not in tree view top level, add it!
            for (int i = 0; i < Settings.Default.AccountSettingsList.Count; i = i + 2)
            {
                if (
                    string.Compare(accountEndpoint, Settings.Default.AccountSettingsList[i],
                        StringComparison.OrdinalIgnoreCase) == 0)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Settings.Default.AccountSettingsList.Add(accountEndpoint);
                Settings.Default.AccountSettingsList.Add(JsonConvert.SerializeObject(accountSettings));

                Settings.Default.Save();

                AddConnectionTreeNode(accountEndpoint, accountSettings);
            }
        }

        private DialogResult ShowMessage(string msg, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return MessageBox.Show(this, msg, title, buttons, icon);
        }

        private void InitTreeView()
        {
            if (Settings.Default.AccountSettingsList == null)
            {
                Settings.Default.AccountSettingsList = new List<string>();
            }
            // load the account settings from the List.
            for (int i = 0; i < Settings.Default.AccountSettingsList.Count; i = i + 2)
            {
                AccountSettings accountSettings =
                    (AccountSettings)
                        JsonConvert.DeserializeObject(Settings.Default.AccountSettingsList[i + 1],
                            typeof (AccountSettings));
                AddConnectionTreeNode(Settings.Default.AccountSettingsList[i], accountSettings);
            }
        }

        private void AddConnectionTreeNode(string accountEndpoint, AccountSettings accountSettings)
        {
            try
            {
                string suffix = Constants.ApplicationName + "/" + Constants.ProductVersion;

                DocumentClient client = new DocumentClient(new Uri(accountEndpoint), accountSettings.MasterKey,
                    new ConnectionPolicy
                    {
                        ConnectionMode = accountSettings.ConnectionMode,
                        ConnectionProtocol = accountSettings.Protocol,
                        UserAgentSuffix = suffix
                    });

                DatabaseAccountNode dbaNode = new DatabaseAccountNode(accountEndpoint, client);
                treeView1.Nodes.Add(dbaNode);

                // Update the map.
                DocumentClientExtension.AddOrUpdate(client.ServiceEndpoint.Host, accountSettings.IsNameBased);
                if (accountSettings.IsNameBased)
                {
                    dbaNode.ForeColor = Color.Green;
                }
                else
                {
                    dbaNode.ForeColor = Color.Blue;
                }

                // Set the tag to the DatabaseAccount resource object, this might fail if the service is not available.
                dbaNode.Tag = client.GetDatabaseAccountAsync().Result;
            }
            catch (Exception e)
            {
                Program.GetMain().SetResultInBrowser(null, e.ToString(), true);
            }
        }

        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node is NodeBase)
            {
                (e.Node as NodeBase).Refresh(false);
            }
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (e.Node is NodeBase)
                {
                    (e.Node as NodeBase).ShowContextMenu(e.Node.TreeView, e.Location);
                }
            }
            else if (e.Button == MouseButtons.Left)
            {
                RenderJson(e.Node);
            }
        }

        private void RenderJson(TreeNode treeNode)
        {
            // render the JSON in the right panel.
            _currentText = null;
            _currentJson = null;

            if (treeNode is DocumentNode)
            {
                DocumentNode nodeBase = treeNode as DocumentNode;
                string body = nodeBase.GetBody();

                if (!string.IsNullOrEmpty(body))
                {
                    _currentText = body;
                }
            }

            if (treeNode.Tag is string)
            {
                _currentText = treeNode.Tag.ToString();
            }
            else if (treeNode is DatabaseAccountNode)
            {
                _currentJson = JsonConvert.SerializeObject(treeNode.Tag, Formatting.Indented);
            }
            else if (treeNode.Tag != null)
            {
                _currentJson = treeNode.Tag.ToString();
            }

            if (_currentJson == null && _currentText == null)
            {
                _currentText = treeNode.Text;
            }

            DisplayResponseContent();
        }

        private void toolStripBtnExecute_Click(object sender, EventArgs e)
        {
            SetLoadingState();

            if (string.Compare(_currentCrudName, "Create documentCollection", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(_currentCrudName, "Replace documentCollection", StringComparison.OrdinalIgnoreCase) == 0)
            {
                _collectionToCreate.IndexingPolicy.IncludedPaths.Clear();
                foreach (object item in lbIncludedPath.Items)
                {
                    IncludedPath includedPath = item as IncludedPath;
                    _collectionToCreate.IndexingPolicy.IncludedPaths.Add(includedPath);
                }

                _collectionToCreate.IndexingPolicy.ExcludedPaths.Clear();
                foreach (object item in lbExcludedPath.Items)
                {
                    String excludedPath = item as String;
                    _collectionToCreate.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath {Path = excludedPath});
                }

                _collectionToCreate.Id = tbCollectionId.Text;
                _currentOperation(null, _collectionToCreate);
            }
            else if (_currentCrudName.StartsWith("Create trigger", StringComparison.OrdinalIgnoreCase))
            {
                Trigger trigger = new Trigger();
                trigger.Body = tbCrudContext.Text;
                trigger.Id = textBoxforId.Text;
                trigger.TriggerOperation = TriggerOperation.All;
                if (rbPreTrigger.Checked)
                    trigger.TriggerType = TriggerType.Pre;
                else if (rbPostTrigger.Checked)
                    trigger.TriggerType = TriggerType.Post;

                _currentOperation(null, trigger);
            }
            else
            {
                if (!string.IsNullOrEmpty(tbCrudContext.SelectedText))
                {
                    _currentOperation(tbCrudContext.SelectedText, textBoxforId.Text);
                }
                else
                {
                    if (_currentCrudName.StartsWith("Execute StoredProcedure", StringComparison.Ordinal) &&
                        !tbCrudContext.Modified)
                    {
                        _currentOperation(null, textBoxforId.Text);
                    }
                    else
                    {
                        _currentOperation(tbCrudContext.Text, textBoxforId.Text);
                    }
                }
            }
        }

        private void PrettyPrintJson(string json, string extraText)
        {
            if (string.IsNullOrEmpty(json))
            {
                json = "\"\"";
            }
            string prettyPrint = _prettyJsonTemplate.Replace("&JSONSTRINGREPLACE&", json);

            if (string.IsNullOrEmpty(extraText))
            {
                extraText = "";
            }

            prettyPrint = prettyPrint.Replace("&EXTRASTRINGREPLACE&", Helper.FormatTextAsHtml(extraText, false, false));

            // save prettyePrint to file.
            string prettyPrintHtml = Path.Combine(_appTempPath, "prettyPrint.Html");

            using (StreamWriter outfile = new StreamWriter(prettyPrintHtml))
            {
                outfile.Write(prettyPrint);
            }

            // now launch it in broswer!
            webBrowserResponse.Url = new Uri(prettyPrintHtml);
        }

        private void btnExecuteNext_Click(object sender, EventArgs e)
        {
            SetLoadingState();


            if (!string.IsNullOrEmpty(tbCrudContext.SelectedText))
            {
                _currentOperation(tbCrudContext.SelectedText, textBoxforId.Text);
            }
            else
            {
                _currentOperation(tbCrudContext.Text, textBoxforId.Text);
            }
        }

        private void cbRequestOptionsApply_CheckedChanged(object sender, EventArgs e)
        {
            if (cbRequestOptionsApply.Checked)
            {
                rbIndexingDefault.Enabled = false;
                rbIndexingExclude.Enabled = false;
                rbIndexingInclude.Enabled = false;

                rbAccessConditionIfMatch.Enabled = false;
                rbAccessConditionIfNoneMatch.Enabled = false;
                tbAccessConditionText.Enabled = false;

                rbConsistencyBound.Enabled = false;
                rbConsistencyEventual.Enabled = false;
                rbConsistencySession.Enabled = false;
                rbConsistencyStrong.Enabled = false;

                tbPreTrigger.Enabled = false;
                tbPostTrigger.Enabled = false;

                _requestOptions = null;
            }
            else
            {
                rbIndexingDefault.Enabled = true;
                rbIndexingExclude.Enabled = true;
                rbIndexingInclude.Enabled = true;

                rbAccessConditionIfMatch.Enabled = true;
                rbAccessConditionIfNoneMatch.Enabled = true;
                tbAccessConditionText.Enabled = true;

                rbConsistencyEventual.Enabled = true;
                rbConsistencyBound.Enabled = true;
                rbConsistencySession.Enabled = true;
                rbConsistencyStrong.Enabled = true;

                tbPreTrigger.Enabled = true;
                tbPostTrigger.Enabled = true;

                CreateDefaultRequestOptions();
            }
        }

        private void CreateDefaultRequestOptions()
        {
            _requestOptions = new RequestOptions();

            if (rbIndexingDefault.Checked)
            {
                _requestOptions.IndexingDirective = IndexingDirective.Default;
            }
            else if (rbIndexingExclude.Checked)
            {
                _requestOptions.IndexingDirective = IndexingDirective.Exclude;
            }
            else if (rbIndexingInclude.Checked)
            {
                _requestOptions.IndexingDirective = IndexingDirective.Include;
            }

            _requestOptions.AccessCondition = new AccessCondition();
            if (rbAccessConditionIfMatch.Checked)
            {
                _requestOptions.AccessCondition.Type = AccessConditionType.IfMatch;
            }
            else if (rbAccessConditionIfNoneMatch.Checked)
            {
                _requestOptions.AccessCondition.Type = AccessConditionType.IfNoneMatch;
            }

            string condition = tbAccessConditionText.Text;
            if (!string.IsNullOrEmpty(condition))
            {
                _requestOptions.AccessCondition.Condition = condition;
            }

            if (rbConsistencyStrong.Checked)
            {
                _requestOptions.ConsistencyLevel = ConsistencyLevel.Strong;
            }
            else if (rbConsistencyBound.Checked)
            {
                _requestOptions.ConsistencyLevel = ConsistencyLevel.BoundedStaleness;
            }
            else if (rbConsistencySession.Checked)
            {
                _requestOptions.ConsistencyLevel = ConsistencyLevel.Session;
            }
            else if (rbConsistencyEventual.Checked)
            {
                _requestOptions.ConsistencyLevel = ConsistencyLevel.Eventual;
            }

            string preTrigger = tbPreTrigger.Text;
            if (!string.IsNullOrEmpty(preTrigger))
            {
                // split by ;
                string[] segments = preTrigger.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);
                _requestOptions.PreTriggerInclude = segments;
            }

            string postTrigger = tbPostTrigger.Text;
            if (!string.IsNullOrEmpty(postTrigger))
            {
                // split by ;
                string[] segments = postTrigger.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);
                _requestOptions.PostTriggerInclude = segments;
            }
        }

        private void rbIndexingDefault_CheckedChanged(object sender, EventArgs e)
        {
            _requestOptions.IndexingDirective = IndexingDirective.Default;
        }

        private void rbIndexingInclude_CheckedChanged(object sender, EventArgs e)
        {
            _requestOptions.IndexingDirective = IndexingDirective.Include;
        }

        private void rbIndexingExclude_CheckedChanged(object sender, EventArgs e)
        {
            _requestOptions.IndexingDirective = IndexingDirective.Exclude;
        }

        private void rbAccessConditionIfMatch_CheckedChanged(object sender, EventArgs e)
        {
            _requestOptions.AccessCondition.Type = AccessConditionType.IfMatch;
        }

        private void rbAccessConditionIfNoneMatch_CheckedChanged(object sender, EventArgs e)
        {
            _requestOptions.AccessCondition.Type = AccessConditionType.IfNoneMatch;
        }

        private void rbConsistencyStrong_CheckedChanged(object sender, EventArgs e)
        {
            _requestOptions.ConsistencyLevel = ConsistencyLevel.Strong;
        }

        private void rbConsistencyBound_CheckedChanged(object sender, EventArgs e)
        {
            _requestOptions.ConsistencyLevel = ConsistencyLevel.BoundedStaleness;
        }

        private void rbConsistencySession_CheckedChanged(object sender, EventArgs e)
        {
            _requestOptions.ConsistencyLevel = ConsistencyLevel.Session;
        }

        private void rbConsistencyEventual_CheckedChanged(object sender, EventArgs e)
        {
            _requestOptions.ConsistencyLevel = ConsistencyLevel.Eventual;
        }

        private void btnAddIncludePath_Click(object sender, EventArgs e)
        {
            IncludedPathForm dlg = new IncludedPathForm();
            dlg.StartPosition = FormStartPosition.CenterParent;
            DialogResult dr = dlg.ShowDialog(this);
            if (dr == DialogResult.OK)
            {
                lbIncludedPath.Items.Add(dlg.IncludedPath);
            }
        }

        private void btnRemovePath_Click(object sender, EventArgs e)
        {
            lbIncludedPath.Items.RemoveAt(lbIncludedPath.SelectedIndex);
        }

        private void lbIncludedPath_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbIncludedPath.SelectedItem != null)
            {
                btnEdit.Enabled = true;
                btnRemovePath.Enabled = true;
            }
            else
            {
                btnEdit.Enabled = false;
                btnRemovePath.Enabled = false;
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            IncludedPath includedPath = lbIncludedPath.SelectedItem as IncludedPath;

            IncludedPathForm dlg = new IncludedPathForm();
            dlg.StartPosition = FormStartPosition.CenterParent;

            dlg.SetIncludedPath(includedPath);

            DialogResult dr = dlg.ShowDialog(this);
            if (dr == DialogResult.OK)
            {
                lbIncludedPath.Items[lbIncludedPath.SelectedIndex] = dlg.IncludedPath;
            }
        }

        private void btnAddExcludedPath_Click(object sender, EventArgs e)
        {
            ExcludedPathForm dlg = new ExcludedPathForm();
            DialogResult dr = dlg.ShowDialog(this);
            if (dr == DialogResult.OK)
            {
                lbExcludedPath.Items.Add(dlg.ExcludedPath);
            }
        }

        private void btnRemoveExcludedPath_Click(object sender, EventArgs e)
        {
            lbExcludedPath.Items.RemoveAt(lbExcludedPath.SelectedIndex);
        }

        private void lbExcludedPath_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnRemoveExcludedPath.Enabled = lbExcludedPath.SelectedItem != null;
        }

        private void cbIndexingPolicyDefault_CheckedChanged(object sender, EventArgs e)
        {
            if (cbIndexingPolicyDefault.Checked)
            {
                cbAutomatic.Enabled = false;
                rbConsistent.Enabled = false;
                rbLazy.Enabled = false;

                lbIncludedPath.Enabled = false;
                btnAddIncludePath.Enabled = false;
                btnRemovePath.Enabled = false;
                btnEdit.Enabled = false;

                lbExcludedPath.Enabled = false;
                btnAddExcludedPath.Enabled = false;
                btnRemoveExcludedPath.Enabled = false;

                _collectionToCreate = new DocumentCollection();
            }
            else
            {
                cbAutomatic.Enabled = true;
                rbConsistent.Enabled = true;
                rbLazy.Enabled = true;

                lbIncludedPath.Enabled = true;
                btnAddIncludePath.Enabled = true;
                btnRemovePath.Enabled = false;
                btnEdit.Enabled = false;

                lbExcludedPath.Enabled = true;
                btnAddExcludedPath.Enabled = true;
                btnRemoveExcludedPath.Enabled = false;

                CreateDefaultIndexingPolicy();
            }
        }


        private void CreateDefaultIndexingPolicy()
        {
            _collectionToCreate.IndexingPolicy.Automatic = cbAutomatic.Checked;

            if (rbConsistent.Checked)
            {
                _collectionToCreate.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            }
            else
            {
                _collectionToCreate.IndexingPolicy.IndexingMode = IndexingMode.Lazy;
            }
        }

        private void cbAutomatic_CheckedChanged(object sender, EventArgs e)
        {
            _collectionToCreate.IndexingPolicy.Automatic = cbAutomatic.Checked;
        }

        private void rbConsistent_CheckedChanged(object sender, EventArgs e)
        {
            if (rbConsistent.Checked)
            {
                _collectionToCreate.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            }
            else
            {
                _collectionToCreate.IndexingPolicy.IndexingMode = IndexingMode.Lazy;
            }
        }

        private void rbLazy_CheckedChanged(object sender, EventArgs e)
        {
            _collectionToCreate.IndexingPolicy.IndexingMode = rbConsistent.Checked
                ? IndexingMode.Consistent
                : IndexingMode.Lazy;
        }

        private void rbOfferS1_CheckedChanged(object sender, EventArgs e)
        {
            _offerType = "S1";
        }

        private void rbOfferS2_CheckedChanged(object sender, EventArgs e)
        {
            _offerType = "S2";
        }

        private void rbOfferS3_CheckedChanged(object sender, EventArgs e)
        {
            _offerType = "S3";
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            RenderJson(e.Node);
        }

        private delegate DialogResult MessageBoxDelegate(
            string msg, string title, MessageBoxButtons buttons, MessageBoxIcon icon);
    }
}