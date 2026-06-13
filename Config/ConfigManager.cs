using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using StockAlert.Core.Models;
using StockAlert.Infrastructure.Plugin;
using UnityEngine;

namespace StockAlert.Config
{
    public static class ConfigManager
    {
        private static ConfigFile _config;
        private static ConfigEntry<KeyboardShortcut> _toggleSettingsKey;
        private static ConfigEntry<bool> _showHud;
        private static ConfigEntry<bool> _movableHud;
        private static ConfigEntry<bool> _showBuildingAlertIndicators;
        private static ConfigEntry<bool> _showBuildingSpecificItemIndicators;
        private static ConfigEntry<float> _buildingShortageIconScale;
        private static ConfigEntry<bool> _showBuilderStatusIcons;
        private static ConfigEntry<bool> _showIdleBuilderStatusIcons;
        private static ConfigEntry<bool> _showBusyBuilderStatusIcons;
        private static ConfigEntry<bool> _showIdleBuildersAlert;
        private static ConfigEntry<bool> _showBuilderDemandCounter;
        private static ConfigEntry<bool> _showWorkerHoverHighlight;
        private static ConfigEntry<bool> _filterWorkerOverlayByRace;
        private static ConfigEntry<bool> _enableHearthDecorationRequirementShortcuts;
        private static ConfigEntry<bool> _showForestEaterDirection;
        private static ConfigEntry<bool> _highQualityShadows;
        private static ConfigEntry<bool> _showZeroBuildingBlueprintHover;
        private static ConfigEntry<bool> _avoidAssigningCarryingBuilders;
        private static ConfigEntry<bool> _enableQueuedWorkerAssignments;
        private static ConfigEntry<bool> _showIngredientWheelBuildingStock;
        private static ConfigEntry<bool> _showEmbarkationCostRanges;
        private static ConfigEntry<bool> _showTradeRouteProfit;
        private static ConfigEntry<bool> _tradeRouteProfitRequireAvailableRawMaterials;
        private static ConfigEntry<bool> _tradeRouteProfitIgnoreDisabledRecipes;
        private static ConfigEntry<bool> _seasonEndingTradeRoutesAlert;
        private static ConfigEntry<bool> _timedOrdersAlert;
        private static ConfigEntry<bool> _hearthLevelDownAlert;
        private static ConfigEntry<bool> _missionObjectiveLoadAlert;
        private static ConfigEntry<bool> _autoAdjustProductionLimits;
        private static ConfigEntry<bool> _autoAdjustPurgingFire;
        private static ConfigEntry<float> _autoAdjustMultiplier;
        private static ConfigEntry<bool> _autoPauseOnNoRainwater;
        private static ConfigEntry<bool> _autoResumeOnRainwater;
        private static ConfigEntry<HudHorizontalAnchor> _hudAnchor;
        private static ConfigEntry<float> _hudPositionX;
        private static ConfigEntry<float> _hudPositionY;

        public enum HudHorizontalAnchor
        {
            Left,
            Right
        }

        public static void Load()
        {
            if (_config != null)
            {
                return;
            }

            _config = new ConfigFile(Paths.ConfigPath + "\\StockAlert.cfg", true);
            _toggleSettingsKey = _config.Bind(
                "General",
                "ToggleSettingsKey",
                new KeyboardShortcut(KeyCode.F8),
                "Key used to show or hide the Stock Alert settings window."
            );
            _showHud = _config.Bind(
                "HUD",
                "ShowHUD",
                true,
                "Show or hide the Stock Alert HUD."
            );
            _movableHud = _config.Bind(
                "HUD",
                "MovableHUD",
                false,
                "Allow dragging the Stock Alert HUD."
            );
            _showBuildingAlertIndicators = _config.Bind(
                "HUD",
                "ShowBuildingAlertIndicators",
                true,
                "Show red and yellow worker markers over recipe buildings related to current shortages."
            );
            _showBuildingSpecificItemIndicators = _config.Bind(
                "HUD",
                "ShowBuildingSpecificItemIndicators",
                true,
                "Show shortage product icons next to building shortage indicators."
            );
            _buildingShortageIconScale = _config.Bind(
                "HUD",
                "BuildingShortageIconScale",
                0.9f,
                new ConfigDescription(
                    "Scale of shortage product icons next to building shortage indicators. Range: 0.2 to 2.0.",
                    new AcceptableValueRange<float>(0.2f, 2f)
                )
            );
            _showBuilderStatusIcons = _config.Bind(
                "HUD",
                "ShowBuilderStatusIcons",
                false,
                "Show idle and working icons over free builder villagers."
            );
            _showIdleBuilderStatusIcons = _config.Bind(
                "HUD",
                "ShowIdleBuilderStatusIcons",
                true,
                "Show the idle-builder icon over idle free builder villagers when builder status icons are enabled."
            );
            _showBusyBuilderStatusIcons = _config.Bind(
                "HUD",
                "ShowBusyBuilderStatusIcons",
                true,
                "Show the busy-builder icon over working free builder villagers when builder status icons are enabled."
            );
            _showIdleBuildersAlert = _config.Bind(
                "HUD",
                "ShowIdleBuildersAlert",
                false,
                "Show a persistent alert while you have idle builders."
            );
            _showBuilderDemandCounter = _config.Bind(
                "HUD",
                "ShowBuilderDemandCounter",
                true,
                "Show the top-left vanilla builder counter as available builders over needed builders."
            );
            _showWorkerHoverHighlight = _config.Bind(
                "HUD",
                "ShowWorkerHoverHighlight",
                true,
                "Show a world highlight ring when hovering assigned worker slots or worker selection race portraits."
            );
            _filterWorkerOverlayByRace = _config.Bind(
                "HUD",
                "FilterWorkerOverlayByRace",
                true,
                "When holding Shift during the vanilla Alt worker overlay, show only workplaces occupied by the currently selected race."
            );
            _enableHearthDecorationRequirementShortcuts = _config.Bind(
                "HUD",
                "EnableHearthDecorationRequirementShortcuts",
                true,
                "Click Hearth decoration upgrade requirements to select a matching decoration for construction."
            );
            _showForestEaterDirection = _config.Bind(
                "HUD",
                "ShowForestEaterDirection",
                true,
                "Show the Grazer's currently chosen path and next direction while the Forest Eater event is active."
            );
            _highQualityShadows = _config.Bind(
                "Graphics",
                "HighQualityShadows",
                false,
                "Use higher-resolution shadow maps, more cascades, and soft shadows to reduce blocky shadows. May reduce performance."
            );
            _showZeroBuildingBlueprintHover = _config.Bind(
                "HUD",
                "ShowZeroBuildingBlueprintHover",
                true,
                "Show unlocked buildings with zero placed when hovering the bottom blightrot icon."
            );
            _avoidAssigningCarryingBuilders = _config.Bind(
                "Workers",
                "AvoidAssigningCarryingBuilders",
                true,
                "Prefer free builders who are not carrying goods when assigning a villager from the builders pool."
            );
            _enableQueuedWorkerAssignments = _config.Bind(
                "HUD",
                "EnableQueuedWorkerAssignments",
                false,
                "Allow queuing a race for a worker slot so it auto-fills when a matching free villager becomes available."
            );
            _showIngredientWheelBuildingStock = _config.Bind(
                "HUD",
                "ShowIngredientWheelBuildingStock",
                false,
                "Show how much of each ingredient is currently sitting in non-warehouse building storage in the ingredient selection wheel."
            );
            _showEmbarkationCostRanges = _config.Bind(
                "HUD",
                "ShowEmbarkationCostRanges",
                false,
                "Show embarkation bonus costs as current cost over maximum possible cost."
            );
            _showTradeRouteProfit = _config.Bind(
                "HUD",
                "ShowTradeRouteProfit",
                false,
                "Show estimated profit on trade route offers."
            );
            _tradeRouteProfitRequireAvailableRawMaterials = _config.Bind(
                "HUD",
                "TradeRouteProfitRequireAvailableRawMaterials",
                false,
                "When calculating trade route profit, only use production recipe chains whose raw inputs are currently available for at least one production cycle."
            );
            _tradeRouteProfitIgnoreDisabledRecipes = _config.Bind(
                "HUD",
                "TradeRouteProfitIgnoreDisabledRecipes",
                false,
                "When calculating trade route profit, ignore recipes that are disabled in placed production buildings and recipes from unlocked buildings that are not placed."
            );
            _seasonEndingTradeRoutesAlert = _config.Bind(
                "HUD",
                "SeasonEndingTradeRoutesAlert",
                false,
                "Pause 3 seconds before season end and show a reminder to check trade routes."
            );
            _timedOrdersAlert = _config.Bind(
                "HUD",
                "TimedOrdersAlert",
                true,
                "Show alerts when timed orders have 1 minute and 30 seconds remaining."
            );
            _hearthLevelDownAlert = _config.Bind(
                "HUD",
                "HearthLevelDownAlert",
                true,
                "Show an alert when a Hearth loses an upgrade level."
            );
            _missionObjectiveLoadAlert = _config.Bind(
                "HUD",
                "MissionObjectiveLoadAlert",
                true,
                "Show an alert when a settlement save is loaded to remind you to check the mission objective."
            );
            _autoAdjustProductionLimits = _config.Bind(
                "Automation",
                "AutoAdjustProductionLimits",
                false,
                "Automatically adjust global production limits for consumer goods based on the number of villagers that can consume each good."
            );
            _autoAdjustPurgingFire = _config.Bind(
                "Automation",
                "AutoAdjustPurgingFire",
                false,
                "Automatically set Purging Fire in blight posts to active cysts plus one."
            );
            _autoAdjustMultiplier = _config.Bind(
                "Automation",
                "ProductionLimitMultiplier",
                2f,
                new ConfigDescription(
                    "Multiplier applied per consuming villager when auto-adjusting production limits. Range: 1.0 to 9.0.",
                    new AcceptableValueRange<float>(1f, 9f)
                )
            );
            _autoPauseOnNoRainwater = _config.Bind(
                "Automation",
                "AutoPauseOnNoRainwater",
                false,
                "Automatically pause buildings with running Rain Engines when the rainwater type they use is fully depleted."
            );
            _autoResumeOnRainwater = _config.Bind(
                "Automation",
                "AutoResumeOnRainwater",
                true,
                "Automatically resume buildings paused by the rainwater auto-pause once their rainwater type is available again. Buildings manually resumed by the player are left alone."
            );
            _hudAnchor = _config.Bind(
                "HUD",
                "HudAnchor",
                HudHorizontalAnchor.Right,
                "Horizontal anchor for the Stock Alert HUD. The HUD always stays anchored to the bottom."
            );
            _hudPositionX = _config.Bind(
                "HUD",
                "PositionX",
                -1f,
                "Saved X position for the Stock Alert HUD. Negative uses the configured bottom anchor."
            );
            _hudPositionY = _config.Bind(
                "HUD",
                "PositionY",
                -1f,
                "Saved Y position for the Stock Alert HUD. Negative uses the configured bottom anchor."
            );
        }

        public static KeyboardShortcut ToggleSettingsKey
        {
            get
            {
                Load();
                return _toggleSettingsKey.Value;
            }
        }

        public static bool ShowHud
        {
            get
            {
                Load();
                return _showHud.Value;
            }
            set
            {
                Load();
                _showHud.Value = value;
                _config.Save();
            }
        }

        public static bool MovableHud
        {
            get
            {
                Load();
                return _movableHud.Value;
            }
            set
            {
                Load();
                _movableHud.Value = value;
                _config.Save();
            }
        }

        public static Vector2 HudPosition
        {
            get
            {
                Load();
                return new Vector2(_hudPositionX.Value, _hudPositionY.Value);
            }
            set
            {
                Load();
                _hudPositionX.Value = value.x;
                _hudPositionY.Value = value.y;
                _config.Save();
            }
        }

        public static HudHorizontalAnchor HudAnchor
        {
            get
            {
                Load();
                return _hudAnchor.Value;
            }
            set
            {
                Load();
                _hudAnchor.Value = value;
                _config.Save();
            }
        }

        public static bool ShowBuildingAlertIndicators
        {
            get
            {
                Load();
                return _showBuildingAlertIndicators.Value;
            }
            set
            {
                Load();
                _showBuildingAlertIndicators.Value = value;
                _config.Save();
            }
        }

        public static bool ShowBuildingSpecificItemIndicators
        {
            get
            {
                Load();
                return _showBuildingSpecificItemIndicators.Value;
            }
            set
            {
                Load();
                _showBuildingSpecificItemIndicators.Value = value;
                _config.Save();
            }
        }

        public static float BuildingShortageIconScale
        {
            get
            {
                Load();
                return NormalizeBuildingShortageIconScale(_buildingShortageIconScale.Value);
            }
            set
            {
                Load();
                _buildingShortageIconScale.Value = NormalizeBuildingShortageIconScale(value);
                _config.Save();
            }
        }

        public static bool ShowBuilderStatusIcons
        {
            get
            {
                Load();
                return _showBuilderStatusIcons.Value;
            }
            set
            {
                Load();
                _showBuilderStatusIcons.Value = value;
                _config.Save();
            }
        }

        public static bool ShowIdleBuilderStatusIcons
        {
            get
            {
                Load();
                return _showIdleBuilderStatusIcons.Value;
            }
            set
            {
                Load();
                _showIdleBuilderStatusIcons.Value = value;
                _config.Save();
            }
        }

        public static bool ShowBusyBuilderStatusIcons
        {
            get
            {
                Load();
                return _showBusyBuilderStatusIcons.Value;
            }
            set
            {
                Load();
                _showBusyBuilderStatusIcons.Value = value;
                _config.Save();
            }
        }

        public static bool ShowIdleBuildersAlert
        {
            get
            {
                Load();
                return _showIdleBuildersAlert.Value;
            }
            set
            {
                Load();
                _showIdleBuildersAlert.Value = value;
                _config.Save();
            }
        }

        public static bool ShowBuilderDemandCounter
        {
            get
            {
                Load();
                return _showBuilderDemandCounter.Value;
            }
            set
            {
                Load();
                _showBuilderDemandCounter.Value = value;
                _config.Save();
            }
        }

        public static bool AvoidAssigningCarryingBuilders
        {
            get
            {
                Load();
                return _avoidAssigningCarryingBuilders.Value;
            }
            set
            {
                Load();
                _avoidAssigningCarryingBuilders.Value = value;
                _config.Save();
            }
        }

        public static bool ShowWorkerHoverHighlight
        {
            get
            {
                Load();
                return _showWorkerHoverHighlight.Value;
            }
            set
            {
                Load();
                _showWorkerHoverHighlight.Value = value;
                _config.Save();
            }
        }

        public static bool FilterWorkerOverlayByRace
        {
            get
            {
                Load();
                return _filterWorkerOverlayByRace.Value;
            }
            set
            {
                Load();
                _filterWorkerOverlayByRace.Value = value;
                _config.Save();
            }
        }

        public static bool EnableHearthDecorationRequirementShortcuts
        {
            get
            {
                Load();
                return _enableHearthDecorationRequirementShortcuts.Value;
            }
            set
            {
                Load();
                _enableHearthDecorationRequirementShortcuts.Value = value;
                _config.Save();
            }
        }

        public static bool ShowForestEaterDirection
        {
            get
            {
                Load();
                return _showForestEaterDirection.Value;
            }
            set
            {
                Load();
                _showForestEaterDirection.Value = value;
                _config.Save();
            }
        }

        public static bool HighQualityShadows
        {
            get
            {
                Load();
                return _highQualityShadows.Value;
            }
            set
            {
                Load();
                _highQualityShadows.Value = value;
                _config.Save();
            }
        }

        public static bool ShowZeroBuildingBlueprintHover
        {
            get
            {
                Load();
                return _showZeroBuildingBlueprintHover.Value;
            }
            set
            {
                Load();
                _showZeroBuildingBlueprintHover.Value = value;
                _config.Save();
            }
        }

        public static bool AutoAdjustProductionLimits
        {
            get
            {
                Load();
                return _autoAdjustProductionLimits.Value;
            }
            set
            {
                Load();
                _autoAdjustProductionLimits.Value = value;
                _config.Save();
            }
        }

        public static bool EnableQueuedWorkerAssignments
        {
            get
            {
                Load();
                return _enableQueuedWorkerAssignments.Value;
            }
            set
            {
                Load();
                _enableQueuedWorkerAssignments.Value = value;
                _config.Save();
            }
        }

        public static bool SeasonEndingTradeRoutesAlert
        {
            get
            {
                Load();
                return _seasonEndingTradeRoutesAlert.Value;
            }
            set
            {
                Load();
                _seasonEndingTradeRoutesAlert.Value = value;
                _config.Save();
            }
        }

        public static bool TimedOrdersAlert
        {
            get
            {
                Load();
                return _timedOrdersAlert.Value;
            }
            set
            {
                Load();
                _timedOrdersAlert.Value = value;
                _config.Save();
            }
        }

        public static bool HearthLevelDownAlert
        {
            get
            {
                Load();
                return _hearthLevelDownAlert.Value;
            }
            set
            {
                Load();
                _hearthLevelDownAlert.Value = value;
                _config.Save();
            }
        }

        public static bool MissionObjectiveLoadAlert
        {
            get
            {
                Load();
                return _missionObjectiveLoadAlert.Value;
            }
            set
            {
                Load();
                _missionObjectiveLoadAlert.Value = value;
                _config.Save();
            }
        }

        public static bool ShowIngredientWheelBuildingStock
        {
            get
            {
                Load();
                return _showIngredientWheelBuildingStock.Value;
            }
            set
            {
                Load();
                _showIngredientWheelBuildingStock.Value = value;
                _config.Save();
            }
        }

        public static bool ShowEmbarkationCostRanges
        {
            get
            {
                Load();
                return _showEmbarkationCostRanges.Value;
            }
            set
            {
                Load();
                _showEmbarkationCostRanges.Value = value;
                _config.Save();
            }
        }

        public static bool ShowTradeRouteProfit
        {
            get
            {
                Load();
                return _showTradeRouteProfit.Value;
            }
            set
            {
                Load();
                _showTradeRouteProfit.Value = value;
                _config.Save();
            }
        }

        public static bool TradeRouteProfitRequireAvailableRawMaterials
        {
            get
            {
                Load();
                return _tradeRouteProfitRequireAvailableRawMaterials.Value;
            }
            set
            {
                Load();
                _tradeRouteProfitRequireAvailableRawMaterials.Value = value;
                _config.Save();
            }
        }

        public static bool TradeRouteProfitIgnoreDisabledRecipes
        {
            get
            {
                Load();
                return _tradeRouteProfitIgnoreDisabledRecipes.Value;
            }
            set
            {
                Load();
                _tradeRouteProfitIgnoreDisabledRecipes.Value = value;
                _config.Save();
            }
        }

        public static float AutoAdjustMultiplier
        {
            get
            {
                Load();
                return NormalizeMultiplier(_autoAdjustMultiplier.Value);
            }
            set
            {
                Load();
                _autoAdjustMultiplier.Value = NormalizeMultiplier(value);
                _config.Save();
            }
        }

        public static bool AutoAdjustPurgingFire
        {
            get
            {
                Load();
                return _autoAdjustPurgingFire.Value;
            }
            set
            {
                Load();
                _autoAdjustPurgingFire.Value = value;
                _config.Save();
            }
        }

        public static bool AutoPauseOnNoRainwater
        {
            get
            {
                Load();
                return _autoPauseOnNoRainwater.Value;
            }
            set
            {
                Load();
                _autoPauseOnNoRainwater.Value = value;
                _config.Save();
            }
        }

        public static bool AutoResumeOnRainwater
        {
            get
            {
                Load();
                return _autoResumeOnRainwater.Value;
            }
            set
            {
                Load();
                _autoResumeOnRainwater.Value = value;
                _config.Save();
            }
        }

        public static void EnsureGoodConfig(GoodInfo g)
        {
            Load();
            ApplyProductionLimit(g);
        }

        public static bool RefreshGoodsFromProductionLimits(IEnumerable<GoodInfo> goods)
        {
            var changed = false;
            foreach (var good in goods)
            {
                changed |= ApplyProductionLimit(good);
            }

            return changed;
        }

        public static bool RefreshGoodFromProductionLimit(GoodInfo good)
        {
            return ApplyProductionLimit(good);
        }

        private static bool ApplyProductionLimit(GoodInfo g)
        {
            var previous = g.Threshold;
            var threshold = Game.GameAPI.GetProductionLimit(g.Model, g.Id);
            g.Threshold = Mathf.Max(0, threshold);
            g.Enabled = g.Threshold > 0;

            if (previous != g.Threshold)
            {
                Plugin.Log($"Synced threshold for {g.Id} => {g.Threshold}");
                return true;
            }

            return false;
        }

        private static float NormalizeMultiplier(float value)
        {
            return Mathf.Clamp(Mathf.Round(value * 10f) / 10f, 1f, 9f);
        }

        private static float NormalizeBuildingShortageIconScale(float value)
        {
            return Mathf.Clamp(Mathf.Round(value * 100f) / 100f, 0.2f, 2f);
        }
    }
}
