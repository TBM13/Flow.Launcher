
using System;
using System.Collections.Generic;
using System.ServiceProcess;

namespace Flow.Launcher.Plugin.WindowsServices;

public class Main : IPlugin, IContextMenu, IPluginI18n
{
    public const string PLUGIN_ICON = "Images\\app.png";

    internal static PluginInitContext Context { get; private set; } = null!;

    public string GetTranslatedPluginTitle() => Localize.plugin_windowsservices_plugin_name();
    public string GetTranslatedPluginDescription() => Localize.plugin_windowsservices_plugin_description();
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
            Context.API.ShowMsgError(Localize.plugin_windowsservices_error_changeStartupTypeFail(), e.ToString());
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
                Context.API.ShowMsgError(Localize.plugin_windowsservices_error_startServiceFail(), e.ToString());
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
            Context.API.ShowMsgError(Localize.plugin_windowsservices_error_changeStartupTypeFail(), e.ToString());
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
                Context.API.ShowMsgError(Localize.plugin_windowsservices_error_stopServiceFail(), e.ToString());
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
                    Title = Localize.plugin_windowsservices_action_restartService(),
                    Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe777"),
                    Action = (c) =>
                    {
                        try
                        {
                            ServiceHelper.ChangeStatus(service, Action.Restart);
                        }
                        catch (Exception e)
                        {
                            Context.API.ShowMsgError(Localize.plugin_windowsservices_error_restartServiceFail(), e.ToString());
                            return false;
                        }

                        Context.API.ReQuery();
                        return true;
                    }
                });
            }

            results.Add(new Result()
            {
                Title = Localize.plugin_windowsservices_action_stopService(),
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe769"),
                Action = (c) =>
                {
                    try
                    {
                        ServiceHelper.ChangeStatus(service, Action.Stop);
                    }
                    catch (Exception e)
                    {
                        Context.API.ShowMsgError(Localize.plugin_windowsservices_error_stopServiceFail(), e.ToString());
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
                Title = Localize.plugin_windowsservices_action_startService(),
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe768"),
                Action = (c) =>
                {
                    try
                    {
                        ServiceHelper.ChangeStatus(service, Action.Start);
                    }
                    catch (Exception e)
                    {
                        Context.API.ShowMsgError(Localize.plugin_windowsservices_error_startServiceFail(), e.ToString());
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
                Title = Localize.plugin_windowsservices_action_enableManual(),
                SubTitle = service.IsRunning ?
                    Localize.plugin_windowsservices_action_enableManual_description() :
                    Localize.plugin_windowsservices_action_enableManualAndStart_description(),
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xEB49"),
                Action = c => EnableService(service, Action.EnableManual)
            });

            results.Add(new Result()
            {
                Title = Localize.plugin_windowsservices_action_enableAutomatic(),
                SubTitle = service.IsRunning ?
                    Localize.plugin_windowsservices_action_enableAutomatic_description() :
                    Localize.plugin_windowsservices_action_enableAutomaticAndStart_description(),
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xEB49"),
                Action = c => EnableService(service, Action.EnableAutomatic)
            });

            results.Add(new Result()
            {
                Title = Localize.plugin_windowsservices_action_enableAutomaticDelayed(),
                SubTitle = service.IsRunning ?
                    Localize.plugin_windowsservices_action_enableAutomaticDelayed_description() :
                    Localize.plugin_windowsservices_action_enableAutomaticDelayedAndStart_description(),
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xEB49"),
                Action = c => EnableService(service, Action.EnableAutomaticDelayed)
            });
        }
        else
        {
            results.Add(new Result()
            {
                Title = Localize.plugin_windowsservices_action_disable(),
                SubTitle = service.IsRunning ?
                    Localize.plugin_windowsservices_action_disableAndStop_description() :
                    Localize.plugin_windowsservices_action_disable_description(),
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xEB4A"),
                Action = c => DisableService(service)
            });
        }

        return results;
    }
}
