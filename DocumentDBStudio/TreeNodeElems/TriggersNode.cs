using System;
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
    class TriggersNode : NodeBase
    {
        private readonly DocumentClient _client;
        private readonly ContextMenu _contextMenu = new ContextMenu();

        public TriggersNode(DocumentClient client)
        {
            Text = "Triggers";
            this._client = client;
            Nodes.Add(new TreeNode("fake"));
            Tag = "This represents the Triggers feed. Right click to add Trigger";
            ImageKey = "Feed";
            SelectedImageKey = "Feed";

            MenuItem myMenuItem = new MenuItem("Create Trigger");
            myMenuItem.Click += myMenuItemAddTrigger_Click;
            _contextMenu.MenuItems.Add(myMenuItem);
            MenuItem myMenuItem2 = new MenuItem("Create Trigger from file");
            myMenuItem2.Click += myMenuItemAddTriggerFromFile_Click;
            _contextMenu.MenuItems.Add(myMenuItem2);
            MenuItem myMenuItem1 = new MenuItem("Refresh Triggers feed");
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
                FeedResponse<Trigger> sps;
                using (PerfStatus.Start("ReadTriggerFeed"))
                {
                    sps = _client.ReadTriggerFeedAsync((collnode.Tag as DocumentCollection).GetLink(_client)).Result;
                }

                foreach (var sp in sps)
                {
                    DocumentNode nodeBase = new DocumentNode(_client, sp, ResourceType.Trigger);
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

        void myMenuItemAddTriggerFromFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            DialogResult dr = ofd.ShowDialog();

            if (dr == DialogResult.OK)
            {
                string filename = ofd.FileName;
                string text = File.ReadAllText(filename);

                Program.GetMain()
                    .SetCrudContext(this, "Create trigger", false, text, AddTrigger,
                        new CommandContext {IsCreateTrigger = true});
            }
        }

        void myMenuItemAddTrigger_Click(object sender, EventArgs e)
        {
            Program.GetMain()
                .SetCrudContext(this,
                    string.Format(CultureInfo.InvariantCulture, "Create trigger in collection {0}",
                        (Parent.Tag as DocumentCollection).Id),
                    true, "function() { \r\n \r\n}", AddTrigger, new CommandContext {IsCreateTrigger = true});
        }

        async Task AddTrigger(string body, object triggerObject)
        {
            try
            {
                Trigger trigger = triggerObject as Trigger;

                ResourceResponse<Trigger> newtrigger;
                using (PerfStatus.Start("CreateTrigger"))
                {
                    newtrigger =
                        await
                            _client.CreateTriggerAsync((Parent.Tag as DocumentCollection).GetLink(_client), trigger,
                                Program.GetMain().GetRequestOptions());
                }

                Nodes.Add(new DocumentNode(_client, newtrigger.Resource, ResourceType.Trigger));

                // set the result window
                string json = newtrigger.Resource.ToString();

                Program.GetMain().SetResultInBrowser(json, null, false, newtrigger.ResponseHeaders);
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