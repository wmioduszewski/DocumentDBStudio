﻿using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Azure.DocumentDBStudio.Util;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.DocumentDBStudio.TreeNodeElems
{
    class StoredProceduresNode : NodeBase
    {
        private readonly DocumentClient _client;
        private readonly ContextMenu _contextMenu = new ContextMenu();

        public StoredProceduresNode(DocumentClient client)
        {
            Text = "StoredProcedures";
            this._client = client;
            Nodes.Add(new TreeNode("fake"));
            Tag = "This represents the StoredProcedure feed. Right click to add StoredProcedure";

            ImageKey = "Feed";
            SelectedImageKey = "Feed";

            MenuItem myMenuItem = new MenuItem("Create StoredProcedure");
            myMenuItem.Click += myMenuItemAddStoredProcedure_Click;
            _contextMenu.MenuItems.Add(myMenuItem);
            MenuItem myMenuItem2 = new MenuItem("Create StoredProcedure From File");
            myMenuItem2.Click += myMenuItemAddStoredProcedureFromFile_Click;
            _contextMenu.MenuItems.Add(myMenuItem2);

            MenuItem myMenuItem1 = new MenuItem("Refresh StoredProcedures feed");
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
                FeedResponse<StoredProcedure> sps;
                using (PerfStatus.Start("ReadStoredProcedure"))
                {
                    sps =
                        _client.ReadStoredProcedureFeedAsync((collnode.Tag as DocumentCollection).GetLink(_client)).Result;
                }

                foreach (var sp in sps)
                {
                    DocumentNode nodeBase = new DocumentNode(_client, sp, ResourceType.StoredProcedure);
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

        void myMenuItemAddStoredProcedure_Click(object sender, EventArgs e)
        {
            // 
            Program.GetMain()
                .SetCrudContext(this,
                    string.Format(CultureInfo.InvariantCulture, "Create StoredProcedure in collection {0}",
                        (Parent.Tag as DocumentCollection).Id),
                    true, "function() { \r\n \r\n}", AddStoredProcedure);
        }

        void myMenuItemAddStoredProcedureFromFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            DialogResult dr = ofd.ShowDialog();

            if (dr == DialogResult.OK)
            {
                string filename = ofd.FileName;
                // 
                string text = File.ReadAllText(filename);

                Program.GetMain().SetCrudContext(this, "Add StoredProcedure", false, text, AddStoredProcedure);
            }
        }

        async Task AddStoredProcedure(string body, object idobject)
        {
            string id = idobject as string;
            try
            {
                StoredProcedure sp = new StoredProcedure();
                sp.Body = body;
                sp.Id = id;

                ResourceResponse<StoredProcedure> newsp;
                using (PerfStatus.Start("CreateStoredProcedure"))
                {
                    newsp =
                        await
                            _client.CreateStoredProcedureAsync((Parent.Tag as DocumentCollection).GetLink(_client), sp,
                                Program.GetMain().GetRequestOptions());
                }

                Nodes.Add(new DocumentNode(_client, newsp.Resource, ResourceType.StoredProcedure));

                // set the result window
                string json = newsp.Resource.ToString();

                Program.GetMain().SetResultInBrowser(json, null, false, newsp.ResponseHeaders);
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