using System;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Allowed plugin languages
    /// </summary>
    public static class AllowedLanguage
    {
        public const string CSharp = "CSharp";
        public const string FSharp = "FSharp";

        /// <summary>
        /// Determines if this language is a .NET language
        /// </summary>
        public static bool IsDotNet(string language)
        {
            return language.Equals(CSharp, StringComparison.OrdinalIgnoreCase)
                || language.Equals(FSharp, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if this language is supported
        /// </summary>
        public static bool IsAllowed(string language)
        {
            return IsDotNet(language);
        }
    }
}
