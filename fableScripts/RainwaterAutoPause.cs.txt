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
    /// Automatically pauses (sleeps) buildings whose Rain Engines are switched on
    /// when the rainwater type they consume is fully depleted, and optionally
    /// resumes them once that rainwater is available again.
    ///
    /// Only buildings paused by this feature are ever resumed by it. Buildings the
    /// player pauses or resumes manually are left alone.
    /// </summary>
    internal static class RainwaterAutoPause
    {
        // Ids of buildings paused by this feature.
        private static readonly HashSet<int> PausedByMod = new HashSet<int>();

        // Avoids resume/pause flapping when only a trickle of water arrives.
        private const int MaxResumeThreshold = 10;

        public static void Clear()
        {
            PausedByMod.Clear();
        }

        public static void Process()
        {
            if (!ConfigManager.AutoPauseOnNoRainwater)
            {
                if (PausedByMod.Count > 0)
                {
                    PausedByMod.Clear();
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
            List<string> pausedNames = null;
            List<string> resumedNames = null;

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

                if (buildingState == null || !buildingState.finished || water == null || state == null || !state.rainpunkUnlocked)
                {
                    PausedByMod.Remove(workshop.Id);
                    continue;
                }

                var waterLeft = GameAPI.GetCityWaterLeft(water);
                if (waterLeft < 0)
                {
                    // Water amount could not be determined - never act on unknown data.
                    continue;
                }

                if (PausedByMod.Contains(workshop.Id))
                {
                    if (!buildingState.isSleeping)
                    {
                        // The player resumed this building manually - stop tracking it.
                        PausedByMod.Remove(workshop.Id);
                        continue;
                    }

                    if (ConfigManager.AutoResumeOnRainwater && waterLeft >= GetResumeThreshold(water))
                    {
                        if (GameAPI.WakeUpBuilding(workshop))
                        {
                            PausedByMod.Remove(workshop.Id);
                            resumedNames ??= new List<string>();
                            resumedNames.Add(GameAPI.GetBuildingDisplayName(workshop.BuildingModel));
                        }
                    }

                    continue;
                }

                if (buildingState.isSleeping || waterLeft > 0 || !HasRunningEngines(state))
                {
                    continue;
                }

                if (GameAPI.SleepBuilding(workshop))
                {
                    PausedByMod.Add(workshop.Id);
                    pausedNames ??= new List<string>();
                    pausedNames.Add(GameAPI.GetBuildingDisplayName(workshop.BuildingModel));
                }
            }

            RemoveStaleEntries(seenIds);
            AnnouncePaused(pausedNames);

            if (resumedNames != null)
            {
                Plugin.Log("Rainwater available again, resumed: " + string.Join(", ", resumedNames.ToArray()));
            }
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
            if (PausedByMod.Count == 0)
            {
                return;
            }

            PausedByMod.RemoveWhere(id => !seenIds.Contains(id));
        }

        private static void AnnouncePaused(List<string> pausedNames)
        {
            if (pausedNames == null || pausedNames.Count == 0)
            {
                return;
            }

            var names = string.Join(", ", pausedNames.Take(5).ToArray());
            if (pausedNames.Count > 5)
            {
                names += $" and {pausedNames.Count - 5} more";
            }

            GameAPI.PublishNews(
                pausedNames.Count == 1
                    ? "Out of rainwater - paused 1 building."
                    : $"Out of rainwater - paused {pausedNames.Count} buildings.",
                names
            );
            Plugin.Log("Rainwater depleted, auto-paused: " + names);
        }
    }
}
