using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Eremite;
using Eremite.Buildings;
using Eremite.Model;
using Eremite.Model.State;
using Eremite.View;
using Eremite.View.HUD;
using Eremite.View.HUD.TradeRoutes;
using HarmonyLib;
using StockAlert.Config;
using StockAlert.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StockAlert.Infrastructure.Hooks
{
    [HarmonyPatch]
    internal static class TradeRouteProfitPatch
    {
        private static readonly FieldInfo StateField = AccessTools.Field(typeof(TownOfferSlot), "state");
        private static readonly FieldInfo TimeTextField = AccessTools.Field(typeof(TownOfferSlot), "timeText");
        private static readonly FieldInfo ButtonField = AccessTools.Field(typeof(TownOfferSlot), "button");
        private static readonly FieldInfo ButtonTriggerField = AccessTools.Field(typeof(TownOfferSlot), "buttonTrigger");
        private static readonly FieldInfo OnlyAvailableToggleField = AccessTools.Field(typeof(TradeRoutesPopup), "onlyAvailableToggle");
        private static readonly MethodInfo SetUpOffersMethod = AccessTools.Method(typeof(TradeRoutesPopup), "SetUpOffers");
        private static readonly PropertyInfo EffectsServiceProperty = AccessTools.Property(typeof(GameMB), "EffectsService");
        private static readonly PropertyInfo TradeServiceProperty = AccessTools.Property(typeof(GameMB), "TradeService");
        private const int MaxCostDepth = 24;
        private const float ProfitModelTooltipBottomPadding = 18f;
        private const string RawMaterialsToggleName = "StockAlertRawMaterialsProfitToggle";
        private static MethodInfo _getTradeRoutesRewardsMethod;
        private static MethodInfo _getTradeRoutesFuelMethod;
        private static MethodInfo _getValueInCurrencyMethod;

        [HarmonyPatch(typeof(TradeRoutesPopup), "Show")]
        [HarmonyPostfix]
        private static void AddRawMaterialsToggleToTradeRoutesPopup(TradeRoutesPopup __instance)
        {
            if (__instance == null)
            {
                return;
            }

            try
            {
                EnsureRawMaterialsToggle(__instance);
            }
            catch (Exception)
            {
            }
        }

        [HarmonyPatch(typeof(TownOfferSlot), "SetUpTexts")]
        [HarmonyPostfix]
        private static void AddProfitToTravelTime(TownOfferSlot __instance)
        {
            if (!ConfigManager.ShowTradeRouteProfit || __instance == null)
            {
                return;
            }

            try
            {
                var state = StateField?.GetValue(__instance) as TownOfferState;
                var timeText = TimeTextField?.GetValue(__instance) as TMP_Text;
                if (state == null || timeText == null || !TryCalculateProfit(state, out var calculation))
                {
                    return;
                }

                timeText.richText = true;
                timeText.text = $"{timeText.text}\n<color={GetProfitColor(calculation.ProfitPercent)}>{FormatPercent(calculation.ProfitPercent)}</color>";
                SetUpProfitTextTooltip(__instance, timeText);
            }
            catch (Exception)
            {
            }
        }

        [HarmonyPatch(typeof(TownOfferSlot), "UpdateAcceptButton")]
        [HarmonyPostfix]
        private static void UpdateProfitTooltip(TownOfferSlot __instance)
        {
            if (__instance == null)
            {
                return;
            }

            try
            {
                var trigger = ButtonTriggerField?.GetValue(__instance) as SimpleTooltipRemoteTrigger;
                if (trigger != null)
                {
                    var button = ButtonField?.GetValue(__instance) as Button;
                    trigger.enabled = button == null || !button.interactable;
                }
            }
            catch (Exception)
            {
            }
        }

        private static void SetUpProfitTextTooltip(TownOfferSlot slot, TMP_Text timeText)
        {
            if (slot == null || timeText == null)
            {
                return;
            }

            timeText.raycastTarget = true;
            var trigger = timeText.GetComponent<SimpleTooltipRemoteTrigger>() ??
                          timeText.gameObject.AddComponent<SimpleTooltipRemoteTrigger>();
            trigger.SetUp(
                () => "Trade route profit",
                () => GetOfferTooltip(slot)
            );
            trigger.enabled = false;
            SetUpProfitModelTooltip(slot, timeText);
        }

        private static void SetUpProfitModelTooltip(TownOfferSlot slot, Component target)
        {
            if (slot == null || target == null)
            {
                return;
            }

            var trigger = target.GetComponent<ProfitModelTooltipTrigger>() ??
                          target.gameObject.AddComponent<ProfitModelTooltipTrigger>();
            trigger.SetUp(slot, GetOfferIcon(slot));
        }

        private static string GetOfferTooltip(TownOfferSlot slot)
        {
            var state = StateField?.GetValue(slot) as TownOfferState;
            var builder = new StringBuilder();
            if (!TryCalculateProfit(state, out var calculation))
            {
                builder.Append("Profit calculation unavailable.");
                return builder.ToString();
            }

            builder.Append(calculation.Tooltip);
            return builder.ToString();
        }

        private static Sprite GetOfferIcon(TownOfferSlot slot)
        {
            try
            {
                var state = StateField?.GetValue(slot) as TownOfferState;
                return state != null
                    ? GameAPI.GetSettings()?.GetGood(state.good.name)?.icon
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool TryCalculateProfit(TownOfferState state, out ProfitCalculation calculation)
        {
            calculation = null;
            var settings = GameAPI.GetSettings();
            var config = settings?.tradeRoutesConfig;
            var goodModel = state != null ? settings?.GetGood(state.good.name) : null;
            var currency = settings?.tradeCurrency;
            var fuelModel = config?.fuel;
            if (settings == null || config == null || goodModel == null || currency == null || fuelModel == null)
            {
                return false;
            }

            if (state.amount <= 0 || state.good.amount <= 0 || state.price < 0 || state.fuel < 0)
            {
                return false;
            }

            var materialAmount = state.good.amount * state.amount;
            if (materialAmount <= 0)
            {
                return false;
            }

            var reward = ApplyTradeRouteRewardEffects(new Good(currency.Name, state.price));
            var fuel = ApplyTradeRouteFuelEffects(new Good(fuelModel.Name, state.fuel));
            var availableRecipes = GameAPI.GetAvailableWorkshopRecipes();
            var requireAvailableRaw = ConfigManager.TradeRouteProfitRequireAvailableRawMaterials;
            var materialCost = ResolveCost(state.good.name, new HashSet<string>(StringComparer.OrdinalIgnoreCase), availableRecipes, 0, requireAvailableRaw);
            var fuelCost = ResolveCost(fuel.name, new HashSet<string>(StringComparer.OrdinalIgnoreCase), availableRecipes, 0, requireAvailableRaw);
            if (materialCost == null || fuelCost == null)
            {
                return false;
            }

            // Profit = Payment - (Items + Provisions costs)
            var fuelAmount = fuel.amount * state.amount;
            var materialValue = materialCost.UnitCost * materialAmount;
            var fuelValue = fuelCost.UnitCost * fuelAmount;
            var baseValue = materialCost.BaseUnitValue * materialAmount + fuelCost.BaseUnitValue * fuelAmount;
            var rewardValue = reward.amount * state.amount;
            var totalProfit = (rewardValue / 2f) - materialValue - fuelValue;
            var profitPercent = totalProfit;//baseValue > 0.001f ? totalProfit / baseValue * 100f : 0f;
            var tooltip = BuildProfitTooltip(
                state.good.name,
                materialAmount,
                materialCost,
                fuel.name,
                fuelAmount,
                fuelCost,
                rewardValue,
                totalProfit,
                profitPercent,
                baseValue,
                materialCost.HasInfiniteLoop || fuelCost.HasInfiniteLoop
            );

            calculation = new ProfitCalculation(profitPercent, materialCost.HasInfiniteLoop || fuelCost.HasInfiniteLoop, tooltip);
            return true;
        }

        private static CostResult ResolveCost(string goodName, HashSet<string> chain, List<GameAPI.AvailableWorkshopRecipe> availableRecipes, int depth, bool requireAvailableRaw)
        {
            if (string.IsNullOrWhiteSpace(goodName))
            {
                return null;
            }

            if (depth >= MaxCostDepth || chain.Contains(goodName))
            {
                return CostResult.InfiniteLoop(goodName, chain.Concat(new[] { goodName }));
            }

            chain.Add(goodName);
            var recipes = availableRecipes
                .Where(r => string.Equals(r?.Recipe?.producedGood?.Name, goodName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            CostResult best = null;
            foreach (var availableRecipe in recipes)
            {
                var recipeCost = ResolveRecipeCost(availableRecipe, chain, availableRecipes, depth + 1, requireAvailableRaw);
                if (recipeCost == null)
                {
                    continue;
                }

                if (recipeCost.HasInfiniteLoop)
                {
                    chain.Remove(goodName);
                    return recipeCost;
                }

                if (best == null || recipeCost.UnitCost < best.UnitCost)
                {
                    best = recipeCost;
                }
            }

            chain.Remove(goodName);
            return CostResult.Raw(goodName, GetTraderSellValueInAmber(goodName, 1));
        }

        private static CostResult ResolveRecipeCost(GameAPI.AvailableWorkshopRecipe availableRecipe, HashSet<string> chain, List<GameAPI.AvailableWorkshopRecipe> availableRecipes, int depth, bool requireAvailableRaw)
        {
            var recipe = availableRecipe?.Recipe;
            var outputAmount = GameAPI.GetEffectiveWorkshopRecipeOutput(availableRecipe);
            if (recipe == null || outputAmount <= 0)
            {
                return null;
            }

            var totalCost = 0f;
            var ingredients = new List<CostIngredient>();
            foreach (var set in recipe.requiredGoods ?? Array.Empty<GoodsSet>())
            {
                var selected = ResolveCheapestIngredient(set, chain, availableRecipes, depth, requireAvailableRaw);
                if (selected == null)
                {
                    if (set?.goods != null && set.goods.Length > 0)
                    {
                        return null;
                    }

                    continue;
                }

                if (selected.Cost.HasInfiniteLoop)
                {
                    return selected.Cost;
                }

                totalCost += selected.TotalCost;
                ingredients.Add(selected);
            }

            var rawRequirements = GetRawRequirementsForRecipeBatch(ingredients);
            if (requireAvailableRaw && !HasEnoughRawInputs(rawRequirements))
            {
                return null;
            }

            return CostResult.FromRecipe(recipe, totalCost / outputAmount, totalCost, outputAmount, ingredients, rawRequirements);
        }

        private static CostIngredient ResolveCheapestIngredient(GoodsSet set, HashSet<string> chain, List<GameAPI.AvailableWorkshopRecipe> availableRecipes, int depth, bool requireAvailableRaw)
        {
            CostIngredient best = null;
            foreach (var good in set?.goods ?? Array.Empty<GoodRef>())
            {
                if (good?.good == null || good.amount <= 0)
                {
                    continue;
                }

                var cost = ResolveCost(good.Name, chain, availableRecipes, depth, requireAvailableRaw);
                if (cost == null)
                {
                    continue;
                }

                if (cost.HasInfiniteLoop)
                {
                    return new CostIngredient(good.Name, good.amount, cost, 0f);
                }

                if (requireAvailableRaw && !HasEnoughRawInputs(GetRawRequirementsForIngredient(good.amount, cost)))
                {
                    continue;
                }

                var totalCost = cost.UnitCost * good.amount;
                if (best == null || totalCost < best.TotalCost)
                {
                    best = new CostIngredient(good.Name, good.amount, cost, totalCost);
                }
            }

            return best;
        }

        private static Dictionary<string, float> GetRawRequirementsForRecipeBatch(IEnumerable<CostIngredient> ingredients)
        {
            var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var ingredient in ingredients ?? Enumerable.Empty<CostIngredient>())
            {
                AddRawRequirements(result, GetRawRequirementsForIngredient(ingredient.Amount, ingredient.Cost));
            }

            return result;
        }

        private static Dictionary<string, float> GetRawRequirementsForIngredient(int amount, CostResult cost)
        {
            var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (cost == null || amount <= 0)
            {
                return result;
            }

            if (cost.Recipe == null)
            {
                result[cost.GoodName] = amount;
                return result;
            }

            var cycles = cost.OutputAmount > 0
                ? Mathf.CeilToInt((float)amount / cost.OutputAmount)
                : 1;
            foreach (var requirement in cost.BatchRawRequirements)
            {
                result[requirement.Key] = requirement.Value * cycles;
            }

            return result;
        }

        private static void AddRawRequirements(Dictionary<string, float> target, Dictionary<string, float> requirements)
        {
            if (target == null || requirements == null)
            {
                return;
            }

            foreach (var requirement in requirements)
            {
                target.TryGetValue(requirement.Key, out var current);
                target[requirement.Key] = current + requirement.Value;
            }
        }

        private static bool HasEnoughRawInputs(Dictionary<string, float> requirements)
        {
            foreach (var requirement in requirements ?? new Dictionary<string, float>())
            {
                if (GameAPI.GetStoredAmount(null, requirement.Key) + 0.001f < requirement.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildProfitTooltip(
            string materialName,
            int materialAmount,
            CostResult materialCost,
            string fuelName,
            int fuelAmount,
            CostResult fuelCost,
            float rewardValue,
            float totalProfit,
            float profitPercent,
            float baseValue,
            bool hasInfiniteLoop)
        {
            var builder = new StringBuilder();
            if (hasInfiniteLoop)
            {
                AppendNoWrapLine(builder, "Profit: Infinite loop");
                builder.AppendLine();
                builder.AppendLine("A recipe chain loop was detected while calculating production cost.");
                AppendCost(builder, materialCost, materialAmount, 0);
                AppendCost(builder, fuelCost, fuelAmount, 0);
                return builder.ToString();
            }

            var materialValue = materialCost.UnitCost * materialAmount;
            var fuelValue = fuelCost.UnitCost * fuelAmount;
            AppendNoWrapLine(builder, $"Profit: {FormatPercent(profitPercent)}");
            AppendNoWrapLine(builder, $"Net gain: {FormatProfit(totalProfit)}");
            builder.AppendLine();
            AppendNoWrapLine(builder, $"Route reward: {FormatAmber(rewardValue)}");
            AppendNoWrapLine(builder, $"Input value: {FormatAmber(baseValue)}");
            builder.AppendLine();
            AppendNoWrapLine(builder, "Sold good path:");
            AppendCost(builder, materialCost, materialAmount, 1);
            builder.AppendLine();
            AppendNoWrapLine(builder, "Provisions path:");
            AppendCost(builder, fuelCost, fuelAmount, 1);
            return builder.ToString();
        }

        private static void AppendCost(StringBuilder builder, CostResult cost, float amount, int indent)
        {
            if (cost == null)
            {
                return;
            }

            var prefix = new string(' ', indent * 3);
            if (cost.HasInfiniteLoop)
            {
                AppendNoWrapLine(builder, $"{prefix}{GetGoodDisplayName(cost.GoodName)}: Infinite loop");
                if (!string.IsNullOrWhiteSpace(cost.LoopPath))
                {
                    AppendNoWrapLine(builder, $"{prefix}{cost.LoopPath}");
                }
                return;
            }

            if (cost.Recipe == null)
            {
                AppendNoWrapLine(builder, $"{prefix}> {GetGoodDisplayName(cost.GoodName)} x{FormatAmount(amount)} = {FormatAmber(cost.UnitCost * amount)}");
                return;
            }

            AppendNoWrapLine(builder, $"{prefix}> {GetGoodDisplayName(cost.GoodName)} x{FormatAmount(amount)} = {FormatAmber(cost.UnitCost * amount)}");
            var batchScale = cost.OutputAmount > 0 ? amount / cost.OutputAmount : 1f;
            foreach (var ingredient in cost.Ingredients)
            {
                AppendCost(builder, ingredient.Cost, ingredient.Amount * batchScale, indent + 1);
            }
        }

        private static void AppendNoWrapLine(StringBuilder builder, string line)
        {
            builder.AppendLine((line ?? string.Empty).Replace(" ", "\u00A0"));
        }

        private static float GetTraderSellValueInAmber(string goodName, int amount)
        {
            if (string.IsNullOrWhiteSpace(goodName) || amount <= 0)
            {
                return 0f;
            }

            var tradeService = TradeServiceProperty?.GetValue(null, null);
            if (tradeService == null)
            {
                return 0f;
            }

            _getValueInCurrencyMethod ??= tradeService.GetType().GetMethod("GetValueInCurrency", new[] { typeof(string), typeof(int) });
            return _getValueInCurrencyMethod?.Invoke(tradeService, new object[] { goodName, amount }) is float result
                ? result
                : 0f;
        }

        private static Good ApplyTradeRouteRewardEffects(Good reward)
        {
            var effectsService = EffectsServiceProperty?.GetValue(null, null);
            if (effectsService == null)
            {
                return reward;
            }

            _getTradeRoutesRewardsMethod ??= effectsService.GetType().GetMethod("GetTradeRoutesRewards", new[] { typeof(Good) });
            return _getTradeRoutesRewardsMethod?.Invoke(effectsService, new object[] { reward }) is Good result
                ? result
                : reward;
        }

        private static Good ApplyTradeRouteFuelEffects(Good fuel)
        {
            var effectsService = EffectsServiceProperty?.GetValue(null, null);
            if (effectsService == null)
            {
                return fuel;
            }

            _getTradeRoutesFuelMethod ??= effectsService.GetType().GetMethod("GetTradeRoutesFuel", new[] { typeof(Good) });
            return _getTradeRoutesFuelMethod?.Invoke(effectsService, new object[] { fuel }) is Good result
                ? result
                : fuel;
        }

        private static string FormatProfit(float profit)
        {
            return profit > 0f ? $"+{profit:0.##}" : profit.ToString("0.##");
        }

        private static string FormatPercent(float percent)
        {
            return percent > 0f ? $"+{percent:0.##}" : $"{percent:0.##}";
        }

        private static string FormatAmber(float value)
        {
            return $"{value:0.##}";
        }

        private static string FormatAmount(float amount)
        {
            return Math.Abs(amount - Math.Round(amount)) < 0.001f
                ? $"{Math.Round(amount):0}"
                : $"{amount:0.##}";
        }

        private static string GetGoodDisplayName(string goodName)
        {
            var model = GameAPI.GetSettings()?.GetGood(goodName);
            return model?.displayName?.Text ?? goodName;
        }

        private static string GetProfitColor(float profit)
        {
            if (profit > 0f)
            {
                return "#7CFF6B";
            }

            if (profit < 0f)
            {
                return "#FF6B5E";
            }

            return "#FFD95A";
        }

        private static void EnsureRawMaterialsToggle(TradeRoutesPopup popup)
        {
            var sourceToggle = OnlyAvailableToggleField?.GetValue(popup) as ToggleButton;
            var sourceRoot = sourceToggle != null ? sourceToggle.transform.parent as RectTransform : null;
            var parent = sourceRoot != null ? sourceRoot.parent as RectTransform : null;
            if (sourceRoot == null || parent == null)
            {
                return;
            }

            var existing = parent.Find(RawMaterialsToggleName);
            var root = existing as RectTransform;
            if (root == null)
            {
                root = UnityEngine.Object.Instantiate(sourceRoot.gameObject, parent).GetComponent<RectTransform>();
                root.name = RawMaterialsToggleName;
                root.SetAsLastSibling();
            }

            root.anchorMin = new Vector2(1f, sourceRoot.anchorMin.y);
            root.anchorMax = new Vector2(1f, sourceRoot.anchorMax.y);
            root.pivot = new Vector2(1f, sourceRoot.pivot.y);
            root.anchoredPosition = new Vector2(-58f, sourceRoot.anchoredPosition.y);
            root.gameObject.SetActive(ConfigManager.ShowTradeRouteProfit);
            var toggle = root.GetComponentInChildren<ToggleButton>(true);
            if (toggle == null)
            {
                return;
            }

            toggle.enabled = false;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!string.IsNullOrWhiteSpace(text.text))
                {
                    text.text = "Available inputs";
                }
            }

            toggle.SetUp(ConfigManager.TradeRouteProfitRequireAvailableRawMaterials, _ => { });
            var button = root.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                return;
            }

            var trigger = button.GetComponent<SimpleTooltipRemoteTrigger>() ??
                          button.gameObject.AddComponent<SimpleTooltipRemoteTrigger>();
            trigger.SetUp(
                () => "Available inputs",
                () => "Changes the formula for the profit to use only recipes that can be crafted with available ingredients"
            );
            trigger.enabled = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                ConfigManager.TradeRouteProfitRequireAvailableRawMaterials = !ConfigManager.TradeRouteProfitRequireAvailableRawMaterials;
                toggle.SetUp(ConfigManager.TradeRouteProfitRequireAvailableRawMaterials, _ => { });
                SetUpOffersMethod?.Invoke(popup, null);
            });
        }

        private sealed class ProfitCalculation
        {
            public ProfitCalculation(float profitPercent, bool hasInfiniteLoop, string tooltip)
            {
                ProfitPercent = profitPercent;
                HasInfiniteLoop = hasInfiniteLoop;
                Tooltip = tooltip;
            }

            public float ProfitPercent { get; }

            public bool HasInfiniteLoop { get; }

            public string Tooltip { get; }
        }

        private sealed class CostResult
        {
            private CostResult(string goodName, float unitCost, float baseUnitValue, WorkshopRecipeModel recipe, float batchCost, float baseBatchValue, int outputAmount, List<CostIngredient> ingredients, Dictionary<string, float> batchRawRequirements, bool hasInfiniteLoop, string loopPath)
            {
                GoodName = goodName;
                UnitCost = unitCost;
                BaseUnitValue = baseUnitValue;
                Recipe = recipe;
                BatchCost = batchCost;
                BaseBatchValue = baseBatchValue;
                OutputAmount = outputAmount;
                Ingredients = ingredients ?? new List<CostIngredient>();
                BatchRawRequirements = batchRawRequirements ?? new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                HasInfiniteLoop = hasInfiniteLoop;
                LoopPath = loopPath;
            }

            public string GoodName { get; }

            public float UnitCost { get; }

            public float BaseUnitValue { get; }

            public WorkshopRecipeModel Recipe { get; }

            public float BatchCost { get; }

            public float BaseBatchValue { get; }

            public int OutputAmount { get; }

            public List<CostIngredient> Ingredients { get; }

            public Dictionary<string, float> BatchRawRequirements { get; }

            public bool HasInfiniteLoop { get; }

            public string LoopPath { get; }

            public static CostResult Raw(string goodName, float unitCost)
            {
                return new CostResult(
                    goodName,
                    unitCost,
                    unitCost,
                    null,
                    0f,
                    unitCost,
                    1,
                    null,
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) { [goodName] = 1f },
                    false,
                    null
                );
            }

            public static CostResult FromRecipe(WorkshopRecipeModel recipe, float unitCost, float batchCost, int outputAmount, List<CostIngredient> ingredients, Dictionary<string, float> batchRawRequirements)
            {
                var baseBatchValue = ingredients?.Sum(i => i.BaseTotalValue) ?? 0f;
                return new CostResult(recipe.producedGood.Name, unitCost, baseBatchValue / outputAmount, recipe, batchCost, baseBatchValue, outputAmount, ingredients, batchRawRequirements, false, null);
            }

            public static CostResult InfiniteLoop(string goodName, IEnumerable<string> path)
            {
                return new CostResult(goodName, 0f, 0f, null, 0f, 0f, 0, null, null, true, string.Join(" -> ", path.Select(GetGoodDisplayName)));
            }
        }

        private sealed class CostIngredient
        {
            public CostIngredient(string goodName, int amount, CostResult cost, float totalCost)
            {
                GoodName = goodName;
                Amount = amount;
                Cost = cost;
                TotalCost = totalCost;
            }

            public string GoodName { get; }

            public int Amount { get; }

            public CostResult Cost { get; }

            public float TotalCost { get; }

            public float BaseTotalValue => Cost.BaseUnitValue * Amount;
        }

        private sealed class ProfitModelTooltipTrigger : GameMB, IPointerEnterHandler, IPointerExitHandler
        {
            private TownOfferSlot _slot;
            private Sprite _icon;
            private bool _showing;

            public void SetUp(TownOfferSlot slot, Sprite icon)
            {
                _slot = slot;
                _icon = icon;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                ShowTooltip();
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                HideTooltip();
            }

            private void OnDisable()
            {
                HideTooltip();
            }

            private void ShowTooltip()
            {
                var tooltip = TooltipsService.Get<ModelTooltip>();
                var target = transform as RectTransform;
                if (tooltip == null || target == null || _slot == null)
                {
                    return;
                }

                _showing = true;
                tooltip.Show(target, "Trade Route Profit", GetOfferTooltip(_slot), _icon, null);
                var tightener = tooltip.GetComponent<ProfitModelTooltipTightener>() ??
                                tooltip.gameObject.AddComponent<ProfitModelTooltipTightener>();
                tightener.ApplyForNextFrames();
            }

            private void HideTooltip()
            {
                if (!_showing)
                {
                    return;
                }

                var tooltip = TooltipsService.Get<ModelTooltip>();
                var target = transform as RectTransform;
                if (tooltip != null && target != null)
                {
                    tooltip.Hide(target);
                }

                _showing = false;
            }
        }

        private sealed class ProfitModelTooltipTightener : MonoBehaviour
        {
            private int _framesLeft;
            private Vector2? _originalDescPadding;

            public void ApplyForNextFrames()
            {
                _framesLeft = 8;
                Apply();
            }

            private void LateUpdate()
            {
                if (_framesLeft <= 0)
                {
                    return;
                }

                _framesLeft--;
                Apply();
            }

            private void Apply()
            {
                var desc = GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(t => string.Equals(t.name, "Desc", StringComparison.Ordinal));
                if (desc == null)
                {
                    return;
                }

                desc.alignment = TextAlignmentOptions.Left;
                desc.textWrappingMode = TextWrappingModes.NoWrap;
                desc.overflowMode = TextOverflowModes.Overflow;

                var textSizer = desc.GetComponent<TextSizer>();
                if (textSizer != null)
                {
                    _originalDescPadding ??= textSizer.padding;
                    textSizer.padding = _originalDescPadding.Value;
                    textSizer.ForceRebuild();
                }

                var rect = GetComponent<RectTransform>();
                if (rect != null)
                {
                    var contentBottom = float.MaxValue;
                    var corners = new Vector3[4];

                    foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text))
                        {
                            continue;
                        }

                        text.GetComponent<RectTransform>().GetWorldCorners(corners);
                        var bottom = rect.InverseTransformPoint(corners[0]).y;
                        contentBottom = Math.Min(contentBottom, bottom);
                    }

                    foreach (var image in GetComponentsInChildren<Image>(true))
                    {
                        if (!image.gameObject.activeInHierarchy || image.sprite == null)
                        {
                            continue;
                        }

                        image.GetComponent<RectTransform>().GetWorldCorners(corners);
                        var bottom = rect.InverseTransformPoint(corners[0]).y;
                        contentBottom = Math.Min(contentBottom, bottom);
                    }

                    if (!float.IsInfinity(contentBottom) && contentBottom < float.MaxValue)
                    {
                        rect.SetHeight(Math.Max(-contentBottom + ProfitModelTooltipBottomPadding, 180f));
                    }
                }
            }
        }
    }
}
