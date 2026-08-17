using MoreBotsServer.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using System.Reflection;

namespace MoreBotsServer.Services;

[Injectable(InjectionType.Singleton)]
public class MoreBotsCustomBotConfigService(
    MoreBotsLogger logger,
    ModHelper modHelper,
    JsonUtil jsonUtil,
    BotConfig botConfig
)
{
    public bool ProcessBotConfig(BotTypeConfig botConfigData, string botTypeName)
    {
        var botTypeNameLower = botTypeName.ToLowerInvariant();

        if (botConfigData == null)
        {
            return false;
        }

        botConfig.PresetBatch[botTypeName] = botConfigData.PresetBatch ?? 1;

        if (botConfigData.IsBoss == true)
        {
            botConfig.Bosses.Add(botTypeName);
        }

        if (botConfigData.Durability != null)
        {
            botConfig.Durability.BotDurabilities[botTypeNameLower] = botConfigData.Durability;
        }

        if (botConfigData.ItemSpawnLimits != null)
        {
            botConfig.ItemSpawnLimits[botTypeNameLower] = botConfigData.ItemSpawnLimits;
        }

        if (botConfigData.EquipmentFilters != null)
        {
            botConfig.Equipment[botTypeNameLower] = botConfigData.EquipmentFilters;
        }

        if (botConfigData.CurrencyStackSize != null)
        {
            botConfig.CurrencyStackSize[botTypeNameLower] = botConfigData.CurrencyStackSize;
        }

        if (botConfigData.MustHaveUniqueName == true)
        {
            botConfig.BotRolesThatMustHaveUniqueName.Add(botTypeNameLower);
        }

        return true;
    }

    public async Task LoadCustomBotConfigs(Assembly assembly, string? relativePath = null)
    {
        try
        {
            var assemblyLocation = modHelper.GetAbsolutePathToModFolder(assembly);
            var botTypeDir = System.IO.Path.Combine("db", "bots", "config");
            var finalDir = System.IO.Path.Combine(assemblyLocation, botTypeDir);

            if (!Directory.Exists(finalDir))
            {
                logger.Error($"Directory for custom bot configs not found at {finalDir}");
                return;
            }

            var files = Directory.GetFiles(finalDir, "*.json*");

            foreach (var file in files)
            {
                var botConfigData = await jsonUtil.DeserializeFromFileAsync<BotTypeConfig>(file);
                var botTypeName = System.IO.Path.GetFileNameWithoutExtension(file);
                var botTypeNameLower = botTypeName.ToLowerInvariant();

                if (ProcessBotConfig(botConfigData, botTypeName))
                {
                    //logger.Info($"Loaded custom bot config for: {botTypeNameLower}");
                }
                else
                {
                    logger.Warning($"Could not read {file} as bot config data! Skipping.");
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error loading custom bot configs: {ex.Message}");
        }
    }

    public async Task LoadCustomBotConfigsShared(Assembly assembly, string sharedFileName, List<string> botTypeNames)
    {
        try
        {
            var assemblyLocation = modHelper.GetAbsolutePathToModFolder(assembly);
            var botTypeDir = System.IO.Path.Combine("db", "bots", "sharedConfig");
            var finalDir = System.IO.Path.Combine(assemblyLocation, botTypeDir);

            if (!Directory.Exists(finalDir))
            {
                logger.Error($"Directory for shared custom bot configs not found at {finalDir}");
                return;
            }

            var files = Directory.GetFiles(finalDir, sharedFileName + ".json*");

            if (!files.Any())
            {
                logger.Warning($"Shared bot config file {sharedFileName} not found at {finalDir}");
                return;
            }

            var file = files[0];

            var botConfigData = await jsonUtil.DeserializeFromFileAsync<BotTypeConfig>(file);

            foreach (var botTypeName in botTypeNames)
            {
                var botTypeNameLower = botTypeName.ToLowerInvariant();

                botConfigData = await jsonUtil.DeserializeFromFileAsync<BotTypeConfig>(file);

                if (ProcessBotConfig(botConfigData, botTypeName))
                {
                    //logger.Info($"Loaded custom shared bot config for: {botTypeNameLower}");
                }
                else
                {
                    logger.Warning($"Could not read {file} as shared bot config data! Skipping loading shared config.");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error loading custom shared bot config: {ex.Message}");
        }
    }
}
