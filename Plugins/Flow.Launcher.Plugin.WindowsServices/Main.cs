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
            Context.API.ShowMsgError(Localize.Error_ChangeStartupTypeFail, e.ToString());
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
                Context.API.ShowMsgError(Localize.Error_StartServiceFail, e.ToString());
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
            Context.API.ShowMsgError(Localize.Error_ChangeStartupTypeFail, e.ToString());
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
                Context.API.ShowMsgError(Localize.Error_StopServiceFail, e.ToString());
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
                    Title = Localize.Action_RestartService,
                    Glyph = new(Glyph: "\xe777"),
                    Action = c =>
                    {
                        try
                        {
                            ServiceHelper.ChangeStatus(service, Action.Restart);
                        }
                        catch (Exception e)
                        {
                            Context.API.ShowMsgError(Localize.Error_RestartServiceFail, e.ToString());
                            return false;
                        }

                        Context.API.ReQuery();
                        return true;
                    }
                });
            }

            results.Add(new Result()
            {
                Title = Localize.Action_StopService,
                Glyph = new(Glyph: "\xe769"),
                Action = c =>
                {
                    try
                    {
                        ServiceHelper.ChangeStatus(service, Action.Stop);
                    }
                    catch (Exception e)
                    {
                        Context.API.ShowMsgError(Localize.Error_StopServiceFail, e.ToString());
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
                Title = Localize.Action_StartService,
                Glyph = new(Glyph: "\xe768"),
                Action = c =>
                {
                    try
                    {
                        ServiceHelper.ChangeStatus(service, Action.Start);
                    }
                    catch (Exception e)
                    {
                        Context.API.ShowMsgError(Localize.Error_StartServiceFail, e.ToString());
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
                Title = Localize.Action_EnableManual,
                SubTitle = service.IsRunning ?
                    Localize.Action_EnableManual_Description :
                    Localize.Action_EnableManualAndStart_Description,
                Glyph = new(Glyph: "\xEB49"),
                Action = c => EnableService(service, Action.EnableManual)
            });

            results.Add(new Result()
            {
                Title = Localize.Action_EnableAutomatic,
                SubTitle = service.IsRunning ?
                    Localize.Action_EnableAutomatic_Description :
                    Localize.Action_EnableAutomaticAndStart_Description,
                Glyph = new(Glyph: "\xEB49"),
                Action = c => EnableService(service, Action.EnableAutomatic)
            });

            results.Add(new Result()
            {
                Title = Localize.Action_EnableAutomaticDelayed,
                SubTitle = service.IsRunning ?
                    Localize.Action_EnableAutomaticDelayed_Description :
                    Localize.Action_EnableAutomaticDelayedAndStart_Description,
                Glyph = new(Glyph: "\xEB49"),
                Action = c => EnableService(service, Action.EnableAutomaticDelayed)
            });
        }
        else
        {
            results.Add(new Result()
            {
                Title = Localize.Action_Disable,
                SubTitle = service.IsRunning ?
                    Localize.Action_DisableAndStop_Description :
                    Localize.Action_Disable_Description,
                Glyph = new(Glyph: "\xEB4A"),
                Action = c => DisableService(service)
            });
        }

        return results;
    }
}
