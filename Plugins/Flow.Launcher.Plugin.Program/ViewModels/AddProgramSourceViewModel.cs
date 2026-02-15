using System;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Plugins;
using Flow.Launcher.Plugin.Program.Views;
using Flow.Launcher.Plugin.Program.Views.Models;

namespace Flow.Launcher.Plugin.Program.ViewModels
{
    public class AddProgramSourceViewModel(PluginInitContext context, Settings settings) : ObservableObject
    {
        private bool _enabled = true;
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                StatusModified = true;
                OnPropertyChanged();
            }
        }

        private string _location = string.Empty;
        public string Location
        {
            get => _location;
            set
            {
                _location = value;
                LocationModified = true;
                OnPropertyChanged();
            }
        }

        public Settings Settings { get; } = settings;
        public ProgramSource Source { get; }
        public IPublicAPI API { get; } = context.API;
        public string AddBtnText { get; } = Localize.Settings_Add;
        private bool LocationModified = false;
        private bool StatusModified = false;
        public bool IsCustomSource { get; } = true;
        public bool IsNotCustomSource => !IsCustomSource;

        public AddProgramSourceViewModel(PluginInitContext context, Settings settings, ProgramSource programSource) : this(context, settings)
        {
            Source = programSource;
            _enabled = Source.Enabled;
            _location = Source.Location;
            AddBtnText = Localize.Settings_Update;
            IsCustomSource = Settings.ProgramSources.Any(x => x.UniqueIdentifier == Source.UniqueIdentifier);
        }

        public void Browse()
        {
            throw new NotImplementedException();
            /*var dialog = new FolderBrowserDialog();
            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                Location = dialog.SelectedPath;
            }*/
        }

        public (bool modified, string message) AddProgramSource()
        {
            if (!Directory.Exists(Location))
            {
                return (false, Localize.Error_InvalidPath);
            }
            else if (DuplicateSource(Location))
            {
                return (false, Localize.ProgramSource_Duplicate);
            }
            else
            {
                var source = new ProgramSource(Location, Enabled);
                Settings.ProgramSources.Insert(0, source);
                ProgramSetting.ProgramSettingDisplayList.Add(source);
                return (true, null);
            }
        }

        public (bool modified, string message) UpdateProgramSource()
        {
            if (LocationModified)
            {
                if (!Directory.Exists(Location))
                {
                    return (false, Localize.Error_InvalidPath);
                }
                else if (DuplicateSource(Location))
                {
                    return (false, Localize.ProgramSource_Duplicate);
                }
                else
                {
                    Source.Location = Location;  // Changes UniqueIdentifier internally
                }
            }
            if (StatusModified)
            {
                Source.Enabled = Enabled;
            }
            return (StatusModified || LocationModified, null);
        }

        public (bool modified, string message) AddOrUpdate()
        {
            if (Source == null)
            {
                return AddProgramSource();
            }
            else
            {
                return UpdateProgramSource();
            }
        }

        public static bool DuplicateSource(string location)
        {
            return ProgramSetting.ProgramSettingDisplayList.Any(x => x.UniqueIdentifier.Equals(location, StringComparison.OrdinalIgnoreCase));
        }
    }
}
