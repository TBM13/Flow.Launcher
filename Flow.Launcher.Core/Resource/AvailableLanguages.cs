using System.Collections.Generic;

namespace Flow.Launcher.Core.Resource
{
    internal static class AvailableLanguages
    {
        public static Language English = new("en", "English");

        public static List<Language> GetAvailableLanguages()
        {
            List<Language> languages = new List<Language>
            {
                English
            };
            return languages;
        }
    }
}
