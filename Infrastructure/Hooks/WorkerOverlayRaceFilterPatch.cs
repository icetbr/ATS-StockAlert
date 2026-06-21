using System.Collections.Generic;
using System.Reflection;
using Eremite;
using Eremite.Buildings;
using Eremite.Buildings.UI;
using Eremite.Characters;
using Eremite.Characters.Villagers;
using Eremite.Model;
using Eremite.Services;
using Eremite.View;
using HarmonyLib;
using StockAlert.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StockAlert.Infrastructure.Hooks
{
    [HarmonyPatch(typeof(BuildingWorkersOverlay), "SetUp")]
    internal static class WorkerOverlayRaceFilterPatch
    {
        private static readonly FieldInfo SlotsField = AccessTools.Field(typeof(BuildingWorkersOverlay), "slots");
        private static readonly FieldInfo LayoutField = AccessTools.Field(typeof(BuildingWorkersOverlay), "layout");
        private static readonly FieldInfo ResizerField = AccessTools.Field(typeof(BuildingWorkersOverlay), "resizer");
        private static readonly FieldInfo ActorField = AccessTools.Field(typeof(BuildingWorkerSlot), "actor");
        private static readonly PropertyInfo GameBlackboardServiceProperty = AccessTools.Property(typeof(GameMB), "GameBlackboardService");
        private static readonly PropertyInfo ActorsServiceProperty = AccessTools.Property(typeof(GameMB), "ActorsService");
        private static PropertyInfo _pickedRaceProperty;
        private static PropertyInfo _pickedRaceValueProperty;
        private static int _cachedFilterFrame = -1;
        private static bool _cachedShouldFilter;
        private static RaceModel _cachedPickedRace;

        private static bool Prefix(BuildingWorkersOverlay __instance, ProductionBuilding building)
        {
            if (__instance == null || !ShouldFilter(out var pickedRace))
            {
                return true;
            }

            if (HasMatchingWorker(building, pickedRace))
            {
                return true;
            }

            __instance.Hide();
            return false;
        }

        private static void Postfix(BuildingWorkersOverlay __instance)
        {
            if (__instance == null || !ShouldFilter(out var pickedRace))
            {
                return;
            }

            if (!(SlotsField?.GetValue(__instance) is List<BuildingWorkerSlot> slots))
            {
                return;
            }

            var visibleCount = 0;
            foreach (var slot in slots)
            {
                if (slot == null || !slot.ActiveSelf)
                {
                    continue;
                }

                if (IsMatchingRace(slot, pickedRace))
                {
                    visibleCount++;
                    continue;
                }

                slot.Hide();
            }

            if (visibleCount == 0)
            {
                __instance.Hide();
                return;
            }

            (LayoutField?.GetValue(__instance) as HorizontalLayout)?.Rebuild(slots);
            (ResizerField?.GetValue(__instance) as SimpleHorizontalResizer)?.Resize(visibleCount);
        }

        private static bool ShouldFilter(out RaceModel pickedRace)
        {
            if (_cachedFilterFrame != Time.frameCount)
            {
                _cachedPickedRace = null;
                _cachedShouldFilter = ConfigManager.FilterWorkerOverlayByRace && IsShiftPressed();
                if (_cachedShouldFilter)
                {
                    _cachedPickedRace = GetPickedRace();
                    _cachedShouldFilter = _cachedPickedRace != null;
                }

                _cachedFilterFrame = Time.frameCount;
            }

            pickedRace = _cachedPickedRace;
            return _cachedShouldFilter;
        }

        private static RaceModel GetPickedRace()
        {
            var blackboardService = GameBlackboardServiceProperty?.GetValue(null, null);
            if (blackboardService == null)
            {
                return null;
            }

            _pickedRaceProperty ??= blackboardService.GetType().GetProperty("PickedRace", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var pickedRace = _pickedRaceProperty?.GetValue(blackboardService, null);
            if (pickedRace == null)
            {
                return null;
            }

            _pickedRaceValueProperty ??= pickedRace.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return _pickedRaceValueProperty?.GetValue(pickedRace, null) as RaceModel;
        }

        private static bool HasMatchingWorker(ProductionBuilding building, RaceModel pickedRace)
        {
            if (building?.Workers == null || pickedRace == null)
            {
                return false;
            }

            var actorsService = ActorsServiceProperty?.GetValue(null, null) as IActorsService;
            if (actorsService == null)
            {
                return false;
            }

            foreach (var workerId in building.Workers)
            {
                if (workerId == 0 || !actorsService.HasActor(workerId))
                {
                    continue;
                }

                if (IsMatchingActor(actorsService.GetActor(workerId), pickedRace))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMatchingRace(BuildingWorkerSlot slot, RaceModel pickedRace)
        {
            return IsMatchingActor(ActorField?.GetValue(slot) as Actor, pickedRace);
        }

        private static bool IsMatchingActor(Actor actor, RaceModel pickedRace)
        {
            if (!(actor is Villager villager) || villager.raceModel == null)
            {
                return false;
            }

            return villager.raceModel == pickedRace || villager.raceModel.Name == pickedRace.Name;
        }

        private static bool IsShiftPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return keyboard.leftShiftKey.isPressed
                    || keyboard.rightShiftKey.isPressed
                    || keyboard.shiftKey.isPressed;
            }

            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
    }
}
