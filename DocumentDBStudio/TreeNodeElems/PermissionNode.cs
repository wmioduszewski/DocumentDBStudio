using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Azure.DocumentDBStudio.Util;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.DocumentDBStudio.TreeNodeElems
{
    class PermissionsNode : NodeBase
    {
        private readonly DocumentClient _client;
        private readonly ContextMenu _contextMenu = new ContextMenu();

        public PermissionsNode(DocumentClient client)
        {
            Text = "Permissions";
            _client = client;
            Nodes.Add(new TreeNode("fake"));
            Tag = "This represents the Permissions feed. Right click to add permission";
            ImageKey = "Permission";
            SelectedImageKey = "Permission";

            MenuItem myMenuItem = new MenuItem("Create Permission");
            myMenuItem.Click += myMenuItemAddPermission_Click;
            _contextMenu.MenuItems.Add(myMenuItem);
            MenuItem myMenuItem1 = new MenuItem("Refresh Permissions feed");
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
                FeedResponse<Permission> sps;
                using (PerfStatus.Start("ReadPermission"))
                {
                    sps = _client.ReadPermissionFeedAsync((Parent.Tag as User).GetLink(_client)).Result;
                }

                foreach (var sp in sps)
                {
                    DocumentNode nodeBase = new DocumentNode(_client, sp, ResourceType.Permission);
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

        async Task AddPermission(string body, object id)
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
                            _client.CreatePermissionAsync((Parent.Tag as Resource).GetLink(_client), permission,
                                Program.GetMain().GetRequestOptions());
                }
                Nodes.Add(new DocumentNode(_client, newtpermission.Resource, ResourceType.Permission));

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
}