
using System;
using System.Collections.Generic;
using System.ServiceProcess;

namespace Flow.Launcher.Plugin.WindowsServices;

public class Main : IPlugin, IContextMenu
{
    public const string PLUGIN_ICON = "Images\\app.png";

    internal static PluginInitContext Context { get; private set; } = null!;

    public void Init(PluginInitContext context)
    {
        Context = context;
    }

    public List<Result> Query(Query query)
    {
        return [.. ServiceHelper.Search(query.Search)];
    }

    private static bool EnableService(ServiceResult service, Action action)
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

    private static bool DisableService(ServiceResult service)
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
        var service = (ServiceResult)result.ContextData;
        List<Result> results = [];

        if (service.IsRunning)
        {
            if (service.StartType != ServiceStartMode.Disabled)
            {
                results.Add(new Result()
                {
                    Title = Localize.Action_RestartService,
                    Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe777"),
                    Action = (c) =>
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
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe769"),
                Action = (c) =>
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
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe768"),
                Action = (c) =>
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
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xEB49"),
                Action = c => EnableService(service, Action.EnableManual)
            });

            results.Add(new Result()
            {
                Title = Localize.Action_EnableAutomatic,
                SubTitle = service.IsRunning ?
                    Localize.Action_EnableAutomatic_Description :
                    Localize.Action_EnableAutomaticAndStart_Description,
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xEB49"),
                Action = c => EnableService(service, Action.EnableAutomatic)
            });

            results.Add(new Result()
            {
                Title = Localize.Action_EnableAutomaticDelayed,
                SubTitle = service.IsRunning ?
                    Localize.Action_EnableAutomaticDelayed_Description :
                    Localize.Action_EnableAutomaticDelayedAndStart_Description,
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xEB49"),
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
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xEB4A"),
                Action = c => DisableService(service)
            });
        }

        return results;
    }
}
