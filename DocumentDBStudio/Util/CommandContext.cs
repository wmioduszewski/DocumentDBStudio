namespace Microsoft.Azure.DocumentDBStudio.Util
{
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
}