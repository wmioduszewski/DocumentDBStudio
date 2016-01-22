using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Microsoft.Azure.DocumentDBStudio.Util;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.DocumentDBStudio.TreeNodeElems
{
    class UdfNode : NodeBase
    {
        private readonly DocumentClient _client;
        private readonly ContextMenu _contextMenu = new ContextMenu();

        public UdfNode(DocumentClient client)
        {
            Text = "UDFs";
            this._client = client;
            Nodes.Add(new TreeNode("fake"));
            Tag = "This represents the UserDefinedFunction feed. Right click to add UserDefinedFunction";
            ImageKey = "Feed";
            SelectedImageKey = "Feed";

            MenuItem myMenuItem = new MenuItem("Create UserDefinedFunction");
            myMenuItem.Click += myMenuItemAddUDF_Click;
            _contextMenu.MenuItems.Add(myMenuItem);
            MenuItem myMenuItem2 = new MenuItem("Create UserDefinedFunction from File");
            myMenuItem2.Click += myMenuItemAddUDFFromFile_Click;
            _contextMenu.MenuItems.Add(myMenuItem2);
            MenuItem myMenuItem1 = new MenuItem("Refresh UserDefinedFunction feed");
            myMenuItem1.Click += (sender, e) => Refresh(true);
            _contextMenu.MenuItems.Add(myMenuItem1);
        }

        public override void ShowContextMenu(TreeView treeview, Point p)
        {
            _contextMenu.Show(treeview, p);
        }

        public override void Refresh(bool forceRefresh)
        {
            if (forceRefresh || IsFirstTime)
            {
                IsFirstTime = false;
                Nodes.Clear();
                FillWithChildren();
            }
        }

        public void FillWithChildren()
        {
            try
            {
                DocumentCollectionNode collnode = (DocumentCollectionNode) Parent;
                FeedResponse<UserDefinedFunction> sps;
                using (PerfStatus.Start("ReadUdfFeed"))
                {
                    sps =
                        _client.ReadUserDefinedFunctionFeedAsync((collnode.Tag as DocumentCollection).GetLink(_client))
                            .Result;
                }

                foreach (var sp in sps)
                {
                    DocumentNode nodeBase = new DocumentNode(_client, sp, ResourceType.UserDefinedFunction);
                    Nodes.Add(nodeBase);
                }
                Program.GetMain().SetResponseHeaders(sps.ResponseHeaders);
            }
            catch (AggregateException e)
            {
                Program.GetMain().SetResultInBrowser(null, e.InnerException.ToString(), true);
            }
            catch (Exception e)
            {
                Program.GetMain().SetResultInBrowser(null, e.ToString(), true);
            }
        }

        void myMenuItemAddUDF_Click(object sender, EventArgs e)
        {
            Program.GetMain()
                .SetCrudContext(this,
                    string.Format(CultureInfo.InvariantCulture, "Add UserDefinedFunction in collection {0}",
                        (Parent.Tag as DocumentCollection).Id),
                    true, "function() { \r\n \r\n}", AddUDF);
        }

        void myMenuItemAddUDFFromFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            DialogResult dr = ofd.ShowDialog();

            if (dr == DialogResult.OK)
            {
                string filename = ofd.FileName;
                string text = File.ReadAllText(filename);

                Program.GetMain().SetCrudContext(this, "Create UDF", false, text, AddUDF);
            }
        }

        async void AddUDF(string body, object idObject)
        {
            string id = idObject as string;
            try
            {
                UserDefinedFunction udf = new UserDefinedFunction();
                udf.Body = body;
                udf.Id = id;

                ResourceResponse<UserDefinedFunction> newudf;
                using (PerfStatus.Start("CreateUDF"))
                {
                    newudf =
                        await
                            _client.CreateUserDefinedFunctionAsync((Parent.Tag as DocumentCollection).GetLink(_client),
                                udf, Program.GetMain().GetRequestOptions());
                }

                Nodes.Add(new DocumentNode(_client, newudf.Resource, ResourceType.UserDefinedFunction));

                // set the result window
                string json = newudf.Resource.ToString();

                Program.GetMain().SetResultInBrowser(json, null, false, newudf.ResponseHeaders);
            }
            catch (AggregateException e)
            {
                Program.GetMain().SetResultInBrowser(null, e.InnerException.ToString(), true);
            }
            catch (Exception e)
            {
                Program.GetMain().SetResultInBrowser(null, e.ToString(), true);
            }
        }
    }
}