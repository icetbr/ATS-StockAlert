using System;
using System.Collections.Generic;
using System.Reflection;
using Eremite;
using Eremite.Buildings;
using Eremite.Buildings.UI;
using HarmonyLib;
using StockAlert.Config;
using UnityEngine.EventSystems;

namespace StockAlert.Infrastructure.Hooks
{
    [HarmonyPatch(typeof(HearthHubPanel), "SetUpRequirmentsSlots")]
    internal static class HearthDecorationRequirementShortcutPatch
    {
        private static readonly FieldInfo RequirementsField = AccessTools.Field(typeof(HearthHubPanel), "requirments");

        private static void Postfix(HearthHubPanel __instance, HubTier tier)
        {
            if (__instance == null || tier == null)
            {
                return;
            }

            if (!(RequirementsField?.GetValue(__instance) is List<HubRequirementSlot> slots))
            {
                return;
            }

            ClearSlots(slots);
            if (!ConfigManager.EnableHearthDecorationRequirementShortcuts)
            {
                return;
            }

            var slotIndex = 0;
            if (tier.minPopulation > 0)
            {
                slotIndex++;
            }

            if (tier.minInstitutions > 0)
            {
                slotIndex++;
            }

            foreach (var requirement in tier.decorations)
            {
                if (requirement?.tier == null || requirement.amount <= 0)
                {
                    continue;
                }

                if (slotIndex >= slots.Count)
                {
                    break;
                }

                var targetName = GetTargetDecorationName(requirement.tier);
                if (!string.IsNullOrEmpty(targetName))
                {
                    HearthDecorationRequirementShortcut.Attach(slots[slotIndex], targetName);
                }

                slotIndex++;
            }
        }

        private static void ClearSlots(IEnumerable<HubRequirementSlot> slots)
        {
            foreach (var slot in slots)
            {
                var shortcut = slot != null ? slot.GetComponent<HearthDecorationRequirementShortcut>() : null;
                if (shortcut != null)
                {
                    shortcut.TargetDecorationName = null;
                }
            }
        }

        private static string GetTargetDecorationName(DecorationTier tier)
        {
            var tierName = tier.Name ?? string.Empty;
            var displayName = tier.displayName?.Text ?? string.Empty;

            if (Matches(tierName, displayName, "Comfort"))
            {
                return "Park";
            }

            if (Matches(tierName, displayName, "Aesthetics"))
            {
                return "Garden";
            }

            if (Matches(tierName, displayName, "Harmony"))
            {
                return "Marble Fountain";
            }

            return null;
        }

        private static bool Matches(string name, string displayName, string expected)
        {
            return string.Equals(name, expected, StringComparison.OrdinalIgnoreCase)
                || string.Equals(displayName, expected, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class HearthDecorationRequirementShortcut : GameMB, IPointerClickHandler
    {
        public string TargetDecorationName;

        public static void Attach(HubRequirementSlot slot, string targetDecorationName)
        {
            if (slot == null || string.IsNullOrEmpty(targetDecorationName))
            {
                return;
            }

            var shortcut = slot.GetComponent<HearthDecorationRequirementShortcut>();
            if (shortcut == null)
            {
                shortcut = slot.gameObject.AddComponent<HearthDecorationRequirementShortcut>();
            }

            shortcut.TargetDecorationName = targetDecorationName;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null
                || eventData.button != PointerEventData.InputButton.Left
                || string.IsNullOrEmpty(TargetDecorationName)
                || !ConfigManager.EnableHearthDecorationRequirementShortcuts)
            {
                return;
            }

            var model = FindDecoration(TargetDecorationName);
            if (model == null
                || !GameMB.GameContentService.IsUnlocked(model)
                || !GameMB.ConstructionService.CanConstruct(model))
            {
                PlayFailedClickSound();
                eventData.Use();
                return;
            }

            PlayRegularClickSound();
            GameMB.GameBlackboardService.BuildingConstructionRequested.OnNext(model);
            eventData.Use();
        }

        private static BuildingModel FindDecoration(string targetDecorationName)
        {
            foreach (var building in MB.Settings.Buildings)
            {
                if (!(building is DecorationModel decoration) || !decoration.hasDecorationTier)
                {
                    continue;
                }

                if (string.Equals(building.displayName?.Text, targetDecorationName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(building.Name, targetDecorationName, StringComparison.OrdinalIgnoreCase))
                {
                    return building;
                }
            }

            return null;
        }
    }
}
