using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.Sys;

public class Settings : BaseModel
{
    public Settings()
    {
        if (Commands.Count > 0)
        {
            SelectedCommand = Commands[0];
        }
    }

    public ObservableCollection<Command> Commands { get; set; } = new ObservableCollection<Command>
    {
        new()
        {
            Key = "Shutdown",
            Keyword = "Shutdown"
        },
        new()
        {
            Key = "Restart",
            Keyword = "Restart"
        },
        new()
        {
            Key = "Restart With Advanced Boot Options",
            Keyword = "Restart With Advanced Boot Options"
        },
        new()
        {
            Key = "Log Off/Sign Out",
            Keyword = "Log Off/Sign Out"
        },
        new()
        {
            Key = "Lock",
            Keyword = "Lock"
        },
        new()
        {
            Key = "Sleep",
            Keyword = "Sleep"
        },
        new()
        {
            Key = "Hibernate",
            Keyword = "Hibernate"
        },
        new()
        {
            Key = "Empty Recycle Bin",
            Keyword = "Empty Recycle Bin"
        },
        new()
        {
            Key = "Open Recycle Bin",
            Keyword = "Open Recycle Bin"
        },
        new()
        {
            Key = "Exit",
            Keyword = "Exit"
        },
        new()
        {
            Key = "Settings",
            Keyword = "Settings"
        },
        new()
        {
            Key = "Toggle Game Mode",
            Keyword = "Toggle Game Mode"
        },
    };

    [JsonIgnore]
    public Command SelectedCommand { get; set; }
}
