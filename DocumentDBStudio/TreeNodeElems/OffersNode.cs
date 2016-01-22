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
    class OffersNode : NodeBase
    {
        private readonly DocumentClient _client;
        private readonly ContextMenu _contextMenu = new ContextMenu();

        public OffersNode(DocumentClient client)
        {
            Text = "Offers";
            this._client = client;
            Nodes.Add(new TreeNode("fake"));
            Tag = "This represents the Offers feed.";
            ImageKey = "Offer";
            SelectedImageKey = "Offer";

            MenuItem myMenuItem1 = new MenuItem("Refresh Offer feed");
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
                FeedResponse<Offer> feedOffers = _client.ReadOffersFeedAsync().Result;

                foreach (var sp in feedOffers)
                {
                    DocumentNode nodeBase = new DocumentNode(_client, sp, ResourceType.Offer);
                    Nodes.Add(nodeBase);
                }
                Program.GetMain().SetResponseHeaders(feedOffers.ResponseHeaders);
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

        void myMenuItemQueryOffers_Click(object sender, EventArgs e)
        {
            Program.GetMain().SetCrudContext(this, "Query Offers",
                false, "select * from c", QueryOffers);
        }

        async void QueryOffers(string queryText, object optional)
        {
            try
            {
                // text is the querytext.
                IDocumentQuery<dynamic> q = _client.CreateOfferQuery(queryText).AsDocumentQuery();

                FeedResponse<Database> r;
                using (PerfStatus.Start("QueryOffer"))
                {
                    r = await q.ExecuteNextAsync<Database>();
                }
                // set the result window
                string text = null;
                if (r.Count > 1)
                {
                    text = string.Format(CultureInfo.InvariantCulture, "Returned {0} Offers", r.Count);
                }
                else
                {
                    text = string.Format(CultureInfo.InvariantCulture, "Returned {0} Offer", r.Count);
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