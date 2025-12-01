using System;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Allowed plugin languages
    /// </summary>
    public static class AllowedLanguage
    {
        /// <summary>
        /// C#
        /// </summary>
        public const string CSharp = "CSharp";

        /// <summary>
        /// F#
        /// </summary>
        public const string FSharp = "FSharp";

        /// <summary>
        /// Determines if this language is a .NET language
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public static bool IsDotNet(string language)
        {
            return language.Equals(CSharp, StringComparison.OrdinalIgnoreCase)
                || language.Equals(FSharp, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if this language is supported
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public static bool IsAllowed(string language)
        {
            return IsDotNet(language);
        }
    }
}
