
using System;
using System.Collections.Generic;
using System.ServiceProcess;

namespace Flow.Launcher.Plugin.WindowsServices;

public class Main : IPlugin, IContextMenu
{
    internal static PluginInitContext Context { get; private set; } = null!;

    public void Init(PluginInitContext context)
    {
        Context = context;
    }

    public List<Result?> Query(Query query)
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

    private static bool DisableService(ServiceResult service)
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
        var service = (ServiceResult)result.ContextData;
        List<Result> results = [];

        if (service.IsRunning)
        {
            if (service.StartType != ServiceStartMode.Disabled)
            {
                results.Add(new Result()
                {
                    Title = "Restart",
                    Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe777"),
                    Action = (c) =>
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
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe769"),
                Action = (c) =>
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
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe768"),
                Action = (c) =>
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
            string startServiceStr = service.IsRunning
                ? string.Empty : " & start the service";

            results.Add(new Result()
            {
                Title = "Enable (manual)",
                SubTitle = "Set startup type to manual" + startServiceStr,
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xEB49"),
                Action = c => EnableService(service, Action.EnableManual)
            });

            results.Add(new Result()
            {
                Title = "Enable (automatic)",
                SubTitle = "Set startup type to automatic" + startServiceStr,
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xEB49"),
                Action = c => EnableService(service, Action.EnableAutomatic)
            });

            results.Add(new Result()
            {
                Title = "Enable (automatic delayed)",
                SubTitle = "Set startup type to automatic delayed" + startServiceStr,
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xEB49"),
                Action = c => EnableService(service, Action.EnableAutomaticDelayed)
            });
        }
        else
        {
            string stopServiceStr = service.IsRunning
                ? " & stop the service" : string.Empty;

            results.Add(new Result()
            {
                Title = "Disable",
                SubTitle = "Set startup type to disabled" + stopServiceStr,
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xEB4A"),
                Action = c => DisableService(service)
            });
        }

        return results;
    }
}

