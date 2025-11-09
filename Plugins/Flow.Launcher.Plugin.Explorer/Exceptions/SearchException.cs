using System;

namespace Flow.Launcher.Plugin.Explorer.Exceptions
{
    public class SearchException : Exception
    {
        public SearchException(string message) : base(message) { }
        public SearchException(string message, Exception innerException) : base(message, innerException) { }

        public override string ToString()
        {
            return $"Search Exception:\n {base.ToString()}";
        }
    }
}
