using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.DocumentDBStudio.Util
{
    internal class AccountSettings
    {
        public ConnectionMode ConnectionMode;
        public bool IsNameBased;
        public string MasterKey;
        public Protocol Protocol;
    }
}