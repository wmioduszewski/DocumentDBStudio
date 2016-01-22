using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Azure.DocumentDBStudio.Util;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.DocumentDBStudio.TreeNodeElems
{
    class DocumentNode : NodeBase
    {
        private readonly DocumentClient _client;
        private readonly ContextMenu _contextMenu = new ContextMenu();
        private readonly ResourceType _resourceType = 0;

        public DocumentNode(DocumentClient client, dynamic document, ResourceType resoureType)
        {
            _resourceType = resoureType;
            if (_resourceType != ResourceType.Offer)
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
            this._client = client;

            MenuItem myMenuItem0 = new MenuItem("Read " + _resourceType);
            myMenuItem0.Click += myMenuItemRead_Click;
            _contextMenu.MenuItems.Add(myMenuItem0);

            if (_resourceType != ResourceType.Conflict && _resourceType != ResourceType.Offer)
            {
                MenuItem myMenuItem1 = new MenuItem("Replace " + _resourceType);
                myMenuItem1.Click += myMenuItemUpdate_Click;
                _contextMenu.MenuItems.Add(myMenuItem1);
            }

            if (_resourceType != ResourceType.Offer)
            {
                MenuItem myMenuItem = new MenuItem("Delete " + _resourceType);
                myMenuItem.Click += myMenuItemDelete_Click;
                _contextMenu.MenuItems.Add(myMenuItem);
            }

            if (_resourceType == ResourceType.Permission)
            {
                ImageKey = "Permission";
                SelectedImageKey = "Permission";
            }
            else if (_resourceType == ResourceType.Attachment)
            {
                ImageKey = "Attachment";
                SelectedImageKey = "Attachment";

                MenuItem myMenuItem2 = new MenuItem("Download media");
                myMenuItem2.Click += myMenuItemDownloadMedia_Click;
                _contextMenu.MenuItems.Add(myMenuItem2);

                MenuItem myMenuItem3 = new MenuItem("Render media");
                myMenuItem3.Click += myMenuItemRenderMedia_Click;
                _contextMenu.MenuItems.Add(myMenuItem3);
            }
            else if (_resourceType == ResourceType.StoredProcedure || _resourceType == ResourceType.Trigger ||
                     _resourceType == ResourceType.UserDefinedFunction)
            {
                ImageKey = "Javascript";
                SelectedImageKey = "Javascript";
                if (_resourceType == ResourceType.StoredProcedure)
                {
                    MenuItem myMenuItem2 = new MenuItem("Execute " + _resourceType);
                    myMenuItem2.Click += myMenuItemExecuteSP_Click;
                    _contextMenu.MenuItems.Add(myMenuItem2);
                }
            }
            else if (_resourceType == ResourceType.User)
            {
                ImageKey = "User";
                SelectedImageKey = "User";

                Nodes.Add(new PermissionsNode(this._client));
            }
            else if (_resourceType == ResourceType.Document)
            {
                Nodes.Add(new TreeNode("Fake"));

                _contextMenu.MenuItems.Add("-");

                MenuItem myMenuItem3 = new MenuItem("Create attachment");
                myMenuItem3.Click += myMenuItemAttachment_Click;
                _contextMenu.MenuItems.Add(myMenuItem3);

                MenuItem myMenuItem4 = new MenuItem("Create attachment from file");
                myMenuItem4.Click += myMenuItemAttachmentFromFile_Click;
                _contextMenu.MenuItems.Add(myMenuItem4);
            }
            else if (_resourceType == ResourceType.Conflict)
            {
                ImageKey = "Conflict";
                SelectedImageKey = "Conflict";
            }
            else if (_resourceType == ResourceType.Offer)
            {
                ImageKey = "Offer";
                SelectedImageKey = "Offer";
            }
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

                if (_resourceType == ResourceType.User)
                {
                    Nodes.Add(new PermissionsNode(_client));
                }
                else if (_resourceType == ResourceType.Document)
                {
                    FillWithChildren();
                }
            }
        }

        public string GetBody()
        {
            string body = null;
            if (_resourceType == ResourceType.StoredProcedure)
            {
                body = "\nThe storedprocedure Javascript function: \n\n" + (Tag as StoredProcedure).Body;
            }
            else if (_resourceType == ResourceType.Trigger)
            {
                body = "\nThe trigger Javascript function: \n\n" + (Tag as Trigger).Body;
            }
            else if (_resourceType == ResourceType.UserDefinedFunction)
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
                    attachments = _client.ReadAttachmentFeedAsync((Tag as Document).GetLink(_client)).Result;
                }
                foreach (var attachment in attachments)
                {
                    DocumentNode nodeBase = new DocumentNode(_client, attachment, ResourceType.Attachment);
                    Nodes.Add(nodeBase);
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
            if (_resourceType == ResourceType.StoredProcedure)
            {
                Program.GetMain()
                    .SetCrudContext(this, "Replace " + _resourceType, true, (Tag as StoredProcedure).Body,
                        UpdateNode);
            }
            else if (_resourceType == ResourceType.Trigger)
            {
                Program.GetMain()
                    .SetCrudContext(this, "Replace " + _resourceType, true, (Tag as Trigger).Body, UpdateNode);
            }
            else if (_resourceType == ResourceType.UserDefinedFunction)
            {
                Program.GetMain()
                    .SetCrudContext(this, "Replace " + _resourceType, true, (Tag as UserDefinedFunction).Body,
                        UpdateNode);
            }
            else
            {
                string x = Tag.ToString();
                Program.GetMain().SetCrudContext(this, "Replace " + _resourceType, false, x, UpdateNode);
            }
        }

        async void myMenuItemRead_Click(object sender, EventArgs eventArg)
        {
            try
            {
                if (_resourceType == ResourceType.Offer)
                {
                    ResourceResponse<Offer> rr;
                    using (PerfStatus.Start("ReadOffer"))
                    {
                        rr = await _client.ReadOfferAsync(((Resource) Tag).SelfLink);
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.Document)
                {
                    ResourceResponse<Document> rr;
                    using (PerfStatus.Start("ReadDocument"))
                    {
                        rr =
                            await
                                _client.ReadDocumentAsync(((Resource) Tag).GetLink(_client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.Conflict)
                {
                    ResourceResponse<Conflict> rr;
                    using (PerfStatus.Start("ReadConflict"))
                    {
                        rr =
                            await
                                _client.ReadConflictAsync(((Resource) Tag).GetLink(_client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.Attachment)
                {
                    ResourceResponse<Attachment> rr;
                    using (PerfStatus.Start("ReadAttachment"))
                    {
                        rr =
                            await
                                _client.ReadAttachmentAsync(((Resource) Tag).GetLink(_client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.User)
                {
                    ResourceResponse<User> rr;
                    using (PerfStatus.Start("ReadUser"))
                    {
                        rr =
                            await
                                _client.ReadUserAsync(((Resource) Tag).GetLink(_client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.Permission)
                {
                    ResourceResponse<Permission> rr;
                    using (PerfStatus.Start("ReadPermission"))
                    {
                        rr =
                            await
                                _client.ReadPermissionAsync(((Resource) Tag).GetLink(_client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.StoredProcedure)
                {
                    ResourceResponse<StoredProcedure> rr;
                    using (PerfStatus.Start("ReadStoredProcedure"))
                    {
                        rr =
                            await
                                _client.ReadStoredProcedureAsync(((Resource) Tag).GetLink(_client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.Trigger)
                {
                    ResourceResponse<Trigger> rr;
                    using (PerfStatus.Start("ReadTrigger"))
                    {
                        rr =
                            await
                                _client.ReadTriggerAsync(((Resource) Tag).GetLink(_client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.UserDefinedFunction)
                {
                    ResourceResponse<UserDefinedFunction> rr;
                    using (PerfStatus.Start("ReadUDF"))
                    {
                        rr =
                            await
                                _client.ReadUserDefinedFunctionAsync(((Resource) Tag).GetLink(_client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    // set the result window
                    string json = JsonConvert.SerializeObject(rr.Resource, Formatting.Indented);

                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else
                {
                    throw new ArgumentException("Unsupported resource type " + _resourceType);
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
                .SetCrudContext(this, "Create attachment for this document " + _resourceType, false, x,
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
                    rr = await _client.ReadMediaAsync(attachment.MediaLink);
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
                        rr = await _client.ReadMediaAsync(attachment.MediaLink);
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
                        MediaOptions options = new MediaOptions
                        {
                            ContentType = "application/octet-stream",
                            Slug = Path.GetFileName(ofd.FileName)
                        };

                        ResourceResponse<Attachment> rr;
                        using (PerfStatus.Start("CreateAttachment"))
                        {
                            rr = await _client.CreateAttachmentAsync((Tag as Document).GetLink(_client) + "/attachments",
                                stream, options);
                        }
                        string json = rr.Resource.ToString();

                        Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);

                        Nodes.Add(new DocumentNode(_client, rr.Resource, ResourceType.Attachment));
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
            Program.GetMain().SetCrudContext(this, "Execute " + _resourceType + " " +
                                                   (Tag as Resource).Id, false,
                "Here is the input parameters to the storedProcedure. Input each parameter as one line without quotation mark.",
                ExecuteStoredProcedure);
        }

        void myMenuItemDelete_Click(object sender, EventArgs e)
        {
            string x = Tag.ToString();
            CommandContext context = new CommandContext();
            context.IsDelete = true;
            Program.GetMain().SetCrudContext(this, "Delete " + _resourceType, false, x, DeleteNode, context);
        }

        async void AddAttachment(string text, object optional)
        {
            try
            {
                Attachment attachment = (Attachment) JsonConvert.DeserializeObject(text, typeof (Attachment));

                ResourceResponse<Attachment> rr;
                using (PerfStatus.Start("CreateAttachment"))
                {
                    rr = await _client.CreateAttachmentAsync((Tag as Resource).GetLink(_client),
                        attachment, Program.GetMain().GetRequestOptions());
                }
                string json = rr.Resource.ToString();

                Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);

                Nodes.Add(new DocumentNode(_client, rr.Resource, ResourceType.Attachment));
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
                    rr = await _client.ExecuteStoredProcedureAsync<dynamic>((Tag as Resource).GetLink(_client),
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
                if (_resourceType == ResourceType.Document)
                {
                    Document doc = (Document) JsonConvert.DeserializeObject(text, typeof (Document));
                    doc.SetReflectedPropertyValue("AltLink", (Tag as Document).GetAltLink());
                    ResourceResponse<Document> rr;
                    using (PerfStatus.Start("ReplaceDocument"))
                    {
                        rr =
                            await
                                _client.ReplaceDocumentAsync(doc.GetLink(_client), doc,
                                    Program.GetMain().GetRequestOptions());
                    }
                    json = rr.Resource.ToString();

                    Tag = rr.Resource;
                    Text = rr.Resource.Id;
                    // set the result window
                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.StoredProcedure)
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
                        rr = await _client.ReplaceStoredProcedureExAsync(sp, Program.GetMain().GetRequestOptions());
                    }
                    json = rr.Resource.ToString();
                    Tag = rr.Resource;
                    Text = rr.Resource.Id;
                    // set the result window
                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.User)
                {
                    User sp = (User) JsonConvert.DeserializeObject(text, typeof (User));
                    sp.SetReflectedPropertyValue("AltLink", (Tag as User).GetAltLink());
                    ResourceResponse<User> rr;
                    using (PerfStatus.Start("ReplaceUser"))
                    {
                        rr = await _client.ReplaceUserExAsync(sp, Program.GetMain().GetRequestOptions());
                    }
                    json = rr.Resource.ToString();
                    Tag = rr.Resource;
                    Text = rr.Resource.Id;
                    // set the result window
                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.Trigger)
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
                        rr = await _client.ReplaceTriggerExAsync(sp, Program.GetMain().GetRequestOptions());
                    }
                    json = rr.Resource.ToString();
                    Tag = rr.Resource;
                    Text = rr.Resource.Id;
                    // set the result window
                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.UserDefinedFunction)
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
                        rr = await _client.ReplaceUserDefinedFunctionExAsync(sp, Program.GetMain().GetRequestOptions());
                    }
                    json = rr.Resource.ToString();
                    Tag = rr.Resource;
                    Text = rr.Resource.Id;
                    // set the result window
                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.Permission)
                {
                    Permission sp = JsonSerializable.LoadFrom<Permission>(new MemoryStream(Encoding.UTF8.GetBytes(text)));
                    sp.SetReflectedPropertyValue("AltLink", (Tag as Permission).GetAltLink());
                    ResourceResponse<Permission> rr;
                    using (PerfStatus.Start("ReplacePermission"))
                    {
                        rr = await _client.ReplacePermissionExAsync(sp, Program.GetMain().GetRequestOptions());
                    }
                    json = rr.Resource.ToString();
                    Tag = rr.Resource;
                    Text = rr.Resource.Id;
                    // set the result window
                    Program.GetMain().SetResultInBrowser(json, null, false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.Attachment)
                {
                    Attachment sp = (Attachment) JsonConvert.DeserializeObject(text, typeof (Attachment));
                    sp.SetReflectedPropertyValue("AltLink", (Tag as Attachment).GetAltLink());
                    ResourceResponse<Attachment> rr;
                    using (PerfStatus.Start("ReplaceAttachment"))
                    {
                        rr = await _client.ReplaceAttachmentExAsync(sp, Program.GetMain().GetRequestOptions());
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
                if (_resourceType == ResourceType.Document)
                {
                    Document doc = (Document) Tag;
                    ResourceResponse<Document> rr;
                    using (PerfStatus.Start("DeleteDocument"))
                    {
                        rr =
                            await _client.DeleteDocumentAsync(doc.GetLink(_client), Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain().SetResultInBrowser(null, "Delete Document succeed!", false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.StoredProcedure)
                {
                    StoredProcedure sp = (StoredProcedure) Tag;
                    ResourceResponse<StoredProcedure> rr;
                    using (PerfStatus.Start("DeleteStoredProcedure"))
                    {
                        rr =
                            await
                                _client.DeleteStoredProcedureAsync(sp.GetLink(_client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain()
                        .SetResultInBrowser(null, "Delete StoredProcedure succeed!", false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.User)
                {
                    User sp = (User) Tag;
                    ResourceResponse<User> rr;
                    using (PerfStatus.Start("DeleteUser"))
                    {
                        rr = await _client.DeleteUserAsync(sp.GetLink(_client), Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain().SetResultInBrowser(null, "Delete User succeed!", false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.Trigger)
                {
                    Trigger sp = (Trigger) Tag;
                    ResourceResponse<Trigger> rr;
                    using (PerfStatus.Start("DeleteTrigger"))
                    {
                        rr = await _client.DeleteTriggerAsync(sp.GetLink(_client), Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain().SetResultInBrowser(null, "Delete Trigger succeed!", false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.UserDefinedFunction)
                {
                    UserDefinedFunction sp = (UserDefinedFunction) Tag;
                    ResourceResponse<UserDefinedFunction> rr;
                    using (PerfStatus.Start("DeleteUDF"))
                    {
                        rr =
                            await
                                _client.DeleteUserDefinedFunctionAsync(sp.GetLink(_client),
                                    Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain()
                        .SetResultInBrowser(null, "Delete UserDefinedFunction succeed!", false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.Permission)
                {
                    Permission sp = (Permission) Tag;
                    ResourceResponse<Permission> rr;
                    using (PerfStatus.Start("DeletePermission"))
                    {
                        rr =
                            await
                                _client.DeletePermissionAsync(sp.GetLink(_client), Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain().SetResultInBrowser(null, "Delete Permission succeed!", false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.Attachment)
                {
                    Attachment sp = (Attachment) Tag;
                    ResourceResponse<Attachment> rr;
                    using (PerfStatus.Start("DeleteAttachment"))
                    {
                        rr =
                            await
                                _client.DeleteAttachmentAsync(sp.GetLink(_client), Program.GetMain().GetRequestOptions());
                    }
                    Program.GetMain().SetResultInBrowser(null, "Delete Attachment succeed!", false, rr.ResponseHeaders);
                }
                else if (_resourceType == ResourceType.Conflict)
                {
                    Conflict sp = (Conflict) Tag;
                    ResourceResponse<Conflict> rr;
                    using (PerfStatus.Start("DeleteConlict"))
                    {
                        rr = await _client.DeleteConflictAsync(sp.GetLink(_client), Program.GetMain().GetRequestOptions());
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
}