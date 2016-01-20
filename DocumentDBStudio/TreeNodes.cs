//-----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.DocumentDBStudio
{
    internal class AccountSettings
    {
        public ConnectionMode ConnectionMode;
        public bool IsNameBased;
        public string MasterKey;
        public Protocol Protocol;
    }

    public class CommandContext
    {
        public bool HasContinuation;
        public bool IsCreateTrigger;
        public bool IsDelete;
        public bool IsFeed;
        public bool QueryStarted;

        public CommandContext()
        {
            IsDelete = false;
            IsFeed = false;
            HasContinuation = false;
            QueryStarted = false;
            IsCreateTrigger = false;
        }
    }

    enum ResourceType
    {
        Document,
        User,
        StoredProcedure,
        UserDefinedFunction,
        Trigger,
        Permission,
        Attachment,
        Conflict,
        Offer
    }

    abstract class FeedNode : TreeNode
    {
        protected bool isFirstTime = true;
        abstract public void ShowContextMenu(TreeView treeview, Point p);

        abstract public void Refresh(bool forceRefresh);
    }

    class TreeNodeConstants
    {
        static public string LoadingNode = "Loading";
    }

    class DatabaseAccountNode : FeedNode
    {
        private string accountEndpoint;
        private DocumentClient client;
        private ContextMenu contextMenu = new ContextMenu();

        public DatabaseAccountNode(string endpointName, DocumentClient client)
        {
            accountEndpoint = endpointName;

            Text = endpointName;

            ImageKey = "DatabaseAccount";
            SelectedImageKey = "DatabaseAccount";

            this.client = client;
            Tag = "This represents the DatabaseAccount. Right click to add Database";

            Nodes.Add(new OffersNode(this.client));

            MenuItem myMenuItem = new MenuItem("Create Database");
            myMenuItem.Click += new EventHandler(myMenuItemAddDatabase_Click);
            contextMenu.MenuItems.Add(myMenuItem);

            MenuItem myMenuItem1 = new MenuItem("Refresh Databases feed");
            myMenuItem1.Click += new EventHandler((sender, e) => Refresh(true));
            contextMenu.MenuItems.Add(myMenuItem1);

            MenuItem myMenuItem4 = new MenuItem("Query Database");
            myMenuItem4.Click += new EventHandler(myMenuItemQueryDatabase_Click);
            contextMenu.MenuItems.Add(myMenuItem4);

            contextMenu.MenuItems.Add("-");

            MenuItem myMenuItem2 = new MenuItem("Remove setting");
            myMenuItem2.Click += new EventHandler(myMenuItemRemoveDatabaseAccount_Click);
            contextMenu.MenuItems.Add(myMenuItem2);
            MenuItem myMenuItem3 = new MenuItem("Change setting");
            myMenuItem3.Click += new EventHandler(myMenuItemChangeSetting_Click);
            contextMenu.MenuItems.Add(myMenuItem3);
        }

        public DocumentClient DocumentClient
        {
            get { return client; }
        }

        override public void ShowContextMenu(TreeView treeview, Point p)
        {
            contextMenu.Show(treeview, p);
        }

        override public void Refresh(bool forceRefresh)
        {
            if (forceRefresh || isFirstTime)
            {
                isFirstTime = false;
                Nodes.Clear();

                Nodes.Add(new OffersNode(client));

                FillWithChildren();
            }
        }

        void myMenuItemChangeSetting_Click(object sender, EventArgs e)
        {
            Program.GetMain().ChangeAccountSettings(this, accountEndpoint);
        }

        void myMenuItemAddDatabase_Click(object sender, EventArgs e)
        {
            // 
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
            // 
            Remove();
            Program.GetMain().RemoveAccountFromSettings(accountEndpoint);
        }

        async void QueryDatabases(string queryText, object optional)
        {
            try
            {
                FeedResponse<Database> r;
                using (PerfStatus.Start("QueryDatabase"))
                {
                    // text is the querytext.
                    IDocumentQuery<dynamic> q = client.CreateDatabaseQuery(queryText).AsDocumentQuery();
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

        private async void FillWithChildren()
        {
            try
            {
                Tag = await client.GetDatabaseAccountAsync();

                FeedResponse<Database> databases;
                using (PerfStatus.Start("ReadDatabaseFeed"))
                {
                    databases = await client.ReadDatabaseFeedAsync();
                }

                foreach (Database db in databases)
                {
                    DatabaseNode node = new DatabaseNode(client, db);
                    Nodes.Add(node);
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
            finally
            {
            }
        }

        async void AddDatabase(string text, object optional)
        {
            try
            {
                Database db = (Database) JsonConvert.DeserializeObject(text, typeof (Database));

                ResourceResponse<Database> newdb;
                using (PerfStatus.Start("CreateDatabase"))
                {
                    newdb = await client.CreateDatabaseAsync(db, Program.GetMain().GetRequestOptions());
                }
                Nodes.Add(new DatabaseNode(client, newdb.Resource));

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

    class DatabaseNode : FeedNode
    {
        private DocumentClient client;
        private ContextMenu contextMenu = new ContextMenu();

        public DatabaseNode(DocumentClient localclient, Database db)
        {
            Text = db.Id;
            Tag = db;
            client = localclient;
            ImageKey = "SystemFeed";
            SelectedImageKey = "SystemFeed";

            Nodes.Add(new UsersNode(client));

            MenuItem myMenuItem3 = new MenuItem("Read Database");
            myMenuItem3.Click += new EventHandler(myMenuItemReadDatabase_Click);
            contextMenu.MenuItems.Add(myMenuItem3);

            MenuItem myMenuItem = new MenuItem("Delete Database");
            myMenuItem.Click += new EventHandler(myMenuItemDeleteDatabase_Click);
            contextMenu.MenuItems.Add(myMenuItem);

            contextMenu.MenuItems.Add("-");

            MenuItem myMenuItem2 = new MenuItem("Create DocumentCollection");
            myMenuItem2.Click += new EventHandler(myMenuItemAddDocumentCollection_Click);
            contextMenu.MenuItems.Add(myMenuItem2);
            MenuItem myMenuItem4 = new MenuItem("Refresh DocumentCollections Feed");
            myMenuItem4.Click += new EventHandler((sender, e) => Refresh(true));
            contextMenu.MenuItems.Add(myMenuItem4);
        }

        public DocumentClient DocumentClient
        {
            get { return client; }
        }

        override public void ShowContextMenu(TreeView treeview, Point p)
        {
            contextMenu.Show(treeview, p);
        }

        override public void Refresh(bool forceRefresh)
        {
            if (forceRefresh || isFirstTime)
            {
                isFirstTime = false;
                Nodes.Clear();

                Nodes.Add(new UsersNode(client));

                FillWithChildren();
            }
        }

        async public void FillWithChildren()
        {
            try
            {
                FeedResponse<DocumentCollection> colls;
                using (PerfStatus.Start("ReadDocumentCollectionFeed"))
                {
                    colls = await client.ReadDocumentCollectionFeedAsync(((Database) Tag).GetLink(client));
                }

                foreach (DocumentCollection coll in colls)
                {
                    DocumentCollectionNode node = new DocumentCollectionNode(client, coll);
                    Nodes.Add(node);
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
                            client.ReadDatabaseAsync(((Database) Tag).GetLink(client),
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

        async void AddDocumentCollection(string text, object optional)
        {
            try
            {
                DocumentCollection coll = optional as DocumentCollection;
                Database db = (Database) Tag;
                ResourceResponse<DocumentCollection> newcoll;
                using (PerfStatus.Start("CreateDocumentCollection"))
                {
                    newcoll =
                        await
                            client.CreateDocumentCollectionAsync(db.GetLink(client), coll,
                                Program.GetMain().GetRequestOptions(true));
                }

                // set the result window
                string json = JsonConvert.SerializeObject(newcoll.Resource, Formatting.Indented);

                Program.GetMain().SetResultInBrowser(json, null, false, newcoll.ResponseHeaders);

                Nodes.Add(new DocumentCollectionNode(client, newcoll.Resource));
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

        async void DeleteDatabase(string text, object optional)
        {
            try
            {
                Database db = (Database) Tag;
                ResourceResponse<Database> newdb;
                using (PerfStatus.Start("DeleteDatabase"))
                {
                    newdb = await client.DeleteDatabaseAsync(db.GetLink(client), Program.GetMain().GetRequestOptions());
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

    class DocumentCollectionNode : FeedNode
    {
        private DocumentClient client;
        private ContextMenu contextMenu = new ContextMenu();
        private string currentContinuation = null;
        private CommandContext currentQueryCommandContext = null;

        public DocumentCollectionNode(DocumentClient client, DocumentCollection coll)
        {
            Text = coll.Id;
            Tag = coll;
            this.client = client;
            ImageKey = "SystemFeed";
            SelectedImageKey = "SystemFeed";

            Nodes.Add(new StoredProceduresNode(this.client));
            Nodes.Add(new UDFsNode(this.client));
            Nodes.Add(new TriggersNode(this.client));
            Nodes.Add(new ConflictsNode(this.client));

            MenuItem myMenuItem5 = new MenuItem("Read DocumentCollection");
            myMenuItem5.Click += new EventHandler(myMenuItemReadDocumentCollection_Click);
            contextMenu.MenuItems.Add(myMenuItem5);

            MenuItem myMenuItem3 = new MenuItem("Replace DocumentCollection");
            myMenuItem3.Click += new EventHandler(myMenuItemUpdateDocumentCollection_Click);
            contextMenu.MenuItems.Add(myMenuItem3);

            MenuItem myMenuItem6 = new MenuItem("Delete DocumentCollection");
            myMenuItem6.Click += new EventHandler(myMenuItemDeleteDocumentCollection_Click);
            contextMenu.MenuItems.Add(myMenuItem6);

            contextMenu.MenuItems.Add("-");

            MenuItem myMenuItem = new MenuItem("Create Document");
            myMenuItem.Click += new EventHandler(myMenuItemAddDocument_Click);
            contextMenu.MenuItems.Add(myMenuItem);
            MenuItem myMenuItem9 = new MenuItem("Create Document From File");
            myMenuItem9.Click += new EventHandler(myMenuItemAddDocumentFromFile_Click);
            contextMenu.MenuItems.Add(myMenuItem9);
            MenuItem myMenuItem4 = new MenuItem("Create Multiple Documents From Folder");
            myMenuItem4.Click += new EventHandler(myMenuItemAddDocumentsFromFolder_Click);
            contextMenu.MenuItems.Add(myMenuItem4);
            MenuItem myMenuItem1 = new MenuItem("Refresh Documents feed");
            myMenuItem1.Click += new EventHandler((sender, e) => Refresh(true));
            contextMenu.MenuItems.Add(myMenuItem1);
            MenuItem myMenuItem2 = new MenuItem("Query Documents");
            myMenuItem2.Click += new EventHandler(myMenuItemQueryDocument_Click);
            contextMenu.MenuItems.Add(myMenuItem2);
        }

        public string GlobalQuery { get; set; }

        override public void ShowContextMenu(TreeView treeview, Point p)
        {
            contextMenu.Show(treeview, p);
        }

        override public void Refresh(bool forceRefresh)
        {
            if (forceRefresh || isFirstTime)
            {
                isFirstTime = false;
                Nodes.Clear();
                Nodes.Add(new StoredProceduresNode(client));
                Nodes.Add(new UDFsNode(client));
                Nodes.Add(new TriggersNode(client));
                Nodes.Add(new ConflictsNode(client));

                FillWithChildren();
            }
        }

        public void FillWithChildren()
        {
            try
            {
                FeedResponse<dynamic> docs;
                using (PerfStatus.Start("ReadDocumentFeed"))
                {
                    if (!string.IsNullOrWhiteSpace(GlobalQuery))
                    {
                        FeedOptions feedOptions = Program.GetMain().GetFeedOptions();
                        var q =
                            client.CreateDocumentQuery(
                                (Tag as DocumentCollection).GetLink(client), GlobalQuery, feedOptions)
                                .AsDocumentQuery();
                        docs = q.ExecuteNextAsync().Result;
                    }
                    else
                    {
                        docs = client.ReadDocumentFeedAsync(((DocumentCollection) Tag).GetLink(client)).Result;
                    }
                }

                foreach (var doc in docs)
                {
                    DocumentNode node = new DocumentNode(client, doc, ResourceType.Document);
                    Nodes.Add(node);
                }
                Program.GetMain().SetResponseHeaders(docs.ResponseHeaders);
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

        async void myMenuItemReadDocumentCollection_Click(object sender, EventArgs eArgs)
        {
            try
            {
                ResourceResponse<DocumentCollection> rr;
                using (PerfStatus.Start("ReadDocumentCollection"))
                {
                    rr =
                        await
                            client.ReadDocumentCollectionAsync(((Resource) Tag).GetLink(client),
                                Program.GetMain().GetRequestOptions());
                }
                // set the result window
                string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
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

        void myMenuItemDeleteDocumentCollection_Click(object sender, EventArgs e)
        {
            string x = Tag.ToString();
            CommandContext context = new CommandContext();
            context.IsDelete = true;
            Program.GetMain()
                .SetCrudContext(this, "Delete DocumentCollection", false, x, DeleteDocumentCollection, context);
        }

        void myMenuItemUpdateDocumentCollection_Click(object sender, EventArgs e)
        {
            string x = Tag.ToString();
            CommandContext context = new CommandContext();
            Program.GetMain()
                .SetCrudContext(this, "Replace DocumentCollection", false, x, UpdateDocumentCollection, context);
        }

        async void UpdateDocumentCollection(string text, object optional)
        {
            try
            {
                RequestOptions requestionOptions = Program.GetMain().GetRequestOptions(true);

                // #1: Update offer if necessary
                DocumentCollection coll = (DocumentCollection) Tag;

                // Find the offer object corresponding to the current offer.
                IQueryable<Offer> offerQuery = from offer in client.CreateOfferQuery()
                    where offer.ResourceLink == coll.SelfLink
                    select offer;
                IDocumentQuery<Offer> offerDocDBQuery = offerQuery.AsDocumentQuery();

                List<Offer> queryResults = new List<Offer>();

                while (offerDocDBQuery.HasMoreResults)
                {
                    queryResults.AddRange(await offerDocDBQuery.ExecuteNextAsync<Offer>());
                }

                // change the Offer type of the document collection 
                Offer offerToReplace = queryResults[0];
                if (requestionOptions.OfferType != null &&
                    string.Compare(offerToReplace.OfferType, requestionOptions.OfferType, StringComparison.Ordinal) != 0)
                {
                    offerToReplace.OfferType = requestionOptions.OfferType;
                    ResourceResponse<Offer> replaceResponse;
                    using (PerfStatus.Start("ReplaceOffer"))
                    {
                        replaceResponse = await client.ReplaceOfferAsync(offerToReplace);
                    }
                }

                // #2: Update collection if necessary
                DocumentCollection collToChange = optional as DocumentCollection;
                collToChange.IndexingPolicy = (IndexingPolicy) coll.IndexingPolicy.Clone();

                ResourceResponse<DocumentCollection> response;
                using (PerfStatus.Start("ReplaceDocumentCollection"))
                {
                    response = await client.ReplaceDocumentCollectionExAsync(coll, requestionOptions);
                }

                Program.GetMain()
                    .SetResultInBrowser(null, "Replace DocumentCollection succeed!", false, response.ResponseHeaders);
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

        async void DeleteDocumentCollection(string text, object optional)
        {
            try
            {
                DocumentCollection coll = (DocumentCollection) Tag;
                ResourceResponse<DocumentCollection> newcoll;
                using (PerfStatus.Start("DeleteDocumentCollection"))
                {
                    newcoll =
                        await
                            client.DeleteDocumentCollectionAsync(coll.GetLink(client),
                                Program.GetMain().GetRequestOptions());
                }
                Program.GetMain()
                    .SetResultInBrowser(null, "Delete DocumentCollection succeed!", false, newcoll.ResponseHeaders);

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

        void myMenuItemAddDocumentFromFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            DialogResult dr = ofd.ShowDialog();

            if (dr == DialogResult.OK)
            {
                string filename = ofd.FileName;
                // 
                string text = File.ReadAllText(filename);

                Program.GetMain().SetCrudContext(this, "Create document", false, text, AddDocument);
            }
        }

        async void myMenuItemAddDocumentsFromFolder_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;

            DialogResult dr = ofd.ShowDialog();

            if (dr == DialogResult.OK)
            {
                string status = string.Format(CultureInfo.InvariantCulture, "Create {0} documents in collection\r\n",
                    ofd.FileNames.Length);
                // Read the files 
                foreach (String filename in ofd.FileNames)
                {
                    // right now assume every file is JSON content
                    string jsonText = File.ReadAllText(filename);
                    string fileRootName = Path.GetFileName(filename);

                    object document = JsonConvert.DeserializeObject(jsonText);

                    try
                    {
                        using (PerfStatus.Start("CreateDocument"))
                        {
                            ResourceResponse<Document> newdocument =
                                await
                                    client.CreateDocumentAsync((Tag as DocumentCollection).GetLink(client), document,
                                        Program.GetMain().GetRequestOptions());
                            status += string.Format(CultureInfo.InvariantCulture, "Succeed adding {0} \r\n",
                                fileRootName);
                        }
                    }
                    catch (DocumentClientException ex)
                    {
                        status += string.Format(CultureInfo.InvariantCulture, "Failed adding {0}, statusCode={1} \r\n",
                            fileRootName, ex.StatusCode);
                    }
                    catch (Exception ex)
                    {
                        status += string.Format(CultureInfo.InvariantCulture,
                            "Failed adding {0}, unknown exception \r\n", fileRootName, ex.Message);
                    }

                    Program.GetMain().SetResultInBrowser(null, status, false);
                }
            }
        }

        void myMenuItemAddDocument_Click(object sender, EventArgs e)
        {
            // 
            dynamic d = new ExpandoObject();
            d.id = "Here is your Document Id";
            string x = JsonConvert.SerializeObject(d, Formatting.Indented);
            Program.GetMain().SetCrudContext(this, "Create document", false, x, AddDocument);
        }


        void myMenuItemQueryDocument_Click(object sender, EventArgs e)
        {
            currentQueryCommandContext = new CommandContext();
            currentQueryCommandContext.IsFeed = true;

            // reset continuation token
            currentContinuation = null;

            Program.GetMain()
                .SetCrudContext(this,
                    string.Format(CultureInfo.InvariantCulture, "Query Documents from Collection {0}",
                        (Tag as DocumentCollection).Id),
                    false, "select * from c", QueryDocuments, currentQueryCommandContext);
        }


        async void AddDocument(string text, object optional)
        {
            try
            {
                object document = JsonConvert.DeserializeObject(text);

                ResourceResponse<Document> newdocument;
                using (PerfStatus.Start("CreateDocument"))
                {
                    newdocument =
                        await
                            client.CreateDocumentAsync((Tag as DocumentCollection).GetLink(client), document,
                                Program.GetMain().GetRequestOptions());
                }

                Nodes.Add(new DocumentNode(client, newdocument.Resource, ResourceType.Document));

                // set the result window
                string json = newdocument.Resource.ToString();

                Program.GetMain().SetResultInBrowser(json, null, false, newdocument.ResponseHeaders);
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

        async void QueryDocuments(string queryText, object optional)
        {
            try
            {
                // text is the querytext.
                IDocumentQuery<dynamic> q = null;

                FeedOptions feedOptions = Program.GetMain().GetFeedOptions();

                if (!string.IsNullOrEmpty(currentContinuation) && string.IsNullOrEmpty(feedOptions.RequestContinuation))
                {
                    feedOptions.RequestContinuation = currentContinuation;
                }

                q =
                    client.CreateDocumentQuery((Tag as DocumentCollection).GetLink(client), queryText, feedOptions)
                        .AsDocumentQuery();

                Stopwatch sw = Stopwatch.StartNew();

                FeedResponse<dynamic> r;
                using (PerfStatus.Start("QueryDocument"))
                {
                    r = await q.ExecuteNextAsync();
                }
                sw.Stop();
                currentContinuation = r.ResponseContinuation;
                currentQueryCommandContext.HasContinuation = !string.IsNullOrEmpty(currentContinuation);
                currentQueryCommandContext.QueryStarted = true;

                // set the result window
                string text = null;
                if (r.Count > 1)
                {
                    text = string.Format(CultureInfo.InvariantCulture, "Returned {0} documents in {1} ms", r.Count,
                        sw.ElapsedMilliseconds);
                }
                else
                {
                    text = string.Format(CultureInfo.InvariantCulture, "Returned {0} document in {1} ms", r.Count,
                        sw.ElapsedMilliseconds);
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

                GlobalQuery = queryText;
                Program.GetMain().SetResultInBrowser(jsonarray, text, true, r.ResponseHeaders);
                Program.GetMain().SetNextPageVisibility(currentQueryCommandContext);
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


    class DocumentNode : FeedNode
    {
        private DocumentClient client;
        private ContextMenu contextMenu = new ContextMenu();
        private ResourceType resourceType = 0;

        public DocumentNode(DocumentClient client, dynamic document, ResourceType resoureType)
        {
            resourceType = resoureType;
            if (resourceType != ResourceType.Offer)
            {
                var doc = document as Document;
                if (doc != null)
                    Text = doc.Id;
                else
                {
                    doc = JsonConvert.DeserializeObject<Document>(document.ToString());
                    document = doc;
                    Text = doc.Id;
                }
            }
            else
            {
                Offer offer = document as Offer;
                Text = offer.OfferType + "_" + offer.GetPropertyValue<String>("offerResourceId");
            }
            Tag = document;
            this.client = client;

            MenuItem myMenuItem0 = new MenuItem("Read " + resourceType.ToString());
            myMenuItem0.Click += new EventHandler(myMenuItemRead_Click);
            contextMenu.MenuItems.Add(myMenuItem0);

            if (resourceType != ResourceType.Conflict && resourceType != ResourceType.Offer)
            {
                MenuItem myMenuItem1 = new MenuItem("Replace " + resourceType.ToString());
                myMenuItem1.Click += new EventHandler(myMenuItemUpdate_Click);
                contextMenu.MenuItems.Add(myMenuItem1);
            }

            if (resourceType != ResourceType.Offer)
            {
                MenuItem myMenuItem = new MenuItem("Delete " + resourceType.ToString());
                myMenuItem.Click += new EventHandler(myMenuItemDelete_Click);
                contextMenu.MenuItems.Add(myMenuItem);
            }

            if (resourceType == ResourceType.Permission)
            {
                ImageKey = "Permission";
                SelectedImageKey = "Permission";
            }
            else if (resourceType == ResourceType.Attachment)
            {
                ImageKey = "Attachment";
                SelectedImageKey = "Attachment";

                MenuItem myMenuItem2 = new MenuItem("Download media");
                myMenuItem2.Click += new EventHandler(myMenuItemDownloadMedia_Click);
                contextMenu.MenuItems.Add(myMenuItem2);

                MenuItem myMenuItem3 = new MenuItem("Render media");
                myMenuItem3.Click += new EventHandler(myMenuItemRenderMedia_Click);
                contextMenu.MenuItems.Add(myMenuItem3);
            }
            else if (resourceType == ResourceType.StoredProcedure || resourceType == ResourceType.Trigger ||
                     resourceType == ResourceType.UserDefinedFunction)
            {
                ImageKey = "Javascript";
                SelectedImageKey = "Javascript";
                if (resourceType == ResourceType.StoredProcedure)
                {
                    MenuItem myMenuItem2 = new MenuItem("Execute " + resourceType.ToString());
                    myMenuItem2.Click += new EventHandler(myMenuItemExecuteSP_Click);
                    contextMenu.MenuItems.Add(myMenuItem2);
                }
            }
            else if (resourceType == ResourceType.User)
            {
                ImageKey = "User";
                SelectedImageKey = "User";

                Nodes.Add(new PermissionsNode(this.client));
            }
            else if (resourceType == ResourceType.Document)
            {
                Nodes.Add(new TreeNode("Fake"));

                contextMenu.MenuItems.Add("-");

                MenuItem myMenuItem3 = new MenuItem("Create attachment");
                myMenuItem3.Click += new EventHandler(myMenuItemAttachment_Click);
                contextMenu.MenuItems.Add(myMenuItem3);

                MenuItem myMenuItem4 = new MenuItem("Create attachment from file");
                myMenuItem4.Click += new EventHandler(myMenuItemAttachmentFromFile_Click);
                contextMenu.MenuItems.Add(myMenuItem4);
            }
            else if (resourceType == ResourceType.Conflict)
            {
                ImageKey = "Conflict";
                SelectedImageKey = "Conflict";
            }
            else if (resourceType == ResourceType.Offer)
            {
                ImageKey = "Offer";
                SelectedImageKey = "Offer";
            }
        }

        override public void ShowContextMenu(TreeView treeview, Point p)
        {
            contextMenu.Show(treeview, p);
        }

        override public void Refresh(bool forceRefresh)
        {
            if (forceRefresh || isFirstTime)
            {
                isFirstTime = false;
                Nodes.Clear();

                if (resourceType == ResourceType.User)
                {
                    Nodes.Add(new PermissionsNode(client));
                }
                else if (resourceType == ResourceType.Document)
                {
                    FillWithChildren();
                }
            }
        }

        public string GetBody()
        {
            string body = null;
            if (resourceType == ResourceType.StoredProcedure)
            {
                body = "\nThe storedprocedure Javascript function: \n\n" + (Tag as StoredProcedure).Body;
            }
            else if (resourceType == ResourceType.Trigger)
            {
                body = "\nThe trigger Javascript function: \n\n" + (Tag as Trigger).Body;
            }
            else if (resourceType == ResourceType.UserDefinedFunction)
            {
                body = "\nThe stored Javascript function: \n\n" + (Tag as UserDefinedFunction).Body;
            }
            return body;
        }

        public void FillWithChildren()
        {
            try
            {
                FeedResponse<Attachment> attachments;
                using (PerfStatus.Start("ReadAttachmentFeed"))
                {
                    attachments = client.ReadAttachmentFeedAsync((Tag as Document).GetLink(client)).Result;
                }
                foreach (var attachment in attachments)
                {
                    DocumentNode node = new DocumentNode(client, attachment, ResourceType.Attachment);
                    Nodes.Add(node);
                }
                Program.GetMain().SetResponseHeaders(attachments.ResponseHeaders);
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

        void myMenuItemUpdate_Click(object sender, EventArgs e)
        {
            if (resourceType == ResourceType.StoredProcedure)
            {
                Program.GetMain()
                    .SetCrudContext(this, "Replace " + resourceType.ToString(), true, (Tag as StoredProcedure).Body,
                        UpdateNode);
            }
            else if (resourceType == ResourceType.Trigger)
            {
                Program.GetMain()
                    .SetCrudContext(this, "Replace " + resourceType.ToString(), true, (Tag as Trigger).Body, UpdateNode);
            }
            else if (resourceType == ResourceType.UserDefinedFunction)
            {
                Program.GetMain()
                    .SetCrudContext(this, "Replace " + resourceType.ToString(), true, (Tag as UserDefinedFunction).Body,
                        UpdateNode);
            }
            else
            {
                string x = Tag.ToString();
                Program.GetMain().SetCrudContext(this, "Replace " + resourceType.ToString(), false, x, UpdateNode);
            }
        }

        async void myMenuItemRead_Click(object sender, EventArgs eventArg)
        {
            try
            {
                if (resourceType == ResourceType.Offer)
                {
                    ResourceResponse<Offer> rr;
                    using (PerfStatus.Start("ReadOffer"))
                    {
                        rr = await client.ReadOfferAsync(((Resource) Tag).SelfLink);
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.Document)
                {
                    ResourceResponse<Document> rr;
                    using (PerfStatus.Start("ReadDocument"))
                    {
                        rr =
                            await
                                client.ReadDocumentAsync(((Resource) Tag).GetLink(client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.Conflict)
                {
                    ResourceResponse<Conflict> rr;
                    using (PerfStatus.Start("ReadConflict"))
                    {
                        rr =
                            await
                                client.ReadConflictAsync(((Resource) Tag).GetLink(client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.Attachment)
                {
                    ResourceResponse<Attachment> rr;
                    using (PerfStatus.Start("ReadAttachment"))
                    {
                        rr =
                            await
                                client.ReadAttachmentAsync(((Resource) Tag).GetLink(client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.User)
                {
                    ResourceResponse<User> rr;
                    using (PerfStatus.Start("ReadUser"))
                    {
                        rr =
                            await
                                client.ReadUserAsync(((Resource) Tag).GetLink(client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.Permission)
                {
                    ResourceResponse<Permission> rr;
                    using (PerfStatus.Start("ReadPermission"))
                    {
                        rr =
                            await
                                client.ReadPermissionAsync(((Resource) Tag).GetLink(client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.StoredProcedure)
                {
                    ResourceResponse<StoredProcedure> rr;
                    using (PerfStatus.Start("ReadStoredProcedure"))
                    {
                        rr =
                            await
                                client.ReadStoredProcedureAsync(((Resource) Tag).GetLink(client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.Trigger)
                {
                    ResourceResponse<Trigger> rr;
                    using (PerfStatus.Start("ReadTrigger"))
                    {
                        rr =
                            await
                                client.ReadTriggerAsync(((Resource) Tag).GetLink(client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.UserDefinedFunction)
                {
                    ResourceResponse<UserDefinedFunction> rr;
                    using (PerfStatus.Start("ReadUDF"))
                    {
                        rr =
                            await
                                client.ReadUserDefinedFunctionAsync(((Resource) Tag).GetLink(client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else
                {
                    throw new ArgumentException("Unsupported resource type " + resourceType);
                }
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

        void myMenuItemAttachment_Click(object sender, EventArgs e)
        {
            Attachment attachment = new Attachment();
            attachment.Id = "Here is your attachment Id";
            attachment.ContentType = "application-content-type";
            attachment.MediaLink = "internal link or Azure blob or Amazon S3 link";

            string x = attachment.ToString();
            Program.GetMain()
                .SetCrudContext(this, "Create attachment for this document " + resourceType.ToString(), false, x,
                    AddAttachment);
        }

        async void myMenuItemRenderMedia_Click(object sender, EventArgs eventArg)
        {
            string appTempPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DocumentDBStudio");
            string guidFileName = Guid.NewGuid().ToString();
            string fileName;

            // let's guess the contentype.
            Attachment attachment = Tag as Attachment;
            if (
                string.Compare(attachment.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase) ==
                0)
            {
                // get the extension from attachment.Id
                int index = attachment.Id.LastIndexOf('.');
                fileName = guidFileName + attachment.Id.Substring(index);
            }
            else if (attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                // treat as image.
                fileName = guidFileName + ".gif";
            }
            else
            {
                fileName = guidFileName + ".txt";
            }

            fileName = Path.Combine(appTempPath, fileName);
            try
            {
                MediaResponse rr;
                using (PerfStatus.Start("DownloadMedia"))
                {
                    rr = await client.ReadMediaAsync(attachment.MediaLink);
                }
                using (FileStream fileStream = File.Create(fileName))
                {
                    rr.Media.CopyTo(fileStream);
                }

                Program.GetMain().SetResultInBrowser(null, "It is saved to " + fileName, true);
                Program.GetMain().RenderFile(fileName);
            }
            catch (Exception e)
            {
                Program.GetMain().SetResultInBrowser(null, e.ToString(), true);
            }
        }

        async void myMenuItemDownloadMedia_Click(object sender, EventArgs eventArg)
        {
            Attachment attachment = Tag as Attachment;

            // Get the filenanme from attachment.Id
            int index = attachment.Id.LastIndexOf('\\');
            string fileName = attachment.Id;
            if (index > 0)
                fileName = fileName.Substring(index + 1);

            SaveFileDialog ofd = new SaveFileDialog();
            ofd.FileName = fileName;
            DialogResult dr = ofd.ShowDialog();

            if (dr == DialogResult.OK)
            {
                string saveFile = ofd.FileName;
                Program.GetMain().SetLoadingState();

                try
                {
                    MediaResponse rr;
                    using (PerfStatus.Start("DownloadMedia"))
                    {
                        rr = await client.ReadMediaAsync(attachment.MediaLink);
                    }
                    using (FileStream fileStream = File.Create(saveFile))
                    {
                        rr.Media.CopyTo(fileStream);
                    }
                    Program.GetMain().SetResultInBrowser(null, "It is saved to " + saveFile, true);
                }
                catch (Exception e)
                {
                    Program.GetMain().SetResultInBrowser(null, e.ToString(), true);
                }
            }
        }

        async void myMenuItemAttachmentFromFile_Click(object sender, EventArgs eventArg)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            DialogResult dr = ofd.ShowDialog();

            if (dr == DialogResult.OK)
            {
                string filename = ofd.FileName;
                // 
                // todo: present the dialog for Slug name and Content type
                // 
                Program.GetMain().SetLoadingState();

                try
                {
                    using (FileStream stream = new FileStream(filename,
                        FileMode.Open, FileAccess.Read))
                    {
                        MediaOptions options = new MediaOptions()
                        {
                            ContentType = "application/octet-stream",
                            Slug = Path.GetFileName(ofd.FileName)
                        };

                        ResourceResponse<Attachment> rr;
                        using (PerfStatus.Start("CreateAttachment"))
                        {
                            rr = await client.CreateAttachmentAsync((Tag as Document).GetLink(client) + "/attachments",
                                stream, options);
                        }
                        string json = rr.Resource.ToString();

                        Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);

                        Nodes.Add(new DocumentNode(client, rr.Resource, ResourceType.Attachment));
                    }
                }
                catch (Exception e)
                {
                    Program.GetMain().SetResultInBrowser(null, e.ToString(), true);
                }
            }
        }

        void myMenuItemExecuteSP_Click(object sender, EventArgs e)
        {
            Program.GetMain().SetCrudContext(this, "Execute " + resourceType.ToString() + " " +
                                                   (Tag as Resource).Id, false,
                "Here is the input parameters to the storedProcedure. Input each parameter as one line without quotation mark.",
                ExecuteStoredProcedure);
        }

        void myMenuItemDelete_Click(object sender, EventArgs e)
        {
            string x = Tag.ToString();
            CommandContext context = new CommandContext();
            context.IsDelete = true;
            Program.GetMain().SetCrudContext(this, "Delete " + resourceType.ToString(), false, x, DeleteNode, context);
        }

        async void AddAttachment(string text, object optional)
        {
            try
            {
                Attachment attachment = (Attachment) JsonConvert.DeserializeObject(text, typeof (Attachment));

                ResourceResponse<Attachment> rr;
                using (PerfStatus.Start("CreateAttachment"))
                {
                    rr = await client.CreateAttachmentAsync((Tag as Resource).GetLink(client),
                        attachment, Program.GetMain().GetRequestOptions());
                }
                string json = rr.Resource.ToString();

                Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);

                Nodes.Add(new DocumentNode(client, rr.Resource, ResourceType.Attachment));
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

        async void ExecuteStoredProcedure(string text, object optional)
        {
            try
            {
                List<string> inputParamters = new List<string>();
                if (!string.IsNullOrEmpty(text))
                {
                    using (StringReader sr = new StringReader(text))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (!string.IsNullOrEmpty(line))
                            {
                                inputParamters.Add(line);
                            }
                        } //while
                    } //usi
                }
                var dynamicInputParams = new dynamic[inputParamters.Count];
                for (var i = 0; i < inputParamters.Count; i++)
                {
                    var inputParamter = inputParamters[i];
                    var jTokenParam = JToken.Parse(inputParamter);
                    var dynamicParam = Helper.ConvertJTokenToDynamic(jTokenParam);
                    dynamicInputParams[i] = dynamicParam;
                }

                StoredProcedureResponse<dynamic> rr;
                using (PerfStatus.Start("ExecuateStoredProcedure"))
                {
                    rr = await client.ExecuteStoredProcedureAsync<dynamic>((Tag as Resource).GetLink(client),
                        dynamicInputParams);
                }
                string executeResult = rr.Response.ToString();

                Program.GetMain().SetResultInBrowser(null, executeResult, true, rr.ResponseHeaders);
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

        async void UpdateNode(string text, object optionalObject)
        {
            string optional = optionalObject as string;
            try
            {
                string json = null;
                if (resourceType == ResourceType.Document)
                {
                    Document doc = (Document) JsonConvert.DeserializeObject(text, typeof (Document));
                    doc.SetReflectedPropertyValue("AltLink", (Tag as Document).GetAltLink());
                    ResourceResponse<Document> rr;
                    using (PerfStatus.Start("ReplaceDocument"))
                    {
                        rr =
                            await
                                client.ReplaceDocumentAsync(doc.GetLink(client), doc,
                                    Program.GetMain().GetRequestOptions());
                    }
                    json = rr.Resource.ToString();

                    Tag = rr.Resource;
                    Text = rr.Resource.Id;
                    // set the result window
                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.StoredProcedure)
                {
                    StoredProcedure sp = Tag as StoredProcedure;
                    sp.Body = text;
                    if (!string.IsNullOrEmpty(optional))
                    {
                        sp.Id = optional;
                    }
                    ResourceResponse<StoredProcedure> rr;
                    using (PerfStatus.Start("ReplaceStoredProcedure"))
                    {
                        rr = await client.ReplaceStoredProcedureExAsync(sp, Program.GetMain().GetRequestOptions());
                    }
                    json = rr.Resource.ToString();
                    Tag = rr.Resource;
                    Text = rr.Resource.Id;
                    // set the result window
                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.User)
                {
                    User sp = (User) JsonConvert.DeserializeObject(text, typeof (User));
                    sp.SetReflectedPropertyValue("AltLink", (Tag as User).GetAltLink());
                    ResourceResponse<User> rr;
                    using (PerfStatus.Start("ReplaceUser"))
                    {
                        rr = await client.ReplaceUserExAsync(sp, Program.GetMain().GetRequestOptions());
                    }
                    json = rr.Resource.ToString();
                    Tag = rr.Resource;
                    Text = rr.Resource.Id;
                    // set the result window
                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.Trigger)
                {
                    Trigger sp = Tag as Trigger;
                    sp.Body = text;
                    if (!string.IsNullOrEmpty(optional))
                    {
                        sp.Id = optional;
                    }
                    ResourceResponse<Trigger> rr;
                    using (PerfStatus.Start("ReplaceTrigger"))
                    {
                        rr = await client.ReplaceTriggerExAsync(sp, Program.GetMain().GetRequestOptions());
                    }
                    json = rr.Resource.ToString();
                    Tag = rr.Resource;
                    Text = rr.Resource.Id;
                    // set the result window
                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.UserDefinedFunction)
                {
                    UserDefinedFunction sp = Tag as UserDefinedFunction;
                    sp.Body = text;
                    if (!string.IsNullOrEmpty(optional))
                    {
                        sp.Id = optional;
                    }
                    ResourceResponse<UserDefinedFunction> rr;
                    using (PerfStatus.Start("ReplaceUDF"))
                    {
                        rr = await client.ReplaceUserDefinedFunctionExAsync(sp, Program.GetMain().GetRequestOptions());
                    }
                    json = rr.Resource.ToString();
                    Tag = rr.Resource;
                    Text = rr.Resource.Id;
                    // set the result window
                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.Permission)
                {
                    Permission sp = JsonSerializable.LoadFrom<Permission>(new MemoryStream(Encoding.UTF8.GetBytes(text)));
                    sp.SetReflectedPropertyValue("AltLink", (Tag as Permission).GetAltLink());
                    ResourceResponse<Permission> rr;
                    using (PerfStatus.Start("ReplacePermission"))
                    {
                        rr = await client.ReplacePermissionExAsync(sp, Program.GetMain().GetRequestOptions());
                    }
                    json = rr.Resource.ToString();
                    Tag = rr.Resource;
                    Text = rr.Resource.Id;
                    // set the result window
                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.Attachment)
                {
                    Attachment sp = (Attachment) JsonConvert.DeserializeObject(text, typeof (Attachment));
                    sp.SetReflectedPropertyValue("AltLink", (Tag as Attachment).GetAltLink());
                    ResourceResponse<Attachment> rr;
                    using (PerfStatus.Start("ReplaceAttachment"))
                    {
                        rr = await client.ReplaceAttachmentExAsync(sp, Program.GetMain().GetRequestOptions());
                    }
                    json = rr.Resource.ToString();
                    Tag = rr.Resource;
                    Text = rr.Resource.Id;
                    // set the result window
                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
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

        async void DeleteNode(string text, object optional)
        {
            try
            {
                if (resourceType == ResourceType.Document)
                {
                    Document doc = (Document) Tag;
                    ResourceResponse<Document> rr;
                    using (PerfStatus.Start("DeleteDocument"))
                    {
                        rr =
                            await client.DeleteDocumentAsync(doc.GetLink(client), Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain().SetResultInBrowser(null, "Delete Document succeed!", false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.StoredProcedure)
                {
                    StoredProcedure sp = (StoredProcedure) Tag;
                    ResourceResponse<StoredProcedure> rr;
                    using (PerfStatus.Start("DeleteStoredProcedure"))
                    {
                        rr =
                            await
                                client.DeleteStoredProcedureAsync(sp.GetLink(client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain()
                        .SetResultInBrowser(null, "Delete StoredProcedure succeed!", false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.User)
                {
                    User sp = (User) Tag;
                    ResourceResponse<User> rr;
                    using (PerfStatus.Start("DeleteUser"))
                    {
                        rr = await client.DeleteUserAsync(sp.GetLink(client), Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain().SetResultInBrowser(null, "Delete User succeed!", false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.Trigger)
                {
                    Trigger sp = (Trigger) Tag;
                    ResourceResponse<Trigger> rr;
                    using (PerfStatus.Start("DeleteTrigger"))
                    {
                        rr = await client.DeleteTriggerAsync(sp.GetLink(client), Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain().SetResultInBrowser(null, "Delete Trigger succeed!", false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.UserDefinedFunction)
                {
                    UserDefinedFunction sp = (UserDefinedFunction) Tag;
                    ResourceResponse<UserDefinedFunction> rr;
                    using (PerfStatus.Start("DeleteUDF"))
                    {
                        rr =
                            await
                                client.DeleteUserDefinedFunctionAsync(sp.GetLink(client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain()
                        .SetResultInBrowser(null, "Delete UserDefinedFunction succeed!", false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.Permission)
                {
                    Permission sp = (Permission) Tag;
                    ResourceResponse<Permission> rr;
                    using (PerfStatus.Start("DeletePermission"))
                    {
                        rr =
                            await
                                client.DeletePermissionAsync(sp.GetLink(client), Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain().SetResultInBrowser(null, "Delete Permission succeed!", false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.Attachment)
                {
                    Attachment sp = (Attachment) Tag;
                    ResourceResponse<Attachment> rr;
                    using (PerfStatus.Start("DeleteAttachment"))
                    {
                        rr =
                            await
                                client.DeleteAttachmentAsync(sp.GetLink(client), Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain().SetResultInBrowser(null, "Delete Attachment succeed!", false, rr.ResponseHeaders);
                }
                else if (resourceType == ResourceType.Conflict)
                {
                    Conflict sp = (Conflict) Tag;
                    ResourceResponse<Conflict> rr;
                    using (PerfStatus.Start("DeleteConlict"))
                    {
                        rr = await client.DeleteConflictAsync(sp.GetLink(client), Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain().SetResultInBrowser(null, "Delete Conflict succeed!", false, rr.ResponseHeaders);
                }
                // Remove the node.
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

    class StoredProceduresNode : FeedNode
    {
        private DocumentClient client;
        private ContextMenu contextMenu = new ContextMenu();

        public StoredProceduresNode(DocumentClient client)
        {
            Text = "StoredProcedures";
            this.client = client;
            Nodes.Add(new TreeNode("fake"));
            Tag = "This represents the StoredProcedure feed. Right click to add StoredProcedure";

            ImageKey = "Feed";
            SelectedImageKey = "Feed";

            MenuItem myMenuItem = new MenuItem("Create StoredProcedure");
            myMenuItem.Click += new EventHandler(myMenuItemAddStoredProcedure_Click);
            contextMenu.MenuItems.Add(myMenuItem);
            MenuItem myMenuItem2 = new MenuItem("Create StoredProcedure From File");
            myMenuItem2.Click += new EventHandler(myMenuItemAddStoredProcedureFromFile_Click);
            contextMenu.MenuItems.Add(myMenuItem2);

            MenuItem myMenuItem1 = new MenuItem("Refresh StoredProcedures feed");
            myMenuItem1.Click += new EventHandler((sender, e) => Refresh(true));
            contextMenu.MenuItems.Add(myMenuItem1);
        }

        override public void ShowContextMenu(TreeView treeview, Point p)
        {
            contextMenu.Show(treeview, p);
        }

        override public void Refresh(bool forceRefresh)
        {
            if (forceRefresh || isFirstTime)
            {
                isFirstTime = false;
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
                        client.ReadStoredProcedureFeedAsync((collnode.Tag as DocumentCollection).GetLink(client)).Result;
                }

                foreach (var sp in sps)
                {
                    DocumentNode node = new DocumentNode(client, sp, ResourceType.StoredProcedure);
                    Nodes.Add(node);
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

        async void AddStoredProcedure(string body, object idobject)
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
                            client.CreateStoredProcedureAsync((Parent.Tag as DocumentCollection).GetLink(client), sp,
                                Program.GetMain().GetRequestOptions());
                }

                Nodes.Add(new DocumentNode(client, newsp.Resource, ResourceType.StoredProcedure));

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

    class UDFsNode : FeedNode
    {
        private DocumentClient client;
        private ContextMenu contextMenu = new ContextMenu();

        public UDFsNode(DocumentClient client)
        {
            Text = "UDFs";
            this.client = client;
            Nodes.Add(new TreeNode("fake"));
            Tag = "This represents the UserDefinedFunction feed. Right click to add UserDefinedFunction";
            ImageKey = "Feed";
            SelectedImageKey = "Feed";

            MenuItem myMenuItem = new MenuItem("Create UserDefinedFunction");
            myMenuItem.Click += new EventHandler(myMenuItemAddUDF_Click);
            contextMenu.MenuItems.Add(myMenuItem);
            MenuItem myMenuItem2 = new MenuItem("Create UserDefinedFunction from File");
            myMenuItem2.Click += new EventHandler(myMenuItemAddUDFFromFile_Click);
            contextMenu.MenuItems.Add(myMenuItem2);
            MenuItem myMenuItem1 = new MenuItem("Refresh UserDefinedFunction feed");
            myMenuItem1.Click += new EventHandler((sender, e) => Refresh(true));
            contextMenu.MenuItems.Add(myMenuItem1);
        }

        override public void ShowContextMenu(TreeView treeview, Point p)
        {
            contextMenu.Show(treeview, p);
        }

        override public void Refresh(bool forceRefresh)
        {
            if (forceRefresh || isFirstTime)
            {
                isFirstTime = false;
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
                        client.ReadUserDefinedFunctionFeedAsync((collnode.Tag as DocumentCollection).GetLink(client))
                            .Result;
                }

                foreach (var sp in sps)
                {
                    DocumentNode node = new DocumentNode(client, sp, ResourceType.UserDefinedFunction);
                    Nodes.Add(node);
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
            // 
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
                // 
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
                            client.CreateUserDefinedFunctionAsync((Parent.Tag as DocumentCollection).GetLink(client),
                                udf, Program.GetMain().GetRequestOptions());
                }

                Nodes.Add(new DocumentNode(client, newudf.Resource, ResourceType.UserDefinedFunction));

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

    class TriggersNode : FeedNode
    {
        private DocumentClient client;
        private ContextMenu contextMenu = new ContextMenu();

        public TriggersNode(DocumentClient client)
        {
            Text = "Triggers";
            this.client = client;
            Nodes.Add(new TreeNode("fake"));
            Tag = "This represents the Triggers feed. Right click to add Trigger";
            ImageKey = "Feed";
            SelectedImageKey = "Feed";

            MenuItem myMenuItem = new MenuItem("Create Trigger");
            myMenuItem.Click += new EventHandler(myMenuItemAddTrigger_Click);
            contextMenu.MenuItems.Add(myMenuItem);
            MenuItem myMenuItem2 = new MenuItem("Create Trigger from file");
            myMenuItem2.Click += new EventHandler(myMenuItemAddTriggerFromFile_Click);
            contextMenu.MenuItems.Add(myMenuItem2);
            MenuItem myMenuItem1 = new MenuItem("Refresh Triggers feed");
            myMenuItem1.Click += new EventHandler((sender, e) => Refresh(true));
            contextMenu.MenuItems.Add(myMenuItem1);
        }

        override public void ShowContextMenu(TreeView treeview, Point p)
        {
            contextMenu.Show(treeview, p);
        }

        override public void Refresh(bool forceRefresh)
        {
            if (forceRefresh || isFirstTime)
            {
                isFirstTime = false;
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
                    sps = client.ReadTriggerFeedAsync((collnode.Tag as DocumentCollection).GetLink(client)).Result;
                }

                foreach (var sp in sps)
                {
                    DocumentNode node = new DocumentNode(client, sp, ResourceType.Trigger);
                    Nodes.Add(node);
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
                // 
                string text = File.ReadAllText(filename);

                Program.GetMain()
                    .SetCrudContext(this, "Create trigger", false, text, AddTrigger,
                        new CommandContext() {IsCreateTrigger = true});
            }
        }

        void myMenuItemAddTrigger_Click(object sender, EventArgs e)
        {
            // 
            Program.GetMain()
                .SetCrudContext(this,
                    string.Format(CultureInfo.InvariantCulture, "Create trigger in collection {0}",
                        (Parent.Tag as DocumentCollection).Id),
                    true, "function() { \r\n \r\n}", AddTrigger, new CommandContext() {IsCreateTrigger = true});
        }

        async void AddTrigger(string body, object triggerObject)
        {
            try
            {
                Trigger trigger = triggerObject as Trigger;

                ResourceResponse<Trigger> newtrigger;
                using (PerfStatus.Start("CreateTrigger"))
                {
                    newtrigger =
                        await
                            client.CreateTriggerAsync((Parent.Tag as DocumentCollection).GetLink(client), trigger,
                                Program.GetMain().GetRequestOptions());
                }

                Nodes.Add(new DocumentNode(client, newtrigger.Resource, ResourceType.Trigger));

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

    class UsersNode : FeedNode
    {
        private DocumentClient client;
        private ContextMenu contextMenu = new ContextMenu();

        public UsersNode(DocumentClient client)
        {
            Text = "Users";
            this.client = client;
            Nodes.Add(new TreeNode("fake"));
            Tag = "This represents the Users feed. Right click to add user";
            ImageKey = "User";
            SelectedImageKey = "User";

            MenuItem myMenuItem = new MenuItem("Create User");
            myMenuItem.Click += new EventHandler(myMenuItemAddUser_Click);
            contextMenu.MenuItems.Add(myMenuItem);
            MenuItem myMenuItem1 = new MenuItem("Refresh Users feed");
            myMenuItem1.Click += new EventHandler((sender, e) => Refresh(true));
            contextMenu.MenuItems.Add(myMenuItem1);
        }

        override public void ShowContextMenu(TreeView treeview, Point p)
        {
            contextMenu.Show(treeview, p);
        }

        override public void Refresh(bool forceRefresh)
        {
            if (forceRefresh || isFirstTime)
            {
                isFirstTime = false;
                Nodes.Clear();
                FillWithChildren();
            }
        }

        public void FillWithChildren()
        {
            try
            {
                FeedResponse<User> sps;
                using (PerfStatus.Start("ReadUser"))
                {
                    sps = client.ReadUserFeedAsync((Parent.Tag as Database).GetLink(client)).Result;
                }
                foreach (var sp in sps)
                {
                    DocumentNode node = new DocumentNode(client, sp, ResourceType.User);
                    Nodes.Add(node);
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

        void myMenuItemAddUser_Click(object sender, EventArgs e)
        {
            dynamic d = new ExpandoObject();
            d.id = "Here is your user Id";
            string x = JsonConvert.SerializeObject(d, Formatting.Indented);
            Program.GetMain()
                .SetCrudContext(this,
                    string.Format(CultureInfo.InvariantCulture, "Create user in database {0}",
                        (Parent.Tag as Database).Id),
                    false, x, AddUser);
        }

        async void AddUser(string body, object id)
        {
            try
            {
                User user = (User) JsonConvert.DeserializeObject(body, typeof (User));

                ResourceResponse<User> newUser;
                using (PerfStatus.Start("CreateUser"))
                {
                    newUser =
                        await
                            client.CreateUserAsync((Parent.Tag as Database).GetLink(client), user,
                                Program.GetMain().GetRequestOptions());
                }
                Nodes.Add(new DocumentNode(client, newUser.Resource, ResourceType.User));

                // set the result window
                string json = newUser.Resource.ToString();

                Program.GetMain().SetResultInBrowser(json, null, false, newUser.ResponseHeaders);
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

    class PermissionsNode : FeedNode
    {
        private DocumentClient client;
        private ContextMenu contextMenu = new ContextMenu();

        public PermissionsNode(DocumentClient client)
        {
            Text = "Permissions";
            this.client = client;
            Nodes.Add(new TreeNode("fake"));
            Tag = "This represents the Permissions feed. Right click to add permission";
            ImageKey = "Permission";
            SelectedImageKey = "Permission";

            MenuItem myMenuItem = new MenuItem("Create Permission");
            myMenuItem.Click += new EventHandler(myMenuItemAddPermission_Click);
            contextMenu.MenuItems.Add(myMenuItem);
            MenuItem myMenuItem1 = new MenuItem("Refresh Permissions feed");
            myMenuItem1.Click += new EventHandler((sender, e) => Refresh(true));
            contextMenu.MenuItems.Add(myMenuItem1);
        }

        override public void ShowContextMenu(TreeView treeview, Point p)
        {
            contextMenu.Show(treeview, p);
        }

        override public void Refresh(bool forceRefresh)
        {
            if (forceRefresh || isFirstTime)
            {
                isFirstTime = false;
                Nodes.Clear();
                FillWithChildren();
            }
        }

        public void FillWithChildren()
        {
            try
            {
                FeedResponse<Permission> sps;
                using (PerfStatus.Start("ReadPermission"))
                {
                    sps = client.ReadPermissionFeedAsync((Parent.Tag as User).GetLink(client)).Result;
                }

                foreach (var sp in sps)
                {
                    DocumentNode node = new DocumentNode(client, sp, ResourceType.Permission);
                    Nodes.Add(node);
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

        void myMenuItemAddPermission_Click(object sender, EventArgs e)
        {
            Permission permission = new Permission();
            permission.Id = "Here is your permission Id";
            permission.PermissionMode = PermissionMode.Read;
            permission.ResourceLink = "your resource link";

            string x = permission.ToString();

            Program.GetMain()
                .SetCrudContext(this,
                    string.Format(CultureInfo.InvariantCulture, "Create permission for user {0}",
                        (Parent.Tag as Resource).Id),
                    false, x, AddPermission);
        }

        async void AddPermission(string body, object id)
        {
            try
            {
                Permission permission =
                    JsonSerializable.LoadFrom<Permission>(new MemoryStream(Encoding.UTF8.GetBytes(body)));

                ResourceResponse<Permission> newtpermission;
                using (PerfStatus.Start("CreatePermission"))
                {
                    newtpermission =
                        await
                            client.CreatePermissionAsync((Parent.Tag as Resource).GetLink(client), permission,
                                Program.GetMain().GetRequestOptions());
                }
                Nodes.Add(new DocumentNode(client, newtpermission.Resource, ResourceType.Permission));

                // set the result window
                string json = newtpermission.Resource.ToString();

                Program.GetMain().SetResultInBrowser(json, null, false, newtpermission.ResponseHeaders);
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

    class ConflictsNode : FeedNode
    {
        private DocumentClient client;
        private ContextMenu contextMenu = new ContextMenu();

        public ConflictsNode(DocumentClient client)
        {
            Text = "Conflicts";
            this.client = client;
            Nodes.Add(new TreeNode("fake"));
            Tag = "This represents the Conflicts feed.";
            ImageKey = "Conflict";
            SelectedImageKey = "Conflict";

            MenuItem myMenuItem1 = new MenuItem("Refresh Conflict feed");
            myMenuItem1.Click += new EventHandler((sender, e) => Refresh(true));
            contextMenu.MenuItems.Add(myMenuItem1);

            // Query conflicts currrently fail due to gateway
            MenuItem myMenuItem2 = new MenuItem("Query Conflict feed");
            myMenuItem2.Click += new EventHandler(myMenuItemQueryConflicts_Click);
            contextMenu.MenuItems.Add(myMenuItem2);
        }

        override public void ShowContextMenu(TreeView treeview, Point p)
        {
            contextMenu.Show(treeview, p);
        }

        override public void Refresh(bool forceRefresh)
        {
            if (forceRefresh || isFirstTime)
            {
                isFirstTime = false;
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
                        client.ReadConflictFeedAsync((Parent.Tag as DocumentCollection).GetLink(client)).Result;
                }

                foreach (var sp in feedConflicts)
                {
                    DocumentNode node = new DocumentNode(client, sp, ResourceType.Conflict);
                    Nodes.Add(node);
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
                        client.CreateConflictQuery((Parent.Tag as DocumentCollection).GetLink(client), queryText)
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

    class OffersNode : FeedNode
    {
        private DocumentClient client;
        private ContextMenu contextMenu = new ContextMenu();

        public OffersNode(DocumentClient client)
        {
            Text = "Offers";
            this.client = client;
            Nodes.Add(new TreeNode("fake"));
            Tag = "This represents the Offers feed.";
            ImageKey = "Offer";
            SelectedImageKey = "Offer";

            MenuItem myMenuItem1 = new MenuItem("Refresh Offer feed");
            myMenuItem1.Click += new EventHandler((sender, e) => Refresh(true));
            contextMenu.MenuItems.Add(myMenuItem1);
        }

        override public void ShowContextMenu(TreeView treeview, Point p)
        {
            contextMenu.Show(treeview, p);
        }

        override public void Refresh(bool forceRefresh)
        {
            if (forceRefresh || isFirstTime)
            {
                isFirstTime = false;
                Nodes.Clear();
                FillWithChildren();
            }
        }

        public void FillWithChildren()
        {
            try
            {
                FeedResponse<Offer> feedOffers = client.ReadOffersFeedAsync().Result;

                foreach (var sp in feedOffers)
                {
                    DocumentNode node = new DocumentNode(client, sp, ResourceType.Offer);
                    Nodes.Add(node);
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
                IDocumentQuery<dynamic> q = client.CreateOfferQuery(queryText).AsDocumentQuery();

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