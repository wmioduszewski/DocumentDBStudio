using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Azure.DocumentDBStudio.Util;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;

namespace Microsoft.Azure.DocumentDBStudio.TreeNodeElems
{
    class ConflictsNode : NodeBase
    {
        private readonly DocumentClient _client;
        private readonly ContextMenu _contextMenu = new ContextMenu();

        public ConflictsNode(DocumentClient client)
        {
            Text = "Conflicts";
            this._client = client;
            Nodes.Add(new TreeNode("fake"));
            Tag = "This represents the Conflicts feed.";
            ImageKey = "Conflict";
            SelectedImageKey = "Conflict";

            MenuItem myMenuItem1 = new MenuItem("Refresh Conflict feed");
            myMenuItem1.Click += (sender, e) => Refresh(true);
            _contextMenu.MenuItems.Add(myMenuItem1);

            // Query conflicts currrently fail due to gateway
            MenuItem myMenuItem2 = new MenuItem("Query Conflict feed");
            myMenuItem2.Click += myMenuItemQueryConflicts_Click;
            _contextMenu.MenuItems.Add(myMenuItem2);
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
                FeedResponse<Conflict> feedConflicts;
                using (PerfStatus.Start("ReadConflictsFeed"))
                {
                    feedConflicts =
                        _client.ReadConflictFeedAsync((Parent.Tag as DocumentCollection).GetLink(_client)).Result;
                }

                foreach (var sp in feedConflicts)
                {
                    DocumentNode nodeBase = new DocumentNode(_client, sp, ResourceType.Conflict);
                    Nodes.Add(nodeBase);
                }
                Program.GetMain().SetResponseHeaders(feedConflicts.ResponseHeaders);
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

        void myMenuItemQueryConflicts_Click(object sender, EventArgs e)
        {
            Program.GetMain().SetCrudContext(this, "Query Conflicts",
                false, "select * from c", QueryConflicts);
        }

        async void QueryConflicts(string queryText, object optional)
        {
            try
            {
                // text is the querytext.
                FeedResponse<Database> r;
                using (PerfStatus.Start("QueryConflicts"))
                {
                    IDocumentQuery<dynamic> q =
                        _client.CreateConflictQuery((Parent.Tag as DocumentCollection).GetLink(_client), queryText)
                            .AsDocumentQuery();
                    r = await q.ExecuteNextAsync<Database>();
                }

                // set the result window
                string text = null;
                if (r.Count > 1)
                {
                    text = string.Format(CultureInfo.InvariantCulture, "Returned {0} Conflict", r.Count);
                }
                else
                {
                    text = string.Format(CultureInfo.InvariantCulture, "Returned {0} Conflict", r.Count);
                }

                string jsonarray = "[";
                int index = 0;
                foreach (dynamic d in r)
                {
                    index++;
                    // currently Query.ToString() has Formatting.Indented, but the public release doesn't have yet.
                    jsonarray += d.ToString();

                    if (index == r.Count)
                    {
                        jsonarray += "]";
                    }
                    else
                    {
                        jsonarray += ",\r\n";
                    }
                }

                Program.GetMain().SetResultInBrowser(jsonarray, text, true, r.ResponseHeaders);
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