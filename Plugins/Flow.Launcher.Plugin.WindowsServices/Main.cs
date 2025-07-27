
using System;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.WindowsServices;

public class Main : IPlugin, IContextMenu
{
    private static PluginInitContext _context;

    public void Init(PluginInitContext context)
    {
        _context = context;
    }

    public List<Result> Query(Query query)
    {
        return [.. ServiceHelper.Search(query.Search)];
    }

    public List<Result> LoadContextMenus(Result result)
    {
        var service = (ServiceResult)result.ContextData;

        if (service.StartMode == System.ServiceProcess.ServiceStartMode.Disabled)
        {
            return
            [
                new Result()
                {
                    Title = "Service is disabled",
                    SubTitle = "Enable it to use start/restart/stop actions.",
                    Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xeb90"),
                }
            ];
        }

        if (!service.IsRunning)
        {
            return
            [
                 new Result()
                 {
                    Title = "Start",
                    Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe768"),
                    Action = (c) => {
                        try {
                            ServiceHelper.ChangeStatus(service, Action.Start);
                            return true;
                        }
                        catch (Exception e) {
                            _context.API.ShowMsgError("Failed to start service", e.ToString());
                            return false;
                        }
                    }
                }
            ];
        }

        return
        [
            new Result()
            {
                Title = "Restart",
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe777"),
                Action = (c) => {
                    try {
                        ServiceHelper.ChangeStatus(service, Action.Restart);
                        return true;
                    }
                    catch (Exception e) {
                        _context.API.ShowMsgError("Failed to restart service", e.ToString());
                        return false;
                    }
                }
            },

            new Result()
            {
                Title = "Stop",
                Glyph = new(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe769"),
                Action = (c) => {
                    try {
                        ServiceHelper.ChangeStatus(service, Action.Stop);
                        return true;
                    }
                    catch (Exception e) {
                        _context.API.ShowMsgError("Failed to stop service", e.ToString());
                        return false;
                    }
                }
            }
        ];
    }
}

