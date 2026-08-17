using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using HarmonyLib;
using MoreBotsAPI.Components;
using MoreBotsAPI.Patches;
using Newtonsoft.Json;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using MoreBotsAPI.Interop;
using UnityEngine;

namespace MoreBotsAPI
{
    [BepInDependency("xyz.drakia.bigbrain", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("me.sol.sain", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(ClientInfo.GUID, ClientInfo.PluginName, ClientInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource LogSource;

        public static ConfigEntry<bool> DrawBotZones;

        public static List<BotZone> BotZones;

        public static bool FikaInitialized = false;

        public static string pluginPath = Path.Combine(Environment.CurrentDirectory, "BepInEx", "plugins", "MoreBotsAPI");

        // BaseUnityPlugin inherits MonoBehaviour, so you can use base unity functions like Awake() and Update()
        private void Awake()
        {
            // save the Logger to variable so we can use it elsewhere in the project
            LogSource = Logger;

            var eftAsm = typeof(BotOwner).Assembly;
            var defaultExcludedDifficulties = new List<BotDifficulty>
            {
                BotDifficulty.easy,
                BotDifficulty.hard,
                BotDifficulty.impossible
            };

            // Find LocalBotSettingsProviderClass equivalent via reflection
            try
            {
                var providerType = eftAsm.GetTypes().FirstOrDefault(t =>
                    t.IsAbstract && t.IsSealed &&
                    t.GetField("Dictionary_1", BindingFlags.Static | BindingFlags.Public) != null);
                var excludedField = providerType?.GetField("Dictionary_1", BindingFlags.Static | BindingFlags.Public);
                var excludedDifficulties = excludedField?.GetValue(null) as Dictionary<WildSpawnType, List<BotDifficulty>>;
                if (excludedDifficulties != null)
                {
                    foreach (var botType in CustomWildSpawnTypeManager.GetCustomWildSpawnTypes())
                    {
                        var spawnType = (WildSpawnType)botType.WildSpawnTypeValue;
                        if (!excludedDifficulties.ContainsKey(spawnType))
                        {
                            var difficulties = botType.ExcludedDifficulties != null
                                ? botType.ExcludedDifficulties.ConvertAll(d => (BotDifficulty)d)
                                : defaultExcludedDifficulties;
                            excludedDifficulties.Add(spawnType, difficulties);
                            Logger.LogInfo($"Added {botType.WildSpawnTypeName} : {botType.WildSpawnTypeValue} to excluded difficulties");
                        }
                    }
                }
                else
                {
                    Logger.LogWarning("Could not find excluded difficulties dictionary — bot spawn types may not load at all difficulties.");
                }
            }
            catch (Exception e) { Logger.LogWarning($"Excluded difficulties registration failed: {e.Message}"); }

            // Find BotSettingsRepoClass equivalent via reflection
            try
            {
                var repoType = eftAsm.GetTypes().FirstOrDefault(t =>
                    t.IsAbstract && t.IsSealed &&
                    t.GetField("Dictionary_0", BindingFlags.Static | BindingFlags.Public) != null);
                var repoField = repoType?.GetField("Dictionary_0", BindingFlags.Static | BindingFlags.Public);
                var repoDict = repoField?.GetValue(null) as System.Collections.IDictionary;
                if (repoDict != null)
                {
                    // Find GClass790 equivalent - constructor(bool, bool, bool, string, ETagStatus)
                    var settingsType = eftAsm.GetTypes().FirstOrDefault(t =>
                        !t.IsAbstract && t.GetConstructors().Any(c =>
                        {
                            var p = c.GetParameters();
                            return p.Length >= 4 && p[0].ParameterType == typeof(bool) && p[1].ParameterType == typeof(bool) && p[3].ParameterType == typeof(string);
                        }));
                    if (settingsType != null)
                    {
                        foreach (var botType in CustomWildSpawnTypeManager.GetCustomWildSpawnTypes())
                        {
                            var spawnType = (WildSpawnType)botType.WildSpawnTypeValue;
                            if (!repoDict.Contains(spawnType))
                            {
                                var entry = Activator.CreateInstance(settingsType, botType.IsBoss, botType.IsFollower, botType.IsHostileToEverybody, $"ScavRole/{botType.ScavRole}", (ETagStatus)0);
                                repoDict.Add(spawnType, entry);
                                if (botType.CountAsBossForStatistics.HasValue)
                                {
                                    var prop = settingsType.GetProperty("CountAsBossForStatistics");
                                    prop?.SetValue(entry, botType.CountAsBossForStatistics.Value);
                                }
                            }
                        }
                    }
                }
                else
                {
                    Logger.LogWarning("Could not find BotSettingsRepo dictionary — custom bots may not have proper settings.");
                }
            }
            catch (Exception e) { Logger.LogWarning($"BotSettingsRepo registration failed: {e.Message}"); }

            new TarkovInitPatch().Enable(); //For Sain stuff
            new FixRaidEndSpawnTypePatch().Enable();
            new StandartBotBrainActivatePatch().Enable();
            new SuitableFollowersListPatch().Enable();
            new FenceLoyaltyWarnPatch().Enable();
            new NewGamePatch().Enable();
            new BotsControllerInitPatch().Enable();
            new FactionRaidEndPatch().Enable();
            new BotsGroupIsPlayerEnemyPatch().Enable();
            
            CheckPlugins();
            
            this.GetOrAddComponent<HuntManager>();
            this.GetOrAddComponent<FactionManager>();

            InitConfig();

            // Replace WildSpawnType JSON converter — find JsonSerializerSettingsClass by reflection
            try
            {
                var settingsClassType = eftAsm.GetTypes().FirstOrDefault(t =>
                    t.IsAbstract && t.IsSealed &&
                    t.GetProperty("Converters", BindingFlags.Static | BindingFlags.Public) != null);
                var convertersProp = settingsClassType?.GetProperty("Converters", BindingFlags.Static | BindingFlags.Public);
                var converters = convertersProp?.GetValue(null) as JsonConverter[];
                if (converters != null)
                {
                    int idx = Array.FindIndex(converters, c => c.CanConvert(typeof(WildSpawnType)) && c.GetType().IsGenericType);
                    if (idx >= 0)
                    {
                        LogSource.LogInfo($"Replacing WildSpawnType converter at index {idx}: {converters[idx].GetType().Name}");
                        converters[idx] = new WildSpawnTypeFromIntConverter<WildSpawnType>(true);
                    }
                    else
                    {
                        LogSource.LogWarning("Could not find WildSpawnType converter to replace.");
                    }
                }
            }
            catch (Exception e) { LogSource.LogWarning($"JSON converter replacement failed: {e.Message}"); }
        }

        public void CheckPlugins()
        {
            if (Chainloader.PluginInfos.ContainsKey("com.fika.core"))
            {
                FikaInitialized = true;
                
                FikaInterop.InitializeInterop();
            }
        }

        private void InitConfig()
        {
            DrawBotZones = Config.Bind(
                "Bot Zones",
                "Draw Bot Zones",
                false,
                "Draw Bot Zones"
                );

            DrawBotZones.SettingChanged += OnDrawBotZones;
        }

        private void OnDrawBotZones(object sender, EventArgs e)
        {
            if (DrawBotZones.Value)
            {
                ZoneDebugComponent.Enable();
            }
            else
            {
                ZoneDebugComponent.Disable();
            }
        }
    }

    internal class NewGamePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));

        [PatchPrefix]
        public static void PatchPrefix()
        {
            ZoneDebugComponent.Enable();
        }
    }
}
