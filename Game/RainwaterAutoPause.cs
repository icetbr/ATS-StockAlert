using System.Collections.Generic;
using System.Linq;
using Eremite.Buildings;
using Eremite.Model;
using StockAlert.Config;
using StockAlert.Infrastructure.Plugin;
using UnityEngine;

namespace StockAlert.Game
{
    /// <summary>
    /// Automatically disables enabled recipes in buildings whose Rain Engines are switched on
    /// when the rainwater type they consume is fully depleted, and optionally re-enables
    /// only those recipes once that rainwater is available again.
    ///
    /// This intentionally does not sleep/pause the building. Disabling the recipe state
    /// prevents new production cycles from being started, while allowing the currently
    /// running production cycle to finish normally.
    ///
    /// Only recipes disabled by this feature are ever re-enabled by it. Recipes the player
    /// had disabled manually are left alone.
    /// </summary>
    internal static class RainwaterAutoPause
    {
        // Building id -> recipe keys disabled by this feature.
        private static readonly Dictionary<int, HashSet<string>> DisabledRecipesByBuilding =
            new Dictionary<int, HashSet<string>>();

        // Avoids resume/disable flapping when only a trickle of water arrives.
        private const int MaxResumeThreshold = 10;

        public static void Clear()
        {
            DisabledRecipesByBuilding.Clear();
        }

        public static void Process()
        {
            if (!ConfigManager.AutoPauseOnNoRainwater)
            {
                if (DisabledRecipesByBuilding.Count > 0)
                {
                    DisabledRecipesByBuilding.Clear();
                }

                return;
            }

            if (!GameAPI.IsGameActive())
            {
                return;
            }

            var workshops = GameAPI.GetWorkshopBuildings();
            if (workshops.Count == 0)
            {
                return;
            }

            var seenIds = new HashSet<int>();
            List<string> disabledNames = null;
            List<string> enabledNames = null;

            foreach (var workshop in workshops)
            {
                if (workshop == null)
                {
                    continue;
                }

                seenIds.Add(workshop.Id);

                var buildingState = workshop.BuildingState;
                var water = workshop.model?.rainpunk?.water;
                var state = workshop.state;

                if (buildingState == null ||
                    !buildingState.finished ||
                    water == null ||
                    state == null ||
                    state.recipes == null ||
                    !state.rainpunkUnlocked)
                {
                    DisabledRecipesByBuilding.Remove(workshop.Id);
                    continue;
                }

                var waterLeft = GameAPI.GetCityWaterLeft(water);
                if (waterLeft < 0)
                {
                    // Water amount could not be determined - never act on unknown data.
                    continue;
                }

                if (DisabledRecipesByBuilding.TryGetValue(workshop.Id, out var disabledRecipeKeys))
                {
                    if (ConfigManager.AutoResumeOnRainwater && waterLeft >= GetResumeThreshold(water))
                    {
                        if (EnableRecipesDisabledByMod(workshop, disabledRecipeKeys) > 0)
                        {
                            enabledNames ??= new List<string>();
                            enabledNames.Add(GameAPI.GetBuildingDisplayName(workshop.BuildingModel));
                        }

                        DisabledRecipesByBuilding.Remove(workshop.Id);
                        continue;
                    }

                    if (waterLeft <= 0 && HasRunningEngines(state))
                    {
                        if (DisableActiveRecipes(workshop, disabledRecipeKeys) > 0)
                        {
                            disabledNames ??= new List<string>();
                            disabledNames.Add(GameAPI.GetBuildingDisplayName(workshop.BuildingModel));
                        }
                    }

                    continue;
                }

                if (buildingState.isSleeping || waterLeft > 0 || !HasRunningEngines(state))
                {
                    continue;
                }

                var newlyDisabledRecipeKeys = new HashSet<string>();
                if (DisableActiveRecipes(workshop, newlyDisabledRecipeKeys) > 0)
                {
                    DisabledRecipesByBuilding[workshop.Id] = newlyDisabledRecipeKeys;
                    disabledNames ??= new List<string>();
                    disabledNames.Add(GameAPI.GetBuildingDisplayName(workshop.BuildingModel));
                }
            }

            RemoveStaleEntries(seenIds);
            AnnounceDisabled(disabledNames);

            if (enabledNames != null)
            {
                Plugin.Log("Rainwater available again, re-enabled recipes in: " + string.Join(", ", enabledNames.ToArray()));
            }
        }

        private static int DisableActiveRecipes(Workshop workshop, HashSet<string> disabledRecipeKeys)
        {
            var recipes = workshop?.state?.recipes;
            if (recipes == null)
            {
                return 0;
            }

            var changed = 0;
            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe == null || !recipe.active)
                {
                    continue;
                }

                recipe.active = false;
                disabledRecipeKeys.Add(GetRecipeKey(recipe, i));
                changed++;
            }

            return changed;
        }

        private static int EnableRecipesDisabledByMod(Workshop workshop, HashSet<string> disabledRecipeKeys)
        {
            var recipes = workshop?.state?.recipes;
            if (recipes == null || disabledRecipeKeys == null || disabledRecipeKeys.Count == 0)
            {
                return 0;
            }

            var changed = 0;
            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe == null || !disabledRecipeKeys.Contains(GetRecipeKey(recipe, i)))
                {
                    continue;
                }

                if (!recipe.active)
                {
                    recipe.active = true;
                    changed++;
                }
            }

            return changed;
        }

        private static string GetRecipeKey(RecipeState recipe, int index)
        {
            if (!string.IsNullOrWhiteSpace(recipe.model))
            {
                return recipe.model;
            }

            // if (!string.IsNullOrWhiteSpace(recipe.productName))
            // {
            //     return "product:" + recipe.productName;
            // }

            return "index:" + index;
        }

        private static bool HasRunningEngines(WorkshopState state)
        {
            var engines = state?.engines;
            if (engines == null)
            {
                return false;
            }

            foreach (var engine in engines)
            {
                if (engine != null && engine.requestedLevel > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetResumeThreshold(WaterModel water)
        {
            var capacity = GameAPI.GetCityWaterCapacity(water);
            if (capacity <= 0)
            {
                return 1;
            }

            return Mathf.Clamp(capacity / 10, 1, MaxResumeThreshold);
        }

        private static void RemoveStaleEntries(HashSet<int> seenIds)
        {
            if (DisabledRecipesByBuilding.Count == 0)
            {
                return;
            }

            var staleIds = DisabledRecipesByBuilding.Keys.Where(id => !seenIds.Contains(id)).ToArray();
            foreach (var id in staleIds)
            {
                DisabledRecipesByBuilding.Remove(id);
            }
        }

        private static void AnnounceDisabled(List<string> disabledNames)
        {
            if (disabledNames == null || disabledNames.Count == 0)
            {
                return;
            }

            var names = string.Join(", ", disabledNames.Take(5).ToArray());
            if (disabledNames.Count > 5)
            {
                names += $" and {disabledNames.Count - 5} more";
            }

            GameAPI.PublishNews(
                disabledNames.Count == 1
                    ? "Out of rainwater - disabled recipes in 1 building."
                    : $"Out of rainwater - disabled recipes in {disabledNames.Count} buildings.",
                names
            );
            Plugin.Log("Rainwater depleted, disabled recipes in: " + names);
        }
    }
}
