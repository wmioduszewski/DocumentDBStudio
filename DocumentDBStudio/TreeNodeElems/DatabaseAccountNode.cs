using System;
using System.Drawing;
using System.Dynamic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;

namespace Microsoft.Azure.DocumentDBStudio.TreeNodeElems
{
    class DatabaseAccountNode : NodeBase
    {
        private readonly string _accountEndpoint;
        private readonly DocumentClient _client;
        private readonly ContextMenu _contextMenu = new ContextMenu();

        public DatabaseAccountNode(string endpointName, DocumentClient client)
        {
            _accountEndpoint = endpointName;

            Text = endpointName;

            ImageKey = "DatabaseAccount";
            SelectedImageKey = "DatabaseAccount";

            _client = client;
            Tag = "This represents the DatabaseAccount. Right click to add Database";

            Nodes.Add(new OffersNode(this._client));

            MenuItem myMenuItem = new MenuItem("Create Database");
            myMenuItem.Click += myMenuItemAddDatabase_Click;
            _contextMenu.MenuItems.Add(myMenuItem);

            MenuItem myMenuItem1 = new MenuItem("Refresh Databases feed");
            myMenuItem1.Click += (sender, e) => Refresh(true);
            _contextMenu.MenuItems.Add(myMenuItem1);

            MenuItem myMenuItem4 = new MenuItem("Query Database");
            myMenuItem4.Click += myMenuItemQueryDatabase_Click;
            _contextMenu.MenuItems.Add(myMenuItem4);

            _contextMenu.MenuItems.Add("-");

            MenuItem myMenuItem2 = new MenuItem("Remove setting");
            myMenuItem2.Click += myMenuItemRemoveDatabaseAccount_Click;
            _contextMenu.MenuItems.Add(myMenuItem2);

            MenuItem myMenuItem3 = new MenuItem("Change setting");
            myMenuItem3.Click += myMenuItemChangeSetting_Click;
            _contextMenu.MenuItems.Add(myMenuItem3);
        }

        public DocumentClient DocumentClient
        {
            get { return _client; }
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

                Nodes.Add(new OffersNode(_client));

                FillWithChildren();
            }
        }

        void myMenuItemChangeSetting_Click(object sender, EventArgs e)
        {
            Program.GetMain().ChangeAccountSettings(this, _accountEndpoint);
        }

        void myMenuItemAddDatabase_Click(object sender, EventArgs e)
        {
            dynamic d = new ExpandoObject();
            d.id = "Here is your Database Id";
            string x = JsonConvert.SerializeObject(d, Formatting.Indented);
            Program.GetMain().SetCrudContext(this, "Create database", false, x, AddDatabase);
        }

        void myMenuItemQueryDatabase_Click(object sender, EventArgs e)
        {
            Program.GetMain().SetCrudContext(this, "Query Database",
                false, "select * from c", QueryDatabases);
        }

        void myMenuItemRemoveDatabaseAccount_Click(object sender, EventArgs e)
        {
            Remove();
            Program.GetMain().RemoveAccountFromSettings(_accountEndpoint);
        }

        async Task QueryDatabases(string queryText, object optional)
        {
            try
            {
                FeedResponse<Database> r;
                using (PerfStatus.Start("QueryDatabase"))
                {
                    // text is the querytext.
                    IDocumentQuery<dynamic> q = _client.CreateDatabaseQuery(queryText).AsDocumentQuery();
                    r = await q.ExecuteNextAsync<Database>();
                }

                // set the result window
                string text = null;
                if (r.Count > 1)
                {
                    text = string.Format(CultureInfo.InvariantCulture, "Returned {0} dataqbases", r.Count);
                }
                else
                {
                    text = string.Format(CultureInfo.InvariantCulture, "Returned {0} dataqbases", r.Count);
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

        private async Task FillWithChildren()
        {
            try
            {
                Tag = await _client.GetDatabaseAccountAsync();

                FeedResponse<Database> databases;
                using (PerfStatus.Start("ReadDatabaseFeed"))
                {
                    databases = await _client.ReadDatabaseFeedAsync();
                }

                foreach (Database db in databases)
                {
                    DatabaseNode nodeBase = new DatabaseNode(_client, db);
                    Nodes.Add(nodeBase);
                }

                Program.GetMain().SetResponseHeaders(databases.ResponseHeaders);
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

        async Task AddDatabase(string text, object optional)
        {
            try
            {
                Database db = (Database)JsonConvert.DeserializeObject(text, typeof(Database));

                ResourceResponse<Database> newdb;
                using (PerfStatus.Start("CreateDatabase"))
                {
                    newdb = await _client.CreateDatabaseAsync(db, Program.GetMain().GetRequestOptions());
                }
                Nodes.Add(new DatabaseNode(_client, newdb.Resource));

                // set the result window
                string json = JsonConvert.SerializeObject(newdb.Resource, Formatting.Indented);

                Program.GetMain().SetResultInBrowser(json, null, false, newdb.ResponseHeaders);
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