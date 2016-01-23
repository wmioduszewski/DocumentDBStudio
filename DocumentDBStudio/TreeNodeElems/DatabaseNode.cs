using System;
using System.Drawing;
using System.Dynamic;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Azure.DocumentDBStudio.Util;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;

namespace Microsoft.Azure.DocumentDBStudio.TreeNodeElems
{
    class DatabaseNode : NodeBase
    {
        private readonly DocumentClient _client;
        private readonly ContextMenu _contextMenu = new ContextMenu();

        public DatabaseNode(DocumentClient localclient, Database db)
        {
            Text = db.Id;
            Tag = db;
            _client = localclient;
            ImageKey = "SystemFeed";
            SelectedImageKey = "SystemFeed";

            Nodes.Add(new UsersNode(_client));

            MenuItem myMenuItem3 = new MenuItem("Read Database");
            myMenuItem3.Click += new EventHandler(myMenuItemReadDatabase_Click);
            _contextMenu.MenuItems.Add(myMenuItem3);

            MenuItem myMenuItem = new MenuItem("Delete Database");
            myMenuItem.Click += new EventHandler(myMenuItemDeleteDatabase_Click);
            _contextMenu.MenuItems.Add(myMenuItem);

            _contextMenu.MenuItems.Add("-");

            MenuItem myMenuItem2 = new MenuItem("Create DocumentCollection");
            myMenuItem2.Click += new EventHandler(myMenuItemAddDocumentCollection_Click);
            _contextMenu.MenuItems.Add(myMenuItem2);
            MenuItem myMenuItem4 = new MenuItem("Refresh DocumentCollections Feed");
            myMenuItem4.Click += new EventHandler((sender, e) => Refresh(true));
            _contextMenu.MenuItems.Add(myMenuItem4);
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

                Nodes.Add(new UsersNode(_client));

                FillWithChildren();
            }
        }

        public async Task FillWithChildren()
        {
            try
            {
                FeedResponse<DocumentCollection> colls;
                using (PerfStatus.Start("ReadDocumentCollectionFeed"))
                {
                    colls = await _client.ReadDocumentCollectionFeedAsync(((Database)Tag).GetLink(_client));
                }

                foreach (DocumentCollection coll in colls)
                {
                    DocumentCollectionNode nodeBase = new DocumentCollectionNode(_client, coll);
                    Nodes.Add(nodeBase);
                }

                Program.GetMain().SetResponseHeaders(colls.ResponseHeaders);
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

        async void myMenuItemReadDatabase_Click(object sender, EventArgs eArgs)
        {
            try
            {
                ResourceResponse<Database> database;
                using (PerfStatus.Start("ReadDatabase"))
                {
                    database =
                        await
                            _client.ReadDatabaseAsync(((Database)Tag).GetLink(_client),
                                Program.GetMain().GetRequestOptions());
                }
                // set the result window
                string json = JsonConvert.SerializeObject(database.Resource, Formatting.Indented);

                Program.GetMain().SetResultInBrowser(json, null, false, database.ResponseHeaders);
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

        void myMenuItemDeleteDatabase_Click(object sender, EventArgs e)
        {
            string x = Tag.ToString();
            CommandContext context = new CommandContext();
            context.IsDelete = true;
            Program.GetMain().SetCrudContext(this, "Delete database", false, x, DeleteDatabase, context);
        }

        void myMenuItemAddDocumentCollection_Click(object sender, EventArgs e)
        {
            dynamic d = new ExpandoObject();
            d.id = "Here is your DocumentCollection Id";

            string x = JsonConvert.SerializeObject(d, Formatting.Indented);
            Program.GetMain().SetCrudContext(this, "Create documentCollection", false, x, AddDocumentCollection);
        }

        async Task AddDocumentCollection(string text, object optional)
        {
            try
            {
                DocumentCollection coll = optional as DocumentCollection;
                Database db = (Database)Tag;
                ResourceResponse<DocumentCollection> newcoll;
                using (PerfStatus.Start("CreateDocumentCollection"))
                {
                    newcoll =
                        await
                            _client.CreateDocumentCollectionAsync(db.GetLink(_client), coll,
                                Program.GetMain().GetRequestOptions(true));
                }

                // set the result window
                string json = JsonConvert.SerializeObject(newcoll.Resource, Formatting.Indented);

                Program.GetMain().SetResultInBrowser(json, null, false, newcoll.ResponseHeaders);

                Nodes.Add(new DocumentCollectionNode(_client, newcoll.Resource));
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

        async Task DeleteDatabase(string text, object optional)
        {
            try
            {
                Database db = (Database)Tag;
                ResourceResponse<Database> newdb;
                using (PerfStatus.Start("DeleteDatabase"))
                {
                    newdb = await _client.DeleteDatabaseAsync(db.GetLink(_client), Program.GetMain().GetRequestOptions());
                }

                Program.GetMain().SetResultInBrowser(null, "Delete database succeed!", false, newdb.ResponseHeaders);

                Remove();
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