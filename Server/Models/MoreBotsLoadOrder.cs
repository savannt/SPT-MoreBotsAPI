using SPTarkov.Server.Core.DI;

namespace MoreBotsServer;

public static class MoreBotsLoadOrder
{
    public const int LoadFactions = OnLoadOrder.PostLoad + 80080;

    public const int LoadBots = OnLoadOrder.PostLoad + 80085;
}