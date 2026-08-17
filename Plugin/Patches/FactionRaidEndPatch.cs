using System.Reflection;
using EFT;
using HarmonyLib;
using MoreBotsAPI.Components;
using SPT.Reflection.Patching;

namespace MoreBotsAPI.Patches;

public class FactionRaidEndPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(EFT.EftClientBackendSession), nameof(EFT.EftClientBackendSession.LocalRaidEnded));
    }

    [PatchPostfix]
    public static void PatchPostfix()
    {
        MonoBehaviourSingleton<FactionManager>.Instance?.SendRevenges();
    }
}
