using System;
using System.Collections.Generic;
using System.Linq;
using Eremite;
using Eremite.Buildings;
using Eremite.Model;
using HarmonyLib;
using StockAlert.Config;
using StockAlert.Game;
using StockAlert.Game.Discovery;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StockAlert.UI.World
{
    internal static class BuildingAlertIndicators
    {
        private const int MaxProductIcons = 4;
        private const int ProductIconGridRows = 2;
        private const int ProductIconSortingOrderOffset = 100;
        private const float ProductIconForwardOffset = -0.05f;
        private const float ProductIconBorderThicknessScale = 0.08f;
        private const string ProductIconBorderRootName = "IngredientBlockedBorder";
        private const string ProductIconsRootName = "StockAlertProductIcons";

        private static readonly Color WhiteColor = Color.white;
        private static readonly Color RedColor = new Color(1f, 0.25f, 0.25f, 1f);
        private static readonly Color YellowColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color GreyColor = new Color(0.48f, 0.48f, 0.48f, 1f);
        private static readonly Color BorderRedColor = new Color(0.62f, 0.04f, 0.04f, 1f);

        private static readonly Dictionary<int, IndicatorSnapshot> LastApplied =
            new Dictionary<int, IndicatorSnapshot>();
        private static Sprite _solidSprite;

        public static void Refresh()
        {
            if (!ConfigManager.ShowBuildingAlertIndicators)
            {
                RestoreVanilla();
                return;
            }

            var settings = GameAPI.GetSettings();
            var lowGoods = new HashSet<string>(
                Discovery.Goods.Where(g => g.IsBelowThreshold).Select(g => g.Id),
                StringComparer.OrdinalIgnoreCase
            );
            var blockedIngredientGoods = new HashSet<string>(Discovery.BlockedIngredientGoods, StringComparer.OrdinalIgnoreCase);

            var seenBuildings = new HashSet<int>();
            foreach (var workshop in GetRecipeBuildings())
            {
                var building = workshop.Base;
                if (building == null)
                {
                    continue;
                }

                seenBuildings.Add(building.Id);
                ApplyIndicatorState(building, workshop, lowGoods, blockedIngredientGoods, settings);
            }

            foreach (var building in GameAPI.GetGatheringSourceBuildings())
            {
                if (building == null)
                {
                    continue;
                }

                seenBuildings.Add(building.Id);
                ApplyGatheringIndicatorState(building, blockedIngredientGoods);
            }

            foreach (var mine in GameAPI.GetMineBuildings())
            {
                if (mine == null)
                {
                    continue;
                }

                seenBuildings.Add(mine.Id);
                ApplyMineIndicatorState(mine, blockedIngredientGoods);
            }

            foreach (var farm in GameAPI.GetFarmBuildings())
            {
                if (farm == null)
                {
                    continue;
                }

                seenBuildings.Add(farm.Id);
                ApplyFarmIndicatorState(farm);
            }

            foreach (var rainCatcher in GameAPI.GetRainCatcherBuildings())
            {
                if (rainCatcher == null)
                {
                    continue;
                }

                seenBuildings.Add(rainCatcher.Id);
                ApplyRainCatcherIndicatorState(rainCatcher);
            }

            foreach (var hearth in GameAPI.GetHearthBuildings())
            {
                if (hearth == null)
                {
                    continue;
                }

                seenBuildings.Add(hearth.Id);
                ApplyHearthIndicatorState(hearth);
            }

            var removed = LastApplied.Keys.Where(id => !seenBuildings.Contains(id)).ToList();
            foreach (var id in removed)
            {
                RemoveProductIcons(id);
                LastApplied.Remove(id);
            }
        }

        public static void RefreshForView(ProductionBuildingView buildingView)
        {
            if (!ConfigManager.ShowBuildingAlertIndicators || buildingView == null)
            {
                return;
            }

            var workshop = GetRecipeBuildings().FirstOrDefault(w => ReferenceEquals(w?.Base?.BuildingView, buildingView));
            if (workshop?.Base == null)
            {
                var hearth = GameAPI.GetHearthBuildings().FirstOrDefault(b => ReferenceEquals(b?.BuildingView, buildingView));
                if (hearth != null)
                {
                    ApplyHearthIndicatorState(hearth);
                    return;
                }

                var rainCatcher = GameAPI.GetRainCatcherBuildings().FirstOrDefault(b => ReferenceEquals(b?.BuildingView, buildingView));
                if (rainCatcher != null)
                {
                    ApplyRainCatcherIndicatorState(rainCatcher);
                }

                return;
            }

            var settings = GameAPI.GetSettings();
            var lowGoods = new HashSet<string>(
                Discovery.Goods.Where(g => g.IsBelowThreshold).Select(g => g.Id),
                StringComparer.OrdinalIgnoreCase
            );
            var blockedIngredientGoods = new HashSet<string>(Discovery.BlockedIngredientGoods, StringComparer.OrdinalIgnoreCase);

            ApplyIndicatorState(workshop.Base, workshop, lowGoods, blockedIngredientGoods, settings);
        }

        public static void RestoreVanilla()
        {
            var restored = new HashSet<int>();
            foreach (var workshop in GetRecipeBuildings())
            {
                var building = workshop.Base;
                if (building == null)
                {
                    continue;
                }

                if (restored.Add(building.Id))
                {
                    RestoreVanillaFor(building);
                }
            }

            foreach (var building in GameAPI.GetGatheringSourceBuildings())
            {
                if (building != null && restored.Add(building.Id))
                {
                    RestoreVanillaFor(building);
                }
            }

            foreach (var mine in GameAPI.GetMineBuildings())
            {
                if (mine != null && restored.Add(mine.Id))
                {
                    RestoreVanillaFor(mine);
                }
            }

            foreach (var farm in GameAPI.GetFarmBuildings())
            {
                if (farm != null && restored.Add(farm.Id))
                {
                    RestoreVanillaFor(farm);
                }
            }

            foreach (var rainCatcher in GameAPI.GetRainCatcherBuildings())
            {
                if (rainCatcher != null && restored.Add(rainCatcher.Id))
                {
                    RestoreVanillaFor(rainCatcher);
                }
            }

            foreach (var hearth in GameAPI.GetHearthBuildings())
            {
                if (hearth != null && restored.Add(hearth.Id))
                {
                    RestoreVanillaFor(hearth);
                }
            }

            LastApplied.Clear();
        }

        private static IEnumerable<IWorkshop> GetRecipeBuildings()
        {
            foreach (var workshop in GameAPI.GetRecipeBuildings())
            {
                yield return workshop;
            }
        }

        private static void ApplyIndicatorState(
            ProductionBuilding building,
            IWorkshop workshop,
            HashSet<string> lowGoods,
            HashSet<string> blockedIngredientGoods,
            Settings settings)
        {
            var icon = building.BuildingView?.transform?.Find("ToRotate/UI/NoWorkersIcon")?.gameObject;
            if (icon == null)
            {
                return;
            }

            MakeIconClickThrough(icon);
            var workerCount = building.CountWorkers();
            var maxWorkers = building.Workplaces?.Length ?? 0;
            var lowProductGoods = GetDemandedRecipeGoods(workshop, lowGoods);
            var hasRelevantLowRecipe = lowProductGoods.Count > 0;
            var hasCraftableLowRecipe = hasRelevantLowRecipe && HasCraftableDemandedRecipe(workshop, lowGoods, settings);
            var hasRelevantIngredientSupplyRecipe = workshop.Recipes.Any(r => IsEnabledGatheringSupplyRecipe(r, blockedIngredientGoods, settings));
            var hasRelevantWork = hasRelevantLowRecipe || hasRelevantIngredientSupplyRecipe;
            var hasOnlyIngredientBlockedWork = hasRelevantLowRecipe &&
                                               !hasCraftableLowRecipe &&
                                               !hasRelevantIngredientSupplyRecipe;

            var active = false;
            var color = WhiteColor;

            if (workerCount == 0)
            {
                active = true;
                color = hasOnlyIngredientBlockedWork ? GreyColor : hasRelevantWork ? RedColor : WhiteColor;
            }
            else if (hasRelevantWork && workerCount < maxWorkers)
            {
                active = true;
                color = hasOnlyIngredientBlockedWork ? GreyColor : YellowColor;
            }

            ApplySnapshot(
                building.Id,
                icon,
                active,
                color,
                active && hasRelevantLowRecipe && ConfigManager.ShowBuildingSpecificItemIndicators ? lowProductGoods : null
            );
        }

        private static void ApplyGatheringIndicatorState(ProductionBuilding building, HashSet<string> blockedIngredientGoods)
        {
            var icon = building.BuildingView?.transform?.Find("ToRotate/UI/NoWorkersIcon")?.gameObject;
            if (icon == null)
            {
                return;
            }

            MakeIconClickThrough(icon);
            var workerCount = building.CountWorkers();
            var maxWorkers = building.Workplaces?.Length ?? 0;
            var hasRelevantGathering = HasRelevantGatheringRecipe(building, blockedIngredientGoods);

            var active = false;
            var color = WhiteColor;

            if (workerCount == 0)
            {
                active = true;
                color = hasRelevantGathering ? RedColor : WhiteColor;
            }
            else if (hasRelevantGathering && workerCount < maxWorkers)
            {
                active = true;
                color = YellowColor;
            }

            ApplySnapshot(building.Id, icon, active, color, null);
        }

        private static void ApplyMineIndicatorState(Mine mine, HashSet<string> blockedIngredientGoods)
        {
            var icon = mine.BuildingView?.transform?.Find("ToRotate/UI/NoWorkersIcon")?.gameObject;
            if (icon == null)
            {
                return;
            }

            MakeIconClickThrough(icon);
            var workerCount = mine.CountWorkers();
            var maxWorkers = mine.Workplaces?.Length ?? 0;
            var relevantGoods = GetRelevantMineGoods(mine, blockedIngredientGoods);
            var hasRelevantMining = relevantGoods.Count > 0;

            var active = false;
            var color = WhiteColor;

            if (workerCount == 0)
            {
                active = true;
                color = hasRelevantMining ? RedColor : WhiteColor;
            }
            else if (hasRelevantMining && workerCount < maxWorkers)
            {
                active = true;
                color = YellowColor;
            }

            ApplySnapshot(
                mine.Id,
                icon,
                active,
                color,
                active && hasRelevantMining && ConfigManager.ShowBuildingSpecificItemIndicators ? relevantGoods : null
            );
        }

        private static void ApplyFarmIndicatorState(Farm farm)
        {
            var icon = farm.BuildingView?.transform?.Find("ToRotate/UI/NoWorkersIcon")?.gameObject;
            if (icon == null)
            {
                return;
            }

            MakeIconClickThrough(icon);
            var workerCount = farm.CountWorkers();
            var maxWorkers = farm.Workplaces?.Length ?? 0;
            var hasRelevantFieldWork = GameAPI.HasFarmFieldWork(farm);

            var active = false;
            var color = WhiteColor;

            if (workerCount == 0)
            {
                active = true;
                color = hasRelevantFieldWork ? RedColor : WhiteColor;
            }
            else if (hasRelevantFieldWork && workerCount < maxWorkers)
            {
                active = true;
                color = YellowColor;
            }

            ApplySnapshot(farm.Id, icon, active, color, null);
        }

        private static void ApplyRainCatcherIndicatorState(RainCatcher rainCatcher)
        {
            var icon = rainCatcher.BuildingView?.transform?.Find("ToRotate/UI/NoWorkersIcon")?.gameObject;
            if (icon == null)
            {
                return;
            }

            MakeIconClickThrough(icon);
            var workerCount = rainCatcher.CountWorkers();
            var maxWorkers = rainCatcher.Workplaces?.Length ?? 0;
            var canFillWaterTank = GameAPI.CanRainCatcherFillWaterTank(rainCatcher);

            var active = false;
            var color = WhiteColor;

            if (workerCount == 0)
            {
                active = true;
                color = canFillWaterTank ? RedColor : WhiteColor;
            }
            else if (canFillWaterTank && workerCount < maxWorkers)
            {
                active = true;
                color = YellowColor;
            }

            ApplySnapshot(rainCatcher.Id, icon, active, color, null);
        }

        private static void ApplyHearthIndicatorState(Hearth hearth)
        {
            var icon = hearth.BuildingView?.transform?.Find("ToRotate/UI/NoWorkersIcon")?.gameObject;
            if (icon == null)
            {
                return;
            }

            MakeIconClickThrough(icon);
            var hasNoFirekeeper = hearth.CountWorkers() == 0;
            var active = hearth.state != null &&
                         hearth.state.finished &&
                         !hearth.state.isSleeping &&
                         hasNoFirekeeper;
            var needsFuelDelivery = active &&
                                    hearth.state.isUsingFire &&
                                    hearth.model != null &&
                                    hearth.state.burningTimeLeft < hearth.model.minBurningTimeToRequestFuel;

            ApplySnapshot(hearth.Id, icon, active, needsFuelDelivery ? RedColor : WhiteColor, null);
        }

        private static bool HasRelevantGatheringRecipe(ProductionBuilding building, HashSet<string> blockedIngredientGoods)
        {
            if (building?.BuildingModel == null ||
                blockedIngredientGoods == null ||
                blockedIngredientGoods.Count == 0)
            {
                return false;
            }

            var recipes = GetRecipeStates(building);
            if (recipes == null || recipes.Count == 0)
            {
                return false;
            }

            foreach (var blockedGood in blockedIngredientGoods)
            {
                if (string.IsNullOrWhiteSpace(blockedGood))
                {
                    continue;
                }

                var goodModel = GameAPI.GetSettings()?.GetGood(blockedGood);
                if (goodModel == null || !building.BuildingModel.IsSourceOf(goodModel))
                {
                    continue;
                }

                foreach (var recipeState in recipes)
                {
                    if (recipeState == null || !recipeState.active)
                    {
                        continue;
                    }

                    if (RecipeProducesGood(building, recipeState, blockedGood))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static List<string> GetRelevantMineGoods(Mine mine, HashSet<string> blockedIngredientGoods)
        {
            var result = new List<string>();
            if (mine?.state?.recipes == null ||
                blockedIngredientGoods == null ||
                blockedIngredientGoods.Count == 0)
            {
                return result;
            }

            foreach (var recipeState in mine.state.recipes)
            {
                if (recipeState == null || !recipeState.active || !mine.CanGather(recipeState))
                {
                    continue;
                }

                var recipe = mine.GetRecipeModel(recipeState);
                var producedGood = recipe?.GetProducedGood();
                if (string.IsNullOrWhiteSpace(producedGood) ||
                    !blockedIngredientGoods.Contains(producedGood) ||
                    mine.GetChargesLeft(producedGood) <= 0)
                {
                    continue;
                }

                if (!result.Contains(producedGood, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(producedGood);
                }
            }

            return result;
        }

        private static List<RecipeState> GetRecipeStates(ProductionBuilding building)
        {
            switch (building)
            {
                case GathererHut gathererHut:
                    return gathererHut.state?.recipes;
                case Camp camp:
                    return camp.state?.recipes;
                default:
                    return null;
            }
        }

        private static bool RecipeProducesGood(ProductionBuilding building, RecipeState recipeState, string blockedGood)
        {
            switch (building)
            {
                case GathererHut gathererHut:
                    return string.Equals(gathererHut.GetRecipeModel(recipeState)?.GetProducedGood(), blockedGood, StringComparison.OrdinalIgnoreCase);
                case Camp camp:
                    return string.Equals(camp.GetRecipeModel(recipeState)?.GetProducedGood(), blockedGood, StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }

        private static void ApplySnapshot(int buildingId, GameObject icon, bool active, Color color, IReadOnlyList<string> productGoodIds)
        {
            var snapshot = new IndicatorSnapshot(active, color, productGoodIds);
            var currentActive = icon.activeSelf;
            var currentColor = GetCurrentIconColor(icon);
            if (LastApplied.TryGetValue(buildingId, out var previous)
                && previous.Equals(snapshot)
                && currentActive == active
                && ColorsEqual(currentColor, color))
            {
                if (active && productGoodIds != null && productGoodIds.Count > 0)
                {
                    RefreshProductIconRenderOrder(icon);
                }

                return;
            }

            icon.SetActive(active);
            foreach (var spriteRenderer in GetBaseIconRenderers(icon))
            {
                spriteRenderer.color = color;
            }

            ApplyProductIcons(buildingId, icon, active ? productGoodIds : null);
            LastApplied[buildingId] = snapshot;
        }

        private static Color GetCurrentIconColor(GameObject icon)
        {
            if (icon == null)
            {
                return Color.clear;
            }

            var spriteRenderer = GetBaseIconRenderers(icon).FirstOrDefault();
            return spriteRenderer != null ? spriteRenderer.color : Color.clear;
        }

        private static List<string> GetDemandedRecipeGoods(IWorkshop workshop, HashSet<string> lowGoods)
        {
            var result = new List<string>();
            if (workshop?.Recipes == null)
            {
                return result;
            }

            foreach (var recipe in workshop.Recipes)
            {
                if (!IsDemandedRecipe(workshop, recipe, lowGoods) ||
                    result.Any(g => string.Equals(g, recipe.productName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                result.Add(recipe.productName);
                if (result.Count >= MaxProductIcons)
                {
                    break;
                }
            }

            return result;
        }

        private static bool HasCraftableDemandedRecipe(IWorkshop workshop, HashSet<string> lowGoods, Settings settings)
        {
            if (workshop?.Recipes == null || settings == null)
            {
                return false;
            }

            foreach (var recipeState in workshop.Recipes)
            {
                if (!IsDemandedRecipe(workshop, recipeState, lowGoods))
                {
                    continue;
                }

                var recipeModel = settings.GetWorkshopRecipe(recipeState.model);
                if (recipeModel != null && Discovery.CanRecipeContinue(workshop, recipeModel))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyProductIcons(int buildingId, GameObject icon, IReadOnlyList<string> productGoodIds)
        {
            if (icon == null || productGoodIds == null || productGoodIds.Count == 0)
            {
                DestroyProductIcons(icon);
                return;
            }

            var baseRenderer = GetBaseIconRenderers(icon).FirstOrDefault(r => r?.sprite != null);
            if (baseRenderer == null)
            {
                DestroyProductIcons(icon);
                return;
            }

            var root = GetOrCreateProductIconsRoot(icon);
            if (root == null)
            {
                return;
            }

            var baseSize = GetRendererSizeInIconSpace(icon.transform, baseRenderer);
            var baseHeight = Mathf.Max(0.01f, baseSize.y * ConfigManager.BuildingShortageIconScale);
            const float gapRatio = 0.08f;
            var iconSize = baseHeight / (ProductIconGridRows + (ProductIconGridRows - 1) * gapRatio);
            var gap = iconSize * gapRatio;
            var x = Mathf.Max(baseSize.x * 0.55f + iconSize * 0.5f, iconSize * 1.15f);
            var shown = 0;

            for (var i = 0; i < productGoodIds.Count; i++)
            {
                var good = Discovery.Goods
                    .FirstOrDefault(g => string.Equals(g.Id, productGoodIds[i], StringComparison.OrdinalIgnoreCase));
                var sprite = good?.Icon;
                if (sprite == null)
                {
                    continue;
                }

                var child = GetOrCreateProductIcon(root, shown);
                var renderer = child.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    continue;
                }

                renderer.sprite = sprite;
                renderer.color = WhiteColor;
                ConfigureProductIconRenderer(renderer, baseRenderer, 1);
                ApplyProductIconBorder(child, sprite, good.IsIngredientBlocked, baseRenderer);

                var row = shown % ProductIconGridRows;
                var column = shown / ProductIconGridRows;
                var y = row == 0 ? (iconSize + gap) * 0.5f : -(iconSize + gap) * 0.5f;
                child.transform.localPosition = new Vector3(x + column * (iconSize + gap), y, 0f);
                child.transform.localRotation = Quaternion.identity;
                child.transform.localScale = Vector3.one * GetScaleForHeight(sprite, iconSize);
                child.SetActive(true);
                shown++;
            }

            for (var i = shown; i < root.transform.childCount; i++)
            {
                root.transform.GetChild(i).gameObject.SetActive(false);
            }

            root.SetActive(shown > 0);
            if (shown == 0)
            {
                DestroyProductIcons(icon);
                LastApplied.Remove(buildingId);
            }
        }

        private static GameObject GetOrCreateProductIconsRoot(GameObject icon)
        {
            var existing = icon.transform.Find(ProductIconsRootName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var root = new GameObject(ProductIconsRootName);
            root.transform.SetParent(icon.transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, ProductIconForwardOffset);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static GameObject GetOrCreateProductIcon(GameObject root, int index)
        {
            while (root.transform.childCount <= index)
            {
                var child = new GameObject("ProductIcon");
                child.transform.SetParent(root.transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.material = new Material(Shader.Find("Sprites/Default"));
            }

            return root.transform.GetChild(index).gameObject;
        }

        private static void RefreshProductIconRenderOrder(GameObject icon)
        {
            var baseRenderer = GetBaseIconRenderers(icon).FirstOrDefault(r => r?.sprite != null);
            var root = icon?.transform?.Find(ProductIconsRootName);
            if (baseRenderer == null || root == null)
            {
                return;
            }

            root.localPosition = new Vector3(root.localPosition.x, root.localPosition.y, ProductIconForwardOffset);
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var offset = IsBorderPart(renderer.transform) ? 2 : 1;
                ConfigureProductIconRenderer(renderer, baseRenderer, offset);
            }
        }

        private static void ApplyProductIconBorder(GameObject productIcon, Sprite sprite, bool active, SpriteRenderer baseRenderer)
        {
            var borderRoot = GetOrCreateProductIconBorder(productIcon);
            if (borderRoot == null)
            {
                return;
            }

            borderRoot.SetActive(active);
            if (!active || sprite == null)
            {
                return;
            }

            var size = sprite.bounds.size;
            var thickness = Mathf.Max(size.y * ProductIconBorderThicknessScale, 0.01f);
            var width = size.x + thickness * 2f;
            var height = size.y + thickness * 2f;

            ConfigureBorderPart(borderRoot.transform.Find("Top")?.GetComponent<SpriteRenderer>(), 0f, height * 0.5f - thickness * 0.5f, width, thickness, baseRenderer);
            ConfigureBorderPart(borderRoot.transform.Find("Bottom")?.GetComponent<SpriteRenderer>(), 0f, -height * 0.5f + thickness * 0.5f, width, thickness, baseRenderer);
            ConfigureBorderPart(borderRoot.transform.Find("Left")?.GetComponent<SpriteRenderer>(), -width * 0.5f + thickness * 0.5f, 0f, thickness, height, baseRenderer);
            ConfigureBorderPart(borderRoot.transform.Find("Right")?.GetComponent<SpriteRenderer>(), width * 0.5f - thickness * 0.5f, 0f, thickness, height, baseRenderer);
        }

        private static GameObject GetOrCreateProductIconBorder(GameObject productIcon)
        {
            if (productIcon == null)
            {
                return null;
            }

            var existing = productIcon.transform.Find(ProductIconBorderRootName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var borderRoot = new GameObject(ProductIconBorderRootName);
            borderRoot.transform.SetParent(productIcon.transform, false);
            borderRoot.transform.localPosition = Vector3.zero;
            borderRoot.transform.localRotation = Quaternion.identity;
            borderRoot.transform.localScale = Vector3.one;

            CreateBorderPart(borderRoot.transform, "Top");
            CreateBorderPart(borderRoot.transform, "Bottom");
            CreateBorderPart(borderRoot.transform, "Left");
            CreateBorderPart(borderRoot.transform, "Right");
            borderRoot.SetActive(false);
            return borderRoot;
        }

        private static void CreateBorderPart(Transform parent, string name)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSolidSprite();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        private static void ConfigureBorderPart(SpriteRenderer renderer, float x, float y, float width, float height, SpriteRenderer baseRenderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.color = BorderRedColor;
            ConfigureProductIconRenderer(renderer, baseRenderer, 2);
            renderer.transform.localPosition = new Vector3(x, y, 0f);
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = new Vector3(width, height, 1f);
        }

        private static void ConfigureProductIconRenderer(SpriteRenderer renderer, SpriteRenderer baseRenderer, int offset)
        {
            if (renderer == null || baseRenderer == null)
            {
                return;
            }

            if (baseRenderer.sharedMaterial != null)
            {
                renderer.sharedMaterial = baseRenderer.sharedMaterial;
            }

            renderer.sortingLayerID = baseRenderer.sortingLayerID;
            renderer.sortingOrder = baseRenderer.sortingOrder + ProductIconSortingOrderOffset + offset;
        }

        private static bool IsBorderPart(Transform transform)
        {
            return transform?.parent != null &&
                   string.Equals(transform.parent.name, ProductIconBorderRootName, StringComparison.Ordinal);
        }

        private static Vector2 GetRendererSizeInIconSpace(Transform iconTransform, SpriteRenderer renderer)
        {
            if (iconTransform == null || renderer?.sprite == null)
            {
                return Vector2.one;
            }

            var bounds = renderer.sprite.bounds.size;
            var width = iconTransform.InverseTransformVector(renderer.transform.TransformVector(new Vector3(bounds.x, 0f, 0f))).magnitude;
            var height = iconTransform.InverseTransformVector(renderer.transform.TransformVector(new Vector3(0f, bounds.y, 0f))).magnitude;
            return new Vector2(Mathf.Max(0.01f, width), Mathf.Max(0.01f, height));
        }

        private static float GetScaleForHeight(Sprite sprite, float targetHeight)
        {
            var height = sprite?.bounds.size.y ?? 0f;
            return height > 0.001f ? targetHeight / height : 1f;
        }

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null)
            {
                return _solidSprite;
            }

            const int size = 4;
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels(Enumerable.Repeat(Color.white, size * size).ToArray());
            texture.Apply(false, true);
            _solidSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return _solidSprite;
        }

        private static IEnumerable<SpriteRenderer> GetBaseIconRenderers(GameObject icon)
        {
            return icon.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(r => r != null && !IsProductIconTransform(r.transform));
        }

        private static bool IsProductIconTransform(Transform transform)
        {
            while (transform != null)
            {
                if (string.Equals(transform.name, ProductIconsRootName, StringComparison.Ordinal))
                {
                    return true;
                }

                transform = transform.parent;
            }

            return false;
        }

        private static void RemoveProductIcons(int buildingId)
        {
            foreach (var workshop in GetRecipeBuildings())
            {
                if (workshop?.Base?.Id != buildingId)
                {
                    continue;
                }

                var icon = workshop.Base.BuildingView?.transform?.Find("ToRotate/UI/NoWorkersIcon")?.gameObject;
                DestroyProductIcons(icon);
                return;
            }

            foreach (var farm in GameAPI.GetFarmBuildings())
            {
                if (farm?.Id != buildingId)
                {
                    continue;
                }

                var icon = farm.BuildingView?.transform?.Find("ToRotate/UI/NoWorkersIcon")?.gameObject;
                DestroyProductIcons(icon);
                return;
            }
        }

        private static void DestroyProductIcons(GameObject icon)
        {
            var root = icon?.transform?.Find(ProductIconsRootName);
            if (root != null)
            {
                UnityEngine.Object.Destroy(root.gameObject);
            }
        }

        private static void RestoreVanillaFor(ProductionBuilding building)
        {
            var icon = building.BuildingView?.transform?.Find("ToRotate/UI/NoWorkersIcon")?.gameObject;
            if (icon == null)
            {
                return;
            }

            MakeIconClickThrough(icon);
            var active = building.BuildingState != null &&
                         building.BuildingState.finished &&
                         !building.BuildingState.isSleeping &&
                         building.CountWorkers() == 0;

            icon.SetActive(active);
            foreach (var spriteRenderer in GetBaseIconRenderers(icon))
            {
                spriteRenderer.color = WhiteColor;
            }

            DestroyProductIcons(icon);
        }

        private static bool ColorsEqual(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r)
                && Mathf.Approximately(a.g, b.g)
                && Mathf.Approximately(a.b, b.b)
                && Mathf.Approximately(a.a, b.a);
        }

        private static void MakeIconClickThrough(GameObject icon)
        {
            if (icon == null)
            {
                return;
            }

            foreach (var graphic in icon.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            foreach (var selectable in icon.GetComponentsInChildren<Selectable>(true))
            {
                selectable.interactable = false;
            }

            foreach (var raycaster in icon.GetComponentsInChildren<GraphicRaycaster>(true))
            {
                raycaster.enabled = false;
            }

            foreach (var trigger in icon.GetComponentsInChildren<EventTrigger>(true))
            {
                trigger.enabled = false;
            }

            foreach (var component in icon.GetComponentsInChildren<Component>(true))
            {
                var type = component?.GetType();
                if (type == null)
                {
                    continue;
                }

                var typeName = type.Name;
                if (typeName.IndexOf("Collider", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                var enabledProperty = type.GetProperty("enabled");
                if (enabledProperty != null && enabledProperty.CanWrite)
                {
                    enabledProperty.SetValue(component, false, null);
                }
            }
        }

        private static bool IsDemandedRecipe(IWorkshop workshop, WorkshopRecipeState recipeState, HashSet<string> lowGoods)
        {
            if (recipeState == null ||
                !recipeState.active ||
                string.IsNullOrWhiteSpace(recipeState.productName))
            {
                return false;
            }

            return (lowGoods != null && lowGoods.Contains(recipeState.productName)) ||
                   IsBelowLocalRecipeLimit(workshop, recipeState);
        }

        private static bool IsBelowLocalRecipeLimit(IWorkshop workshop, WorkshopRecipeState recipeState)
        {
            if (workshop?.Base == null ||
                recipeState == null ||
                !recipeState.isLimitLocal ||
                recipeState.limit <= 0 ||
                string.IsNullOrWhiteSpace(recipeState.productName))
            {
                return false;
            }

            return GetCombinedStoredAmount(workshop.Base, recipeState.productName) < recipeState.limit;
        }

        private static int GetCombinedStoredAmount(ProductionBuilding building, string goodId)
        {
            if (building == null || string.IsNullOrWhiteSpace(goodId))
            {
                return 0;
            }

            var storageAmount = GameAPI.GetStoredAmount(null, goodId);
            var localAmount = building.ProductionStorage?.GetAmount(goodId) ?? 0;
            return storageAmount + localAmount;
        }

        private static bool IsEnabledGatheringSupplyRecipe(
            WorkshopRecipeState recipeState,
            HashSet<string> blockedIngredientGoods,
            Settings settings)
        {
            if (recipeState == null ||
                !recipeState.active ||
                blockedIngredientGoods == null ||
                blockedIngredientGoods.Count == 0 ||
                string.IsNullOrWhiteSpace(recipeState.productName) ||
                !blockedIngredientGoods.Contains(recipeState.productName))
            {
                return false;
            }

            var recipeModel = settings?.GetWorkshopRecipe(recipeState.model);
            return recipeModel?.requiredGoods == null || recipeModel.requiredGoods.Length == 0;
        }

        private readonly struct IndicatorSnapshot : IEquatable<IndicatorSnapshot>
        {
            public IndicatorSnapshot(bool active, Color color, IReadOnlyList<string> productGoodIds)
            {
                Active = active;
                Color = color;
                ShowProductIcons = ConfigManager.ShowBuildingSpecificItemIndicators;
                ProductIconScale = ConfigManager.BuildingShortageIconScale;
                ProductGoodsKey = productGoodIds == null || productGoodIds.Count == 0
                    ? string.Empty
                    : string.Join("|", productGoodIds.Select(BuildProductGoodsKey));
            }

            public bool Active { get; }

            public Color Color { get; }

            public bool ShowProductIcons { get; }

            public float ProductIconScale { get; }

            public string ProductGoodsKey { get; }

            public bool Equals(IndicatorSnapshot other)
            {
                return Active == other.Active &&
                       Color.Equals(other.Color) &&
                       ShowProductIcons == other.ShowProductIcons &&
                       Mathf.Approximately(ProductIconScale, other.ProductIconScale) &&
                       string.Equals(ProductGoodsKey, other.ProductGoodsKey, StringComparison.Ordinal);
            }

            private static string BuildProductGoodsKey(string goodId)
            {
                var good = Discovery.Goods
                    .FirstOrDefault(g => string.Equals(g.Id, goodId, StringComparison.OrdinalIgnoreCase));
                return goodId + (good?.IsIngredientBlocked == true ? ":blocked" : ":normal");
            }
        }
    }

    [HarmonyPatch(typeof(ProductionBuildingView), "ShowWorkers")]
    internal static class BuildingAlertIndicatorsViewPatch
    {
        [HarmonyPostfix]
        private static void ShowWorkersPostfix(ProductionBuildingView __instance)
        {
            if (__instance == null || !GameAPI.IsGameActive())
            {
                return;
            }

            BuildingAlertIndicators.RefreshForView(__instance);
        }
    }
}
