using EFT;
using SAIN.Preset;
using System.Collections.Generic;

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
            // BigBrainHandler.BrainAssignment API removed in SPT 4.1.2 SAIN — stub only
            Plugin.LogSource.LogWarning("AddSAINLayers: BigBrainHandler.BrainAssignment API gone, custom SAIN layers not applied.");
        }

        public static void CreateCustomBotTypes()
        {
            Plugin.LogSource.LogInfo("Creating custom bot types for SAIN...");

            foreach (var setting in CustomWildSpawnTypeManager.GetSAINSettings())
            {
                var botType = new BotType()
                {
                    Name = setting.Name,
                    Description = setting.Description,
                    Section = setting.Section,
                    WildSpawnType = (WildSpawnType)setting.WildSpawnType,
                };

                if (!BotTypeDefinitions.BotTypes.ContainsKey(botType.WildSpawnType))
                    BotTypeDefinitions.BotTypes.Add(botType.WildSpawnType, botType);

                Plugin.LogSource.LogInfo($"Added SAIN BotType: {botType.Name} with WildSpawnType {botType.WildSpawnType}");
            }
        }
    }
}
