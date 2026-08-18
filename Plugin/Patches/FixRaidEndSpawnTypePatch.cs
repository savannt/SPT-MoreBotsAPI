using EFT;
using EFT.Ballistics;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace MoreBotsAPI.Patches
{
    public class FixRaidEndSpawnTypePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EFT.BaseStatisticsManager), nameof(EFT.BaseStatisticsManager.OnDeath));
        }

        [PatchPostfix]
        protected static void PatchPostfix(Player player)
        {
            if (player?.Profile?.EftStats?.DeathCause == null) return;
            var role = player.Profile.EftStats.DeathCause.Role;
            if (CustomWildSpawnTypeManager.IsCustomWildSpawnType((int)role))
                player.Profile.EftStats.DeathCause.Role = WildSpawnType.assault;
        }
    }
}
