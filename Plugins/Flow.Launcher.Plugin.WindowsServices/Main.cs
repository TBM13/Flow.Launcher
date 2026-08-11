using System.ServiceProcess;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;

namespace Flow.Launcher.Plugin.WindowsServices;

public static class PluginMetadataDefinition
{
    public static readonly PluginMetadata Metadata = new()
    {
        ID = "42f63e8d-3d3d-4b4b-9e91-c6094bf240ec",
        ActionKeywords = ["svc"],
        Name = "Windows Services Manager",
        Description = "Manage Windows services.",
        Author = "TBM13",
        Version = "1.1.1",
        IcoPath = "Images/Plugin.WindowsServices.png",

        Plugin = new Main()
    };
}

public class Main : IPlugin, IContextMenu
{
    public static PluginInitContext Context { get; private set; } = null!;

    public void Init(PluginInitContext context)
    {
        Context = context;
    }

    public List<Result>? Query(Query query)
    {
        if (query.IsHomeQuery)
            return null;

        return [.. ServiceHelper.Search(query.Search)];
    }

    private bool EnableService(ServiceResult service, Action action)
    {
        try
        {
            ServiceHelper.ChangeStatus(service, action);
        }
        catch (Exception e)
        {
            Context.API.ShowMsgError("Failed to change startup type", e.ToString());
            return false;
        }

        if (!service.IsRunning)
        {
            try
            {
                ServiceHelper.ChangeStatus(service, Action.Start);
            }
            catch (Exception e)
            {
                Context.API.ShowMsgError("Failed to start service", e.ToString());
                return false;
            }
        }

        Context.API.ReQuery();
        return true;
    }

    private bool DisableService(ServiceResult service)
    {
        try
        {
            ServiceHelper.ChangeStatus(service, Action.Disable);
        }
        catch (Exception e)
        {
            Context.API.ShowMsgError("Failed to change startup type", e.ToString());
            return false;
        }

        if (service.IsRunning)
        {
            try
            {
                ServiceHelper.ChangeStatus(service, Action.Stop);
            }
            catch (Exception e)
            {
                Context.API.ShowMsgError("Failed to stop service", e.ToString());
                return false;
            }
        }

        Context.API.ReQuery();
        return true;
    }

    public List<Result> LoadContextMenus(Result result)
    {
        if (result.ContextData is not ServiceResult service)
            throw new InvalidOperationException("Unexpected context data type");

        List<Result> results = [];
        if (service.IsRunning)
        {
            if (service.StartType != ServiceStartMode.Disabled)
            {
                results.Add(new Result()
                {
                    Title = "Restart",
                    IconOrGlyph = "\xe777",
                    Action = c =>
                    {
                        try
                        {
                            ServiceHelper.ChangeStatus(service, Action.Restart);
                        }
                        catch (Exception e)
                        {
                            Context.API.ShowMsgError("Failed to restart service", e.ToString());
                            return false;
                        }

                        Context.API.ReQuery();
                        return true;
                    }
                });
            }

            results.Add(new Result()
            {
                Title = "Stop",
                IconOrGlyph = "\xe769",
                Action = c =>
                {
                    try
                    {
                        ServiceHelper.ChangeStatus(service, Action.Stop);
                    }
                    catch (Exception e)
                    {
                        Context.API.ShowMsgError("Failed to stop service", e.ToString());
                        return false;
                    }

                    Context.API.ReQuery();
                    return true;
                }
            });
        }
        else if (service.StartType != ServiceStartMode.Disabled)
        {
            results.Add(new Result()
            {
                Title = "Start",
                IconOrGlyph = "\xe768",
                Action = c =>
                {
                    try
                    {
                        ServiceHelper.ChangeStatus(service, Action.Start);
                    }
                    catch (Exception e)
                    {
                        Context.API.ShowMsgError("Failed to start service", e.ToString());
                        return false;
                    }

                    Context.API.ReQuery();
                    return true;
                }
            });
        }

        if (service.StartType == ServiceStartMode.Disabled)
        {
            results.Add(new Result()
            {
                Title = "Enable (manual)",
                SubTitle = service.IsRunning ?
                    "Set startup type to manual" :
                    "Set startup type to manual & start the service",
                IconOrGlyph = "\xEB49",
                Action = c => EnableService(service, Action.EnableManual)
            });

            results.Add(new Result()
            {
                Title = "Enable (automatic)",
                SubTitle = service.IsRunning ?
                    "Set startup type to automatic" :
                    "Set startup type to automatic & start the service",
                IconOrGlyph = "\xEB49",
                Action = c => EnableService(service, Action.EnableAutomatic)
            });

            results.Add(new Result()
            {
                Title = "Enable (automatic delayed)",
                SubTitle = service.IsRunning ?
                    "Set startup type to automatic delayed" :
                    "Set startup type to automatic delayed & start the service",
                IconOrGlyph = "\xEB49",
                Action = c => EnableService(service, Action.EnableAutomaticDelayed)
            });
        }
        else
        {
            results.Add(new Result()
            {
                Title = "Disable",
                SubTitle = service.IsRunning ?
                    "Set startup type to disabled & stop the service" :
                    "Set startup type to disabled",
                IconOrGlyph = "\xEB4A",
                Action = c => DisableService(service)
            });
        }

        return results;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
