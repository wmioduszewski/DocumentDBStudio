using System;
using System.Drawing;
using System.Dynamic;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Azure.DocumentDBStudio.Util;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;

namespace Microsoft.Azure.DocumentDBStudio.TreeNodeElems
{
    class UsersNode : NodeBase
    {
        private readonly DocumentClient _client;
        private readonly ContextMenu _contextMenu = new ContextMenu();

        public UsersNode(DocumentClient client)
        {
            Text = "Users";
            _client = client;
            Nodes.Add(new TreeNode("fake"));
            Tag = "This represents the Users feed. Right click to add user";
            ImageKey = "User";
            SelectedImageKey = "User";

            MenuItem myMenuItem = new MenuItem("Create User");
            myMenuItem.Click += myMenuItemAddUser_Click;
            _contextMenu.MenuItems.Add(myMenuItem);
            MenuItem myMenuItem1 = new MenuItem("Refresh Users feed");
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
                FeedResponse<User> sps;
                using (PerfStatus.Start("ReadUser"))
                {
                    sps = _client.ReadUserFeedAsync((Parent.Tag as Database).GetLink(_client)).Result;
                }
                foreach (var sp in sps)
                {
                    DocumentNode nodeBase = new DocumentNode(_client, sp, ResourceType.User);
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
                            _client.CreateUserAsync((Parent.Tag as Database).GetLink(_client), user,
                                Program.GetMain().GetRequestOptions());
                }
                Nodes.Add(new DocumentNode(_client, newUser.Resource, ResourceType.User));

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
}