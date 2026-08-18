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

            // Register custom types in BotInternalSettingsController.ExcludedDifficulties
            // (SPT 4.1.2: replaces old LocalBotSettingsProviderClass.Dictionary_1)
            try
            {
                var bisc = eftAsm.GetType("BotInternalSettingsController");
                var excludedField = bisc?.GetField("ExcludedDifficulties", BindingFlags.Static | BindingFlags.Public);
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
                            Logger.LogInfo($"Added {botType.WildSpawnTypeName} to ExcludedDifficulties");
                        }
                    }
                }
                else
                {
                    Logger.LogWarning("Could not find BotInternalSettingsController.ExcludedDifficulties.");
                }
            }
            catch (Exception e) { Logger.LogWarning($"ExcludedDifficulties registration failed: {e.Message}"); }

            // Register custom types in WildSpawnTypeExtension._spawnTypeSettings
            // (SPT 4.1.2: replaces old BotSettingsRepoClass.Dictionary_0)
            // _spawnTypeSettings is private; WildSpawnTypeSettings ctor: (bool isBoss, bool isFollower, bool isHostileToEverybody, string scavRoleKey, ETagStatus phraseTag)
            try
            {
                var spawnTypeSettingsField = typeof(WildSpawnTypeExtension).GetField("_spawnTypeSettings", BindingFlags.Static | BindingFlags.NonPublic);
                var spawnTypeSettings = spawnTypeSettingsField?.GetValue(null) as System.Collections.IDictionary;
                var settingsType = eftAsm.GetType("WildSpawnTypeSettings");
                if (spawnTypeSettings != null && settingsType != null)
                {
                    foreach (var botType in CustomWildSpawnTypeManager.GetCustomWildSpawnTypes())
                    {
                        var spawnType = (WildSpawnType)botType.WildSpawnTypeValue;
                        if (!spawnTypeSettings.Contains(spawnType))
                        {
                            var entry = Activator.CreateInstance(settingsType, botType.IsBoss, botType.IsFollower, botType.IsHostileToEverybody, $"ScavRole/{botType.ScavRole}", (ETagStatus)0);
                            spawnTypeSettings.Add(spawnType, entry);
                            if (botType.CountAsBossForStatistics.HasValue)
                            {
                                var field = settingsType.GetField("CountAsBossForStatistics");
                                field?.SetValue(entry, (bool?)botType.CountAsBossForStatistics.Value);
                            }
                            Logger.LogInfo($"Registered WildSpawnTypeSettings for {botType.WildSpawnTypeName}");
                        }
                    }
                }
                else
                {
                    Logger.LogWarning($"Could not find WildSpawnTypeExtension._spawnTypeSettings (found={spawnTypeSettings != null}) or WildSpawnTypeSettings type (found={settingsType != null}).");
                }
            }
            catch (Exception e) { Logger.LogWarning($"WildSpawnTypeSettings registration failed: {e.Message}"); }

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
