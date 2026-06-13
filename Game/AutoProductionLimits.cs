using System;
using System.Collections.Generic;
using Eremite.Model;
using StockAlert.Config;
using StockAlert.Infrastructure.Plugin;
using UnityEngine;

namespace StockAlert.Game
{
    internal static class AutoProductionLimits
    {
        private static readonly Dictionary<string, HashSet<string>> ConsumingRacesByGood =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            BuildConsumerMap();
            _initialized = true;
        }

        public static void ApplyCurrentTargets()
        {
            if ((!ConfigManager.AutoAdjustProductionLimits &&
                 !ConfigManager.AutoAdjustPurgingFire &&
                 !ConfigManager.AutoStopProductionWhenRainwaterEmpty) ||
                !GameAPI.IsGameActive())
            {
                return;
            }

            Initialize();

            var availableRecipeGoods = GameAPI.GetAvailableRecipeGoods();
            if (availableRecipeGoods.Count == 0)
            {
                return;
            }

            var changed = false;

            if (ConfigManager.AutoAdjustProductionLimits)
            {
                var multiplier = ConfigManager.AutoAdjustMultiplier;

                foreach (var entry in ConsumingRacesByGood)
                {
                    if (!availableRecipeGoods.Contains(entry.Key))
                    {
                        continue;
                    }

                    var consumerCount = 0;
                    foreach (var raceId in entry.Value)
                    {
                        consumerCount += GameAPI.GetAliveVillagerCount(raceId);
                    }

                    var targetLimit = Mathf.Max(0, Mathf.CeilToInt(consumerCount * multiplier));
                    var currentLimit = GameAPI.GetProductionLimit(null, entry.Key);
                    if (currentLimit == targetLimit)
                    {
                        continue;
                    }

                    GameAPI.SetProductionLimit(entry.Key, targetLimit);
                    Plugin.Log($"Auto-adjusted production limit for {entry.Key} => {targetLimit} ({consumerCount} consumers x {multiplier:0.0})");
                    changed = true;
                }
            }

            if (ConfigManager.AutoAdjustPurgingFire)
            {
                changed |= ApplyBlightFuelTarget(availableRecipeGoods);
            }

            if (ConfigManager.AutoStopProductionWhenRainwaterEmpty)
            {
                changed |= RainwaterProductionStopper.Apply();
            }

            if (changed)
            {
                GameAPI.RefreshWorkshopLimits();
            }
        }

        private static void BuildConsumerMap()
        {
            ConsumingRacesByGood.Clear();

            var settings = GameAPI.GetSettings();
            if (settings?.Races == null)
            {
                return;
            }

            foreach (var race in settings.Races)
            {
                if (race?.needs == null || string.IsNullOrWhiteSpace(race.Name))
                {
                    continue;
                }

                foreach (var need in race.needs)
                {
                    var good = need?.referenceGood;
                    if (good == null || string.IsNullOrWhiteSpace(good.Name))
                    {
                        continue;
                    }

                    if (!ConsumingRacesByGood.TryGetValue(good.Name, out var consumers))
                    {
                        consumers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        ConsumingRacesByGood[good.Name] = consumers;
                    }

                    consumers.Add(race.Name);
                }
            }

            Plugin.Log($"AutoProductionLimits: mapped {ConsumingRacesByGood.Count} consumable goods");
        }

        private static bool ApplyBlightFuelTarget(HashSet<string> availableRecipeGoods)
        {
            var blightFuelId = GameAPI.GetBlightPostFuelId();
            if (string.IsNullOrWhiteSpace(blightFuelId) || !availableRecipeGoods.Contains(blightFuelId))
            {
                return false;
            }

            var targetLimit = Mathf.Max(0, GameAPI.GetGlobalActiveCysts() + 1);
            var currentLimit = GameAPI.GetProductionLimit(null, blightFuelId);
            if (currentLimit == targetLimit)
            {
                return false;
            }

            GameAPI.SetProductionLimit(blightFuelId, targetLimit);
            Plugin.Log($"Auto-adjusted production limit for {blightFuelId} => {targetLimit} ({targetLimit - 1} cysts + 1)");
            return true;
        }
    }
}
