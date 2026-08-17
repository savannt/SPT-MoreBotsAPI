using DrakiaXYZ.BigBrain.Brains;
using EFT;
using HarmonyLib;
using SAIN.Attributes;
using SAIN.Preset;
using SAIN.Preset.BotSettings;
using SAIN.Preset.BotSettings.SAINSettings;
using System;
using System.Collections.Generic;
using System.Reflection;
using SAIN;

namespace MoreBotsAPI.Interop
{
    public class SAINInterop
    {
        public void Init()
        {
            Plugin.LogSource.LogInfo("Initializing SAIN interop for MoreBotsAPI...");
            //AddSAINLayers();
            CreateCustomBotTypes();
        }

        private static readonly string[] commonVanillaLayersToRemove = new string[]
        {
            "Help",
            "AdvAssaultTarget",
            "Hit",
            "Simple Target",
            "Pmc",
            "AssaultHaveEnemy",
            "Assault Building",
            "Enemy Building",
            "PushAndSup",
            "Pursuit",
        };

        public static void AddSAINLayers()
        {
            // BigBrainHandler.BrainAssignment API changed in SPT 4.1.2 — method is not called, stub left for compatibility
            Plugin.LogSource.LogWarning("AddSAINLayers: BigBrainHandler.BrainAssignment API changed, custom SAIN layers not applied.");
        }

        public static void CreateCustomBotTypes()
        {
            Plugin.LogSource.LogInfo("Creating custom bot types for SAIN...");

            var preset = SAINPresetClass.Instance;
            var botSettings = preset.BotSettings;

            foreach (var setting in CustomWildSpawnTypeManager.GetSAINSettings())
            {
                var botType = new BotType()
                {
                    Name = setting.Name,
                    Description = setting.Description,
                    Section = setting.Section,
                    WildSpawnType = (WildSpawnType)setting.WildSpawnType,
                    BaseBrain = setting.BaseBrain
                };
                
                // BotTypeDefinitions.AddBotType removed in new SAIN — add to dictionary directly
                if (!BotTypeDefinitions.BotTypes.ContainsKey(botType.WildSpawnType))
                    BotTypeDefinitions.BotTypes.Add(botType.WildSpawnType, botType);
                

                Plugin.LogSource.LogInfo($"Added SAIN BotType: {botType.Name} with WildSpawnType {botType.WildSpawnType}");
            }
        }
    }
}
