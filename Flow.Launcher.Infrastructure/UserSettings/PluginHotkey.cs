using System;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class CustomPluginHotkey(string hotkey, string actionKeyword) : BaseModel
    {
        public string Hotkey { get; set; } = hotkey;
        public string ActionKeyword { get; set; } = actionKeyword;

        public override bool Equals(object other)
        {
            if (other is CustomPluginHotkey otherHotkey)
            {
                return Hotkey == otherHotkey.Hotkey && ActionKeyword == otherHotkey.ActionKeyword;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Hotkey, ActionKeyword);
        }
    }
}
