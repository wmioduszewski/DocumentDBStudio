using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Azure.DocumentDBStudio.Util;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;

namespace Microsoft.Azure.DocumentDBStudio.TreeNodeElems
{
    class DocumentCollectionNode : NodeBase
    {
        private readonly DocumentClient _client;
        private readonly ContextMenu _contextMenu = new ContextMenu();
        private string _currentContinuation;
        private CommandContext _currentQueryCommandContext;

        public DocumentCollectionNode(DocumentClient client, DocumentCollection coll)
        {
            Text = coll.Id;
            Tag = coll;
            this._client = client;
            ImageKey = "SystemFeed";
            SelectedImageKey = "SystemFeed";

            Nodes.Add(new StoredProceduresNode(this._client));
            Nodes.Add(new UdfNode(this._client));
            Nodes.Add(new TriggersNode(this._client));
            Nodes.Add(new ConflictsNode(this._client));

            MenuItem myMenuItem5 = new MenuItem("Read DocumentCollection");
            myMenuItem5.Click += myMenuItemReadDocumentCollection_Click;
            _contextMenu.MenuItems.Add(myMenuItem5);

            MenuItem myMenuItem3 = new MenuItem("Replace DocumentCollection");
            myMenuItem3.Click += myMenuItemUpdateDocumentCollection_Click;
            _contextMenu.MenuItems.Add(myMenuItem3);

            MenuItem myMenuItem6 = new MenuItem("Delete DocumentCollection");
            myMenuItem6.Click += myMenuItemDeleteDocumentCollection_Click;
            _contextMenu.MenuItems.Add(myMenuItem6);

            _contextMenu.MenuItems.Add("-");

            MenuItem myMenuItem = new MenuItem("Create Document");
            myMenuItem.Click += myMenuItemAddDocument_Click;
            _contextMenu.MenuItems.Add(myMenuItem);
            MenuItem myMenuItem9 = new MenuItem("Create Document From File");
            myMenuItem9.Click += myMenuItemAddDocumentFromFile_Click;
            _contextMenu.MenuItems.Add(myMenuItem9);
            MenuItem myMenuItem4 = new MenuItem("Create Multiple Documents From Folder");
            myMenuItem4.Click += myMenuItemAddDocumentsFromFolder_Click;
            _contextMenu.MenuItems.Add(myMenuItem4);
            MenuItem myMenuItem1 = new MenuItem("Refresh Documents feed");
            myMenuItem1.Click += (sender, e) => Refresh(true);
            _contextMenu.MenuItems.Add(myMenuItem1);
            MenuItem myMenuItem2 = new MenuItem("Query Documents");
            myMenuItem2.Click += myMenuItemQueryDocument_Click;
            _contextMenu.MenuItems.Add(myMenuItem2);
        }

        public string GlobalQuery { get; set; }

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
                Nodes.Add(new StoredProceduresNode(_client));
                Nodes.Add(new UdfNode(_client));
                Nodes.Add(new TriggersNode(_client));
                Nodes.Add(new ConflictsNode(_client));

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
                            _client.CreateDocumentQuery(
                                (Tag as DocumentCollection).GetLink(_client), GlobalQuery, feedOptions)
                                .AsDocumentQuery();
                        docs = q.ExecuteNextAsync().Result;
                    }
                    else
                    {
                        docs = _client.ReadDocumentFeedAsync(((DocumentCollection) Tag).GetLink(_client)).Result;
                    }
                }

                foreach (var doc in docs)
                {
                    DocumentNode nodeBase = new DocumentNode(_client, doc, ResourceType.Document);
                    Nodes.Add(nodeBase);
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
                            _client.ReadDocumentCollectionAsync(((Resource) Tag).GetLink(_client),
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
                IQueryable<Offer> offerQuery = from offer in _client.CreateOfferQuery()
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
                        replaceResponse = await _client.ReplaceOfferAsync(offerToReplace);
                    }
                }

                // #2: Update collection if necessary
                DocumentCollection collToChange = optional as DocumentCollection;
                collToChange.IndexingPolicy = (IndexingPolicy) coll.IndexingPolicy.Clone();

                ResourceResponse<DocumentCollection> response;
                using (PerfStatus.Start("ReplaceDocumentCollection"))
                {
                    response = await _client.ReplaceDocumentCollectionExAsync(coll, requestionOptions);
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
                            _client.DeleteDocumentCollectionAsync(coll.GetLink(_client),
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
                                    _client.CreateDocumentAsync((Tag as DocumentCollection).GetLink(_client), document,
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
            _currentQueryCommandContext = new CommandContext();
            _currentQueryCommandContext.IsFeed = true;

            // reset continuation token
            _currentContinuation = null;

            Program.GetMain()
                .SetCrudContext(this,
                    string.Format(CultureInfo.InvariantCulture, "Query Documents from Collection {0}",
                        (Tag as DocumentCollection).Id),
                    false, "select * from c", QueryDocuments, _currentQueryCommandContext);
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
                            _client.CreateDocumentAsync((Tag as DocumentCollection).GetLink(_client), document,
                                Program.GetMain().GetRequestOptions());
                }

                Nodes.Add(new DocumentNode(_client, newdocument.Resource, ResourceType.Document));

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

                if (!string.IsNullOrEmpty(_currentContinuation) && string.IsNullOrEmpty(feedOptions.RequestContinuation))
                {
                    feedOptions.RequestContinuation = _currentContinuation;
                }

                q =
                    _client.CreateDocumentQuery((Tag as DocumentCollection).GetLink(_client), queryText, feedOptions)
                        .AsDocumentQuery();

                Stopwatch sw = Stopwatch.StartNew();

                FeedResponse<dynamic> r;
                using (PerfStatus.Start("QueryDocument"))
                {
                    r = await q.ExecuteNextAsync();
                }
                sw.Stop();
                _currentContinuation = r.ResponseContinuation;
                _currentQueryCommandContext.HasContinuation = !string.IsNullOrEmpty(_currentContinuation);
                _currentQueryCommandContext.QueryStarted = true;

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
                Program.GetMain().SetNextPageVisibility(_currentQueryCommandContext);
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