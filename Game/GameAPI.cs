using Eremite;
using Eremite.Model;
using Eremite.Model.Orders;
using Eremite.Model.State;
using Eremite.Services;
using Eremite.Services.Monitors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Eremite.Buildings;
using Eremite.Characters.Villagers;
using Eremite.MapObjects;
using System.Linq;
using StockAlert.Core.Models;
using UnityEngine;

namespace StockAlert.Game
{
    public static class GameAPI
    {
        private static PropertyInfo _piMbSettings;
        private static PropertyInfo _piStorageService;
        private static PropertyInfo _piRainpunkService;
        private static PropertyInfo _piWorkshopsService;
        private static PropertyInfo _piGameContentService;
        private static PropertyInfo _piRecipesService;
        private static PropertyInfo _piConstructionService;
        private static PropertyInfo _piOrdersService;
        private static PropertyInfo _piVillagersService;
        private static PropertyInfo _piBuildingsService;
        private static PropertyInfo _piBlightService;
        private static PropertyInfo _piRainpunkService;
        private static PropertyInfo _piLocalStorages;
        private static PropertyInfo _piIngredientsStorages;
        private static PropertyInfo _piIsGameActive;
        private static PropertyInfo _piCalendarService;
        private static PropertyInfo _piTimeScaleService;
        private static PropertyInfo _piNewsService;
        private static PropertyInfo _piResourcesService;
        private static PropertyInfo _piEffectsService;
        private static PropertyInfo _piStateService;
        private static PropertyInfo _piDepositsService;
        private static PropertyInfo _piLakesService;
        private static PropertyInfo _piOreService;
        private static PropertyInfo _piGladesService;
        private static MethodInfo _miGetAmount;
        private static MethodInfo _miGetGlobalLimit;
        private static MethodInfo _miSetGlobalLimit;
        private static MethodInfo _miIsBuildingUnlocked;
        private static MethodInfo _miGetRecipesForBuilding;
        private static MethodInfo _miGetAliveRaceAmount;
        private static MethodInfo _miGetGlobalActiveCysts;
        private static MethodInfo _miCountWaterLeft;
        private static MethodInfo _miCountTanksCapacity;
        private static MethodInfo _miGetDefaultProfessionAmount;
        private static MethodInfo _miGetDefaultProfessionVillager;
        private static MethodInfo _miSetProfession;
        private static MethodInfo _miGetTimeTillNextSeasonChange;
        private static MethodInfo _miPause;
        private static MethodInfo _miPublishNews;
        private static MethodInfo _miRemoveNews;
        private static MethodInfo _miIsGlade;
        private static PropertyInfo _piWorkshopLimits;
        private static PropertyInfo _piWorkshops;
        private static PropertyInfo _piBlightPosts;
        private static PropertyInfo _piGathererHuts;
        private static PropertyInfo _piCamps;
        private static PropertyInfo _piFarms;
        private static PropertyInfo _piFarmfields;
        private static PropertyInfo _piMines;
        private static PropertyInfo _piRainCatchers;
        private static PropertyInfo _piStorages;
        private static PropertyInfo _piConstructions;
        private static FieldInfo _fiCurrentNews;
        private static PropertyInfo _piReactiveValue;

        public sealed class AvailableWorkshopRecipe
        {
            public AvailableWorkshopRecipe(WorkshopRecipeModel recipe, ProductionBuilding sourceBuilding, BuildingModel sourceModel)
            {
                Recipe = recipe;
                SourceBuilding = sourceBuilding;
                SourceModel = sourceModel;
            }

            public WorkshopRecipeModel Recipe { get; }

            public ProductionBuilding SourceBuilding { get; }

            public BuildingModel SourceModel { get; }
        }

        public sealed class BuildingCountInfo
        {
            public BuildingCountInfo(BuildingModel model, int count)
            {
                Model = model;
                Count = count;
            }

            public BuildingModel Model { get; }

            public int Count { get; }
        }

        public sealed class TimedOrderInfo
        {
            public TimedOrderInfo(int id, string displayName, float timeLeft)
            {
                Id = id;
                DisplayName = displayName;
                TimeLeft = timeLeft;
            }

            public int Id { get; }

            public string DisplayName { get; }

            public float TimeLeft { get; }
        }

        public sealed class ForestEaterDirectionInfo
        {
            public ForestEaterDirectionInfo(int index, Vector2Int current, Vector2Int target, List<Vector2Int> currentPath)
            {
                Index = index;
                Current = current;
                Target = target;
                CurrentPath = currentPath ?? new List<Vector2Int>();
            }

            public int Index { get; }

            public Vector2Int Current { get; }

            public Vector2Int Target { get; }

            public List<Vector2Int> CurrentPath { get; }
        }

        public static Settings GetSettings()
        {
            if (_piMbSettings == null)
            {
                _piMbSettings = typeof(MB).GetProperty("Settings", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piMbSettings?.GetValue(null, null) as Settings;
        }

        public static int GetStoredAmount(GoodModel model, string fallbackId)
        {
            if (model == null && string.IsNullOrWhiteSpace(fallbackId))
            {
                return 0;
            }

            try
            {
                if (_piStorageService == null)
                {
                    _piStorageService = typeof(GameMB).GetProperty("StorageService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                }

                var storage = _piStorageService?.GetValue(null, null);
                if (storage == null)
                {
                    return 0;
                }

                if (_miGetAmount == null)
                {
                    _miGetAmount = storage.GetType().GetMethod("GetAmount", new[] { typeof(string) });
                }

                if (_miGetAmount == null)
                {
                    return 0;
                }

                var goodName = model != null ? model.Name : fallbackId;
                return (int)_miGetAmount.Invoke(storage, new object[] { goodName });
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static List<ForestEaterDirectionInfo> GetForestEaterDirections()
        {
            var result = new List<ForestEaterDirectionInfo>();

            try
            {
                var effects = GetEffectsState();
                var states = effects?.forestEaters;
                if (states == null)
                {
                    return result;
                }

                for (var i = 0; i < states.Count; i++)
                {
                    var state = states[i];
                    if (state == null)
                    {
                        continue;
                    }

                    var path = state.currentPath != null
                        ? new List<Vector2Int>(state.currentPath)
                        : new List<Vector2Int>();
                    result.Add(new ForestEaterDirectionInfo(i, state.current, state.target, path));
                }
            }
            catch (Exception)
            {
                return result;
            }

            return result;
        }

        public static int GetAmountInNonWarehouseBuildings(string goodId)
        {
            if (string.IsNullOrWhiteSpace(goodId))
            {
                return 0;
            }

            try
            {
                var storageService = GetStorageService();
                if (storageService == null)
                {
                    return 0;
                }

                var total = 0;

                _piLocalStorages ??= storageService.GetType().GetProperty("LocalStorages", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var localStorages = _piLocalStorages?.GetValue(storageService, null) as IDictionary;
                if (localStorages != null)
                {
                    foreach (DictionaryEntry entry in localStorages)
                    {
                        if (entry.Value is BuildingStorage storage)
                        {
                            total += storage.GetAmount(goodId);
                        }
                    }
                }

                _piIngredientsStorages ??= storageService.GetType().GetProperty("IngredientsStorages", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var ingredientStorages = _piIngredientsStorages?.GetValue(storageService, null) as IDictionary;
                if (ingredientStorages != null)
                {
                    foreach (DictionaryEntry entry in ingredientStorages)
                    {
                        if (entry.Value is BuildingIngredientsStorage storage && storage.goods != null)
                        {
                            total += storage.goods.GetAmount(goodId);
                        }
                    }
                }

                return total;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static bool IsGameActive()
        {
            try
            {
                if (_piIsGameActive == null)
                {
                    _piIsGameActive = typeof(GameMB).GetProperty("IsGameActive", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                }

                if (_piIsGameActive == null)
                {
                    return false;
                }

                var value = _piIsGameActive.GetValue(null, null);
                return value is bool isActive && isActive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static Dictionary<string, int> GetProductionLimitsSnapshot()
        {
            var snapshot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var workshopsService = GetWorkshopsService();
                if (workshopsService == null)
                {
                    return snapshot;
                }

                if (_piWorkshopLimits == null)
                {
                    _piWorkshopLimits = workshopsService.GetType().GetProperty("Limits", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                var limits = _piWorkshopLimits?.GetValue(workshopsService, null) as IDictionary;
                if (limits == null)
                {
                    return snapshot;
                }

                foreach (DictionaryEntry entry in limits)
                {
                    if (entry.Key is string key && entry.Value is int value)
                    {
                        snapshot[key] = value;
                    }
                }
            }
            catch (Exception)
            {
            }

            return snapshot;
        }

        public static int GetProductionLimit(GoodModel model, string fallbackId)
        {
            if (string.IsNullOrWhiteSpace(fallbackId))
            {
                return 0;
            }

            try
            {
                var workshopsService = GetWorkshopsService();
                if (workshopsService == null)
                {
                    return 0;
                }

                if (_miGetGlobalLimit == null)
                {
                    _miGetGlobalLimit = workshopsService.GetType().GetMethod("GetGlobalLimitFor", new[] { typeof(string) });
                }

                if (_miGetGlobalLimit == null)
                {
                    return 0;
                }

                return (int)_miGetGlobalLimit.Invoke(workshopsService, new object[] { fallbackId });
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static void SetProductionLimit(string goodId, int limit)
        {
            if (string.IsNullOrWhiteSpace(goodId))
            {
                return;
            }

            try
            {
                var workshopsService = GetWorkshopsService();
                if (workshopsService == null)
                {
                    return;
                }

                if (_miSetGlobalLimit == null)
                {
                    _miSetGlobalLimit = workshopsService.GetType().GetMethod("SetGlobalLimitFor", new[] { typeof(string), typeof(int) });
                }

                _miSetGlobalLimit?.Invoke(workshopsService, new object[] { goodId, limit });
            }
            catch (Exception)
            {
            }
        }

        public static void RefreshWorkshopLimits()
        {
            try
            {
                var buildingsService = GetBuildingsService();
                if (buildingsService == null)
                {
                    return;
                }

                RefreshLimitsFromDictionary(buildingsService, "Workshops", ref _piWorkshops);
                RefreshLimitsFromDictionary(buildingsService, "BlightPosts", ref _piBlightPosts);
            }
            catch (Exception)
            {
            }
        }

        public static int GetAliveVillagerCount(string raceId)
        {
            if (string.IsNullOrWhiteSpace(raceId))
            {
                return 0;
            }

            try
            {
                if (_piVillagersService == null)
                {
                    _piVillagersService = typeof(GameMB).GetProperty("VillagersService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                }

                var villagersService = _piVillagersService?.GetValue(null, null);
                if (villagersService == null)
                {
                    return 0;
                }

                if (_miGetAliveRaceAmount == null)
                {
                    _miGetAliveRaceAmount = villagersService.GetType().GetMethod("GetAliveRaceAmount", new[] { typeof(string) });
                }

                if (_miGetAliveRaceAmount == null)
                {
                    return 0;
                }

                return (int)_miGetAliveRaceAmount.Invoke(villagersService, new object[] { raceId });
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static int GetDefaultProfessionAmount(string raceId)
        {
            if (string.IsNullOrWhiteSpace(raceId))
            {
                return 0;
            }

            try
            {
                var villagersService = GetVillagersService();
                if (villagersService == null)
                {
                    return 0;
                }

                if (_miGetDefaultProfessionAmount == null)
                {
                    _miGetDefaultProfessionAmount = villagersService.GetType().GetMethod("GetDefaultProfessionAmount", new[] { typeof(string) });
                }

                if (_miGetDefaultProfessionAmount == null)
                {
                    return 0;
                }

                return (int)_miGetDefaultProfessionAmount.Invoke(villagersService, new object[] { raceId });
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static Villager GetDefaultProfessionVillager(string raceId, ProductionBuilding building)
        {
            if (string.IsNullOrWhiteSpace(raceId) || building == null)
            {
                return null;
            }

            try
            {
                var villagersService = GetVillagersService();
                if (villagersService == null)
                {
                    return null;
                }

                if (_miGetDefaultProfessionVillager == null)
                {
                    _miGetDefaultProfessionVillager = villagersService.GetType().GetMethod("GetDefaultProfessionVillager", new[] { typeof(string), typeof(ProductionBuilding) });
                }

                return _miGetDefaultProfessionVillager?.Invoke(villagersService, new object[] { raceId, building }) as Villager;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void AssignVillagerToWorkplace(Villager villager, ProductionBuilding building, int workplaceIndex)
        {
            if (villager == null || building == null || workplaceIndex < 0)
            {
                return;
            }

            try
            {
                var villagersService = GetVillagersService();
                if (villagersService == null)
                {
                    return;
                }

                if (_miSetProfession == null)
                {
                    _miSetProfession = villagersService.GetType().GetMethod("SetProfession", new[] { typeof(Villager), typeof(string), typeof(ProductionBuilding), typeof(int), typeof(bool) });
                }

                _miSetProfession?.Invoke(villagersService, new object[] { villager, building.Profession, building, workplaceIndex, true });
            }
            catch (Exception)
            {
            }
        }

        public static int GetGlobalActiveCysts()
        {
            try
            {
                var blightService = GetBlightService();
                if (blightService == null)
                {
                    return 0;
                }

                if (_miGetGlobalActiveCysts == null)
                {
                    _miGetGlobalActiveCysts = blightService.GetType().GetMethod("GetGlobalActiveCysts", Type.EmptyTypes);
                }

                if (_miGetGlobalActiveCysts == null)
                {
                    return 0;
                }

                return (int)_miGetGlobalActiveCysts.Invoke(blightService, null);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static int GetCityWaterLeft(WaterModel water)
        {
            if (water == null)
            {
                return -1;
            }

            try
            {
                var rainpunkService = GetRainpunkService();
                if (rainpunkService == null)
                {
                    return -1;
                }

                if (_miCountWaterLeft == null)
                {
                    _miCountWaterLeft = rainpunkService.GetType().GetMethod("CountWaterLeft", new[] { typeof(WaterModel) });
                }

                if (_miCountWaterLeft == null)
                {
                    return -1;
                }

                return (int)_miCountWaterLeft.Invoke(rainpunkService, new object[] { water });
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public static int GetCityWaterCapacity(WaterModel water)
        {
            if (water == null)
            {
                return -1;
            }

            try
            {
                var rainpunkService = GetRainpunkService();
                if (rainpunkService == null)
                {
                    return -1;
                }

                if (_miCountTanksCapacity == null)
                {
                    _miCountTanksCapacity = rainpunkService.GetType().GetMethod("CountTanksCapacity", new[] { typeof(WaterModel) });
                }

                if (_miCountTanksCapacity == null)
                {
                    return -1;
                }

                return (int)_miCountTanksCapacity.Invoke(rainpunkService, new object[] { water });
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public static List<Workshop> GetWorkshopBuildings()
        {
            var result = new List<Workshop>();

            try
            {
                var buildingsService = GetBuildingsService();
                if (buildingsService == null)
                {
                    return result;
                }

                _piWorkshops ??= buildingsService.GetType().GetProperty("Workshops", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var entries = _piWorkshops?.GetValue(buildingsService, null) as IDictionary;
                if (entries == null)
                {
                    return result;
                }

                foreach (DictionaryEntry entry in entries)
                {
                    if (entry.Value is Workshop workshop)
                    {
                        result.Add(workshop);
                    }
                }
            }
            catch (Exception)
            {
            }

            return result;
        }

        public static bool IsBuildingSleeping(Building building)
        {
            return building?.BuildingState != null && building.BuildingState.isSleeping;
        }

        public static bool SleepBuilding(Building building)
        {
            if (building?.BuildingState == null || building.BuildingState.isSleeping)
            {
                return false;
            }

            try
            {
                var canSleepMethod = GetCachedInstanceMethod(CanSleepMethods, building, "CanSleep");
                if (canSleepMethod != null && canSleepMethod.Invoke(building, null) is bool canSleep && !canSleep)
                {
                    return false;
                }

                var sleepMethod = GetCachedInstanceMethod(SleepMethods, building, "Sleep");
                if (sleepMethod == null)
                {
                    return false;
                }

                sleepMethod.Invoke(building, null);
                return building.BuildingState.isSleeping;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool WakeUpBuilding(Building building)
        {
            if (building?.BuildingState == null || !building.BuildingState.isSleeping)
            {
                return false;
            }

            try
            {
                var wakeUpMethod = GetCachedInstanceMethod(WakeUpMethods, building, "WakeUp");
                if (wakeUpMethod == null)
                {
                    return false;
                }

                wakeUpMethod.Invoke(building, null);
                return !building.BuildingState.isSleeping;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static readonly Dictionary<Type, MethodInfo> CanSleepMethods = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, MethodInfo> SleepMethods = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, MethodInfo> WakeUpMethods = new Dictionary<Type, MethodInfo>();

        private static MethodInfo GetCachedInstanceMethod(Dictionary<Type, MethodInfo> cache, object instance, string methodName)
        {
            var type = instance.GetType();
            if (!cache.TryGetValue(type, out var method))
            {
                method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                cache[type] = method;
            }

            return method;
        }

        public static float GetTimeTillNextSeasonChange()
        {
            try
            {
                var calendarService = GetCalendarService();
                if (calendarService == null)
                {
                    return float.MaxValue;
                }

                if (_miGetTimeTillNextSeasonChange == null)
                {
                    _miGetTimeTillNextSeasonChange = calendarService.GetType().GetMethod("GetTimeTillNextSeasonChange", Type.EmptyTypes);
                }

                if (_miGetTimeTillNextSeasonChange == null)
                {
                    return float.MaxValue;
                }

                return (float)_miGetTimeTillNextSeasonChange.Invoke(calendarService, null);
            }
            catch (Exception)
            {
                return float.MaxValue;
            }
        }

        public static GameDate GetCurrentGameDate()
        {
            try
            {
                var calendarService = GetCalendarService();
                var prop = calendarService?.GetType().GetProperty("GameDate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return prop?.GetValue(calendarService, null) is GameDate date ? date : default;
            }
            catch (Exception)
            {
                return default;
            }
        }

        public static Season? GetCurrentSeason()
        {
            try
            {
                var calendarService = GetCalendarService();
                var prop = calendarService?.GetType().GetProperty("Season", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return prop?.GetValue(calendarService, null) is Season season ? season : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void PauseGame()
        {
            try
            {
                var timeScaleService = GetTimeScaleService();
                if (timeScaleService == null)
                {
                    return;
                }

                var isPausedMethod = timeScaleService.GetType().GetMethod("IsPaused", Type.EmptyTypes);
                if (isPausedMethod?.Invoke(timeScaleService, null) is bool isPaused && isPaused)
                {
                    return;
                }

                if (_miPause == null)
                {
                    _miPause = timeScaleService.GetType().GetMethod("Pause", new[] { typeof(bool) });
                }

                _miPause?.Invoke(timeScaleService, new object[] { false });
            }
            catch (Exception)
            {
            }
        }

        public static News PublishNews(string content, string description = null, AlertSeverity severity = AlertSeverity.Info)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                var newsService = GetNewsService();
                if (newsService == null)
                {
                    return null;
                }

                var before = new HashSet<News>(GetCurrentNews());
                if (_miPublishNews == null)
                {
                    _miPublishNews = newsService.GetType().GetMethod(
                        "PublishNews",
                        new[] { typeof(string), typeof(string), typeof(AlertSeverity), typeof(UnityEngine.Sprite), typeof(Eremite.IBroadcaster) }
                    );
                }

                _miPublishNews?.Invoke(newsService, new object[] { content, description, severity, null, null });
                return GetCurrentNews().FirstOrDefault(news => news != null && !before.Contains(news) && news.content == content);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void RemoveNews(News news)
        {
            if (news == null)
            {
                return;
            }

            try
            {
                var newsService = GetNewsService();
                if (newsService == null)
                {
                    return;
                }

                if (_miRemoveNews == null)
                {
                    _miRemoveNews = newsService.GetType().GetMethod("RemoveNews", new[] { typeof(News) });
                }

                _miRemoveNews?.Invoke(newsService, new object[] { news });
            }
            catch (Exception)
            {
            }
        }

        public static List<News> GetCurrentNews()
        {
            try
            {
                var newsService = GetNewsService();
                if (newsService == null)
                {
                    return new List<News>();
                }

                _fiCurrentNews ??= newsService.GetType().GetField("currentNews", BindingFlags.Instance | BindingFlags.NonPublic);
                var reactiveProperty = _fiCurrentNews?.GetValue(newsService);
                if (reactiveProperty == null)
                {
                    return new List<News>();
                }

                _piReactiveValue ??= reactiveProperty.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return _piReactiveValue?.GetValue(reactiveProperty, null) as List<News> ?? new List<News>();
            }
            catch (Exception)
            {
                return new List<News>();
            }
        }

        public static string GetBlightPostFuelId()
        {
            try
            {
                return GetSettings()?.blightConfig?.blightPostFuel?.Name ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static HashSet<string> GetAvailableRecipeGoods()
        {
            var goods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var buildingsService = GetBuildingsService();
                if (buildingsService == null)
                {
                    return goods;
                }

                AddRecipeGoodsFromDictionary(buildingsService, "Workshops", ref _piWorkshops, goods);
                AddRecipeGoodsFromDictionary(buildingsService, "BlightPosts", ref _piBlightPosts, goods);

                var settings = GetSettings();
                if (settings != null)
                {
                    AddUnlockedRecipeGoodsFromModels(settings.workshops, goods);
                    AddUnlockedRecipeGoodsFromModels(settings.blightPosts, goods);
                }
            }
            catch (Exception)
            {
            }

            return goods;
        }

        public static List<AvailableWorkshopRecipe> GetAvailableWorkshopRecipes(bool includeDisabledRecipes = true, bool includeUnbuiltUnlockedRecipes = true)
        {
            var recipes = new List<AvailableWorkshopRecipe>();

            try
            {
                var settings = GetSettings();
                if (settings == null)
                {
                    return recipes;
                }

                var constructedModels = new HashSet<BuildingModel>();
                foreach (var workshop in GetRecipeBuildings())
                {
                    if (workshop?.Recipes == null)
                    {
                        continue;
                    }

                    if (workshop.BaseModel != null)
                    {
                        constructedModels.Add(workshop.BaseModel);
                    }

                    foreach (var recipeState in workshop.Recipes)
                    {
                        if (recipeState == null || (!includeDisabledRecipes && !recipeState.active))
                        {
                            continue;
                        }

                        AddAvailableWorkshopRecipe(settings.GetWorkshopRecipe(recipeState.model), workshop.Base, workshop.BaseModel, recipes);
                    }
                }

                if (includeUnbuiltUnlockedRecipes)
                {
                    AddUnlockedWorkshopRecipesFromModels(settings.workshops, recipes, constructedModels);
                    AddUnlockedWorkshopRecipesFromModels(settings.blightPosts, recipes, constructedModels);
                }
            }
            catch (Exception)
            {
            }

            return recipes;
        }

        public static List<BuildingCountInfo> GetUnlockedZeroCountBuildings()
        {
            var result = new List<BuildingCountInfo>();

            try
            {
                var settings = GetSettings();
                if (settings == null)
                {
                    return result;
                }

                var existingCounts = GetPlacedBuildingCountsByModel();
                var seenModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var model in EnumerateBuildingModels(settings))
                {
                    if (model == null ||
                        string.IsNullOrWhiteSpace(model.Name) ||
                        !seenModels.Add(model.Name) ||
                        !model.HasAccessTo() ||
                        !IsBuildingUnlocked(model) ||
                        ShouldExcludeFromZeroBuildingOverview(model))
                    {
                        continue;
                    }

                    existingCounts.TryGetValue(model.Name, out var count);
                    if (count == 0)
                    {
                        result.Add(new BuildingCountInfo(model, count));
                    }
                }

                result.Sort((left, right) => string.Compare(GetBuildingDisplayName(left.Model), GetBuildingDisplayName(right.Model), StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception)
            {
            }

            return result;
        }

        private static void AddUnlockedRecipeGoodsFromModels(IEnumerable<BuildingModel> buildingModels, HashSet<string> goods)
        {
            if (buildingModels == null)
            {
                return;
            }

            foreach (var buildingModel in buildingModels)
            {
                if (buildingModel == null || !buildingModel.HasAccessTo() || !IsBuildingUnlocked(buildingModel))
                {
                    continue;
                }

                AddRecipeGoodsFromModel(buildingModel, goods);
            }
        }

        public static int GetEffectiveWorkshopRecipeOutput(AvailableWorkshopRecipe availableRecipe)
        {
            var recipe = availableRecipe?.Recipe;
            var baseAmount = recipe?.producedGood?.amount ?? 0;
            var goodName = recipe?.producedGood?.Name;
            if (recipe == null || string.IsNullOrWhiteSpace(goodName) || baseAmount <= 0)
            {
                return 0;
            }

            try
            {
                var baseProduction = new Good(goodName, baseAmount);
                var effectsService = GetEffectsService();
                var sourceBuilding = availableRecipe.SourceBuilding;
                if (effectsService != null && sourceBuilding != null)
                {
                    var method = effectsService.GetType().GetMethod("GetProduction", new[] { typeof(Building), typeof(Good), typeof(RecipeModel), typeof(bool) });
                    if (method?.Invoke(effectsService, new object[] { sourceBuilding, baseProduction, recipe, false }) is Good result)
                    {
                        return result.amount;
                    }
                }

                var effects = GetEffectsState();
                var amount = baseAmount + GetSafe(effects?.rawGoodsProductionBonus, goodName);
                var factor = 1f;
                var buildingName = availableRecipe.SourceModel?.Name ?? availableRecipe.SourceBuilding?.ModelName;
                if (!string.IsNullOrWhiteSpace(buildingName))
                {
                    factor += GetSafe(effects?.buildingPercentageProductionBonus, buildingName);
                }

                foreach (var tag in recipe.tags ?? Array.Empty<ModelTag>())
                {
                    factor += GetSafe(effects?.recipesTagsPercentageProductionBonus, tag?.Name);
                }

                return Math.Max(0, (int)Math.Round(amount * factor, MidpointRounding.AwayFromZero));
            }
            catch (Exception)
            {
                return baseAmount;
            }
        }

        private static object GetEffectsService()
        {
            if (_piEffectsService == null)
            {
                _piEffectsService = typeof(GameMB).GetProperty("EffectsService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piEffectsService?.GetValue(null, null);
        }

        private static EffectsState GetEffectsState()
        {
            if (_piStateService == null)
            {
                _piStateService = typeof(GameMB).GetProperty("StateService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            var stateService = _piStateService?.GetValue(null, null);
            return stateService?.GetType().GetProperty("Effects")?.GetValue(stateService, null) as EffectsState;
        }

        private static int GetSafe(Dictionary<string, int> values, string key)
        {
            return !string.IsNullOrWhiteSpace(key) && values != null && values.TryGetValue(key, out var value)
                ? value
                : 0;
        }

        private static float GetSafe(Dictionary<string, float> values, string key)
        {
            return !string.IsNullOrWhiteSpace(key) && values != null && values.TryGetValue(key, out var value)
                ? value
                : 0f;
        }

        private static void AddUnlockedWorkshopRecipesFromModels(IEnumerable<BuildingModel> buildingModels, List<AvailableWorkshopRecipe> recipes, HashSet<BuildingModel> constructedModels)
        {
            if (buildingModels == null)
            {
                return;
            }

            foreach (var buildingModel in buildingModels)
            {
                if (buildingModel == null || !buildingModel.HasAccessTo() || !IsBuildingUnlocked(buildingModel))
                {
                    continue;
                }

                if (constructedModels != null && constructedModels.Contains(buildingModel))
                {
                    continue;
                }

                AddWorkshopRecipesFromModel(buildingModel, recipes);
            }
        }

        private static void AddWorkshopRecipesFromModel(BuildingModel buildingModel, List<AvailableWorkshopRecipe> recipes)
        {
            var settings = GetSettings();
            var recipesService = GetRecipesService();
            if (settings == null || recipesService == null)
            {
                return;
            }

            if (_miGetRecipesForBuilding == null)
            {
                _miGetRecipesForBuilding = recipesService.GetType().GetMethod("GetRecipesFor", new[] { typeof(string) });
            }

            var recipeNames = _miGetRecipesForBuilding?.Invoke(recipesService, new object[] { buildingModel.Name }) as IEnumerable<string>;
            if (recipeNames == null)
            {
                return;
            }

            foreach (var recipeName in recipeNames)
            {
                if (string.IsNullOrWhiteSpace(recipeName))
                {
                    continue;
                }

                AddAvailableWorkshopRecipe(settings.GetWorkshopRecipe(recipeName), null, buildingModel, recipes);
            }
        }

        private static void AddAvailableWorkshopRecipe(WorkshopRecipeModel recipe, List<AvailableWorkshopRecipe> recipes)
        {
            AddAvailableWorkshopRecipe(recipe, null, null, recipes);
        }

        private static void AddAvailableWorkshopRecipe(WorkshopRecipeModel recipe, ProductionBuilding sourceBuilding, BuildingModel sourceModel, List<AvailableWorkshopRecipe> recipes)
        {
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.Name) || recipes == null)
            {
                return;
            }

            if (recipes.Any(r =>
                    string.Equals(r.Recipe?.Name, recipe.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.SourceModel?.Name, sourceModel?.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            recipes.Add(new AvailableWorkshopRecipe(recipe, sourceBuilding, sourceModel));
        }

        private static void AddRecipeGoodsFromModel(BuildingModel buildingModel, HashSet<string> goods)
        {
            var settings = GetSettings();
            var recipesService = GetRecipesService();
            if (settings == null || recipesService == null)
            {
                return;
            }

            if (_miGetRecipesForBuilding == null)
            {
                _miGetRecipesForBuilding = recipesService.GetType().GetMethod("GetRecipesFor", new[] { typeof(string) });
            }

            var recipeNames = _miGetRecipesForBuilding?.Invoke(recipesService, new object[] { buildingModel.Name }) as IEnumerable<string>;
            if (recipeNames == null)
            {
                return;
            }

            foreach (var recipeName in recipeNames)
            {
                if (string.IsNullOrWhiteSpace(recipeName))
                {
                    continue;
                }

                var recipe = settings.GetWorkshopRecipe(recipeName);
                var productName = recipe?.producedGood?.Name;
                if (!string.IsNullOrWhiteSpace(productName))
                {
                    goods.Add(productName);
                }
            }
        }

        public static string GetBuildingDisplayName(BuildingModel model)
        {
            var localized = model?.displayName?.ToString();
            if (!string.IsNullOrWhiteSpace(localized) &&
                localized.IndexOf("Missing key", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return localized;
            }

            return model?.Name ?? "Unknown Building";
        }

        private static IEnumerable<BuildingModel> EnumerateBuildingModels(Settings settings)
        {
            if (settings == null)
            {
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in settings.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!typeof(IEnumerable).IsAssignableFrom(field.FieldType) ||
                    field.FieldType == typeof(string))
                {
                    continue;
                }

                if (!(field.GetValue(settings) is IEnumerable values))
                {
                    continue;
                }

                foreach (var value in values)
                {
                    if (value is BuildingModel model &&
                        !string.IsNullOrWhiteSpace(model.Name) &&
                        seen.Add(model.Name))
                    {
                        yield return model;
                    }
                }
            }
        }

        private static Dictionary<string, int> GetPlacedBuildingCountsByModel()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var buildingsService = GetBuildingsService();
                if (buildingsService == null)
                {
                    return counts;
                }

                var buildingsProperty = buildingsService.GetType().GetProperty("Buildings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var buildings = buildingsProperty?.GetValue(buildingsService, null) as IDictionary;
                if (buildings == null)
                {
                    return counts;
                }

                foreach (DictionaryEntry entry in buildings)
                {
                    if (!(entry.Value is Building building))
                    {
                        continue;
                    }

                    var modelName = building.BuildingModel?.Name ?? building.ModelName;
                    if (string.IsNullOrWhiteSpace(modelName))
                    {
                        continue;
                    }

                    counts[modelName] = counts.TryGetValue(modelName, out var current) ? current + 1 : 1;
                }
            }
            catch (Exception)
            {
            }

            return counts;
        }

        private static bool ShouldExcludeFromZeroBuildingOverview(BuildingModel model)
        {
            if (model == null)
            {
                return true;
            }

            var prefab = model.Prefab;
            if (prefab is Decoration ||
                prefab is Storage ||
                prefab is Hearth ||
                prefab is House ||
                prefab is Hydrant ||
                prefab is Road ||
                prefab is GathererHut ||
                prefab is Camp ||
                prefab is FishingHut ||
                prefab is Extractor)
            {
                return true;
            }

            var name = (model.Name ?? string.Empty) + " " + GetBuildingDisplayName(model);
            return name.IndexOf("Geyser", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Farm Field", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Mine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Small Warehouse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("House", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Road", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Hydrant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Hearth", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsBuildingUnlocked(BuildingModel buildingModel)
        {
            var gameContentService = GetGameContentService();
            if (gameContentService == null)
            {
                return false;
            }

            if (_miIsBuildingUnlocked == null)
            {
                _miIsBuildingUnlocked = gameContentService.GetType().GetMethod("IsUnlocked", new[] { typeof(BuildingModel) });
            }

            var value = _miIsBuildingUnlocked?.Invoke(gameContentService, new object[] { buildingModel });
            return value is bool isUnlocked && isUnlocked;
        }

        public static List<IWorkshop> GetRecipeBuildings()
        {
            var result = new List<IWorkshop>();

            try
            {
                var buildingsService = GetBuildingsService();
                if (buildingsService == null)
                {
                    return result;
                }

                AddWorkshopsFromDictionary(buildingsService, "Workshops", ref _piWorkshops, result);
                AddWorkshopsFromDictionary(buildingsService, "BlightPosts", ref _piBlightPosts, result);
            }
            catch (Exception)
            {
            }

            return result;
        }

        public static List<ProductionBuilding> GetGatheringSourceBuildings()
        {
            var result = new List<ProductionBuilding>();

            try
            {
                var buildingsService = GetBuildingsService();
                if (buildingsService == null)
                {
                    return result;
                }

                AddProductionBuildingsFromDictionary(buildingsService, "GathererHuts", ref _piGathererHuts, result);
                AddProductionBuildingsFromDictionary(buildingsService, "Camps", ref _piCamps, result);
            }
            catch (Exception)
            {
            }

            return result;
        }

        public static List<Mine> GetMineBuildings()
        {
            var result = new List<ProductionBuilding>();

            try
            {
                var buildingsService = GetBuildingsService();
                if (buildingsService == null)
                {
                    return new List<Mine>();
                }

                AddProductionBuildingsFromDictionary(buildingsService, "Mines", ref _piMines, result);
            }
            catch (Exception)
            {
            }

            return result.OfType<Mine>().ToList();
        }

        public static List<Farm> GetFarmBuildings()
        {
            var result = new List<ProductionBuilding>();

            try
            {
                var buildingsService = GetBuildingsService();
                if (buildingsService == null)
                {
                    return new List<Farm>();
                }

                AddProductionBuildingsFromDictionary(buildingsService, "Farms", ref _piFarms, result);
            }
            catch (Exception)
            {
            }

            return result.OfType<Farm>().ToList();
        }

        public static List<RainCatcher> GetRainCatcherBuildings()
        {
            var result = new List<ProductionBuilding>();

            try
            {
                var buildingsService = GetBuildingsService();
                if (buildingsService == null)
                {
                    return new List<RainCatcher>();
                }

                AddProductionBuildingsFromDictionary(buildingsService, "RainCatchers", ref _piRainCatchers, result);
            }
            catch (Exception)
            {
            }

            return result.OfType<RainCatcher>().ToList();
        }

        public static bool CanRainCatcherFillWaterTank(RainCatcher rainCatcher)
        {
            try
            {
                var rainpunkService = GetRainpunkService();
                var water = rainCatcher?.GetCurrentWaterType();
                if (rainpunkService == null || water == null)
                {
                    return false;
                }

                return !rainpunkService.AreTanksFull(water);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static List<Storage> GetStorageBuildings()
        {
            var result = new List<ProductionBuilding>();

            try
            {
                var buildingsService = GetBuildingsService();
                if (buildingsService == null)
                {
                    return new List<Storage>();
                }

                AddProductionBuildingsFromDictionary(buildingsService, "Storages", ref _piStorages, result);
            }
            catch (Exception)
            {
            }

            return result.OfType<Storage>().ToList();
        }

        public static List<Building> GetConstructionBuildings()
        {
            var result = new List<Building>();

            try
            {
                var buildingsService = GetBuildingsService();
                if (buildingsService == null)
                {
                    return result;
                }

                _piConstructions ??= buildingsService.GetType().GetProperty("Constructions", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var entries = _piConstructions?.GetValue(buildingsService, null) as IDictionary;
                if (entries == null)
                {
                    return result;
                }

                foreach (DictionaryEntry entry in entries)
                {
                    if (entry.Value is Building building)
                    {
                        result.Add(building);
                    }
                }
            }
            catch (Exception)
            {
            }

            return result;
        }

        public static List<TimedOrderInfo> GetActiveTimedOrders()
        {
            var result = new List<TimedOrderInfo>();

            try
            {
                var ordersService = GetOrdersService();
                var settings = GetSettings();
                if (ordersService?.Orders == null || settings == null)
                {
                    return result;
                }

                foreach (var order in ordersService.Orders)
                {
                    if (order == null ||
                        !order.started ||
                        !order.picked ||
                        order.completed ||
                        string.IsNullOrWhiteSpace(order.model))
                    {
                        continue;
                    }

                    var model = settings.GetOrder(order.model);
                    if (model == null || !model.canBeFailed || order.timeLeft <= 0f)
                    {
                        continue;
                    }

                    result.Add(new TimedOrderInfo(order.Id, GetOrderDisplayName(model), order.timeLeft));
                }
            }
            catch (Exception)
            {
            }

            return result;
        }

        private static string GetOrderDisplayName(OrderModel model)
        {
            var displayName = model?.displayName?.Text;
            return !string.IsNullOrWhiteSpace(displayName) ? displayName : model?.Name ?? "Timed Order";
        }

        public static bool CanConstructionUseBuilders(Building building)
        {
            if (building?.BuildingState == null || building.BuildingModel == null)
            {
                return false;
            }

            if (building.BuildingState.isSleeping)
            {
                return false;
            }

            return HasEnoughConstructionGoods(building);
        }

        private static bool HasEnoughConstructionGoods(Building building)
        {
            try
            {
                var constructionService = GetConstructionService();
                var deliveredGoods = building?.BuildingState?.deliveredGoods;
                if (constructionService == null || building?.BuildingModel == null || deliveredGoods == null)
                {
                    return false;
                }

                var cost = constructionService.GetConstructionCostFor(building.BuildingModel);
                if (cost == null)
                {
                    return true;
                }

                foreach (var good in cost)
                {
                    if (string.IsNullOrWhiteSpace(good.name))
                    {
                        continue;
                    }

                    var unreservedRemaining = deliveredGoods.GetUnreservedSpaceLeft(good.name);
                    if (unreservedRemaining > 0 && GetStoredAmount(null, good.name) < unreservedRemaining)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool HasFarmFieldWork(Farm farm)
        {
            if (farm == null || !farm.AnyRecipeActive())
            {
                return false;
            }

            try
            {
                var season = GetCurrentSeason();
                if (season == null)
                {
                    return false;
                }

                var fieldsInRange = farm.CountAllReaveleadFieldsInRange();
                if (fieldsInRange <= 0)
                {
                    return false;
                }

                if (season == Season.Drizzle && farm.CountSownFieldsInRange() < fieldsInRange)
                {
                    return HasOpenFarmfieldMatching(farm, field => !field.IsSeeded());
                }

                if (season == Season.Clearance && farm.CountSownFieldsInRange() > 0)
                {
                    return HasOpenFarmfieldMatching(farm, field => field.IsSeeded());
                }

                if (season == Season.Storm && farm.CountPlownFieldsInRange() < fieldsInRange)
                {
                    return HasOpenFarmfieldMatching(farm, field => !field.IsPlowed());
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        private static bool HasOpenFarmfieldMatching(Farm farm, Func<Farmfield, bool> check)
        {
            foreach (var farmfield in GetFarmfields())
            {
                if (farmfield == null ||
                    farmfield.state == null ||
                    !farmfield.IsFinished() ||
                    farmfield.state.currentWorker > 0 ||
                    !farm.AreaRect.Contains(new Vector2(farmfield.Center.x, farmfield.Center.z)))
                {
                    continue;
                }

                if (check(farmfield))
                {
                    return true;
                }
            }

            return false;
        }

        internal static List<TrackedItemLocation> GetTrackedItemLocations(string goodId)
        {
            var results = new Dictionary<string, TrackedItemLocation>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(goodId))
            {
                return results.Values.ToList();
            }

            try
            {
                AddBuildingStorageLocations(goodId, results);
                AddIngredientStorageLocations(goodId, results);
                AddRelicLocations(goodId, results);
                AddCarriedGoodDestinations(goodId, results);
                AddNaturalResourceLocations(goodId, results);
                AddDepositLocations(goodId, results);
                AddLakeLocations(goodId, results);
                AddOreLocations(goodId, results);
            }
            catch (Exception)
            {
            }

            return results.Values.ToList();
        }

        private static object GetWorkshopsService()
        {
            if (_piWorkshopsService == null)
            {
                _piWorkshopsService = typeof(GameMB).GetProperty("WorkshopsService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piWorkshopsService?.GetValue(null, null);
        }

        private static object GetGameContentService()
        {
            if (_piGameContentService == null)
            {
                _piGameContentService = typeof(GameMB).GetProperty("GameContentService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piGameContentService?.GetValue(null, null);
        }

        private static object GetRecipesService()
        {
            if (_piRecipesService == null)
            {
                _piRecipesService = typeof(GameMB).GetProperty("RecipesService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piRecipesService?.GetValue(null, null);
        }

        private static IConstructionService GetConstructionService()
        {
            if (_piConstructionService == null)
            {
                _piConstructionService = typeof(GameMB).GetProperty("ConstructionService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piConstructionService?.GetValue(null, null) as IConstructionService;
        }

        private static IOrdersService GetOrdersService()
        {
            if (_piOrdersService == null)
            {
                _piOrdersService = typeof(GameMB).GetProperty("OrdersService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piOrdersService?.GetValue(null, null) as IOrdersService;
        }

        private static object GetStorageService()
        {
            if (_piStorageService == null)
            {
                _piStorageService = typeof(GameMB).GetProperty("StorageService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piStorageService?.GetValue(null, null);
        }

        private static IRainpunkService GetRainpunkService()
        {
            if (_piRainpunkService == null)
            {
                _piRainpunkService = typeof(GameMB).GetProperty("RainpunkService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piRainpunkService?.GetValue(null, null) as IRainpunkService;
        }

        private static object GetBuildingsService()
        {
            if (_piBuildingsService == null)
            {
                _piBuildingsService = typeof(GameMB).GetProperty("BuildingsService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piBuildingsService?.GetValue(null, null);
        }

        private static object GetVillagersService()
        {
            if (_piVillagersService == null)
            {
                _piVillagersService = typeof(GameMB).GetProperty("VillagersService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piVillagersService?.GetValue(null, null);
        }

        private static object GetBlightService()
        {
            if (_piBlightService == null)
            {
                _piBlightService = typeof(GameMB).GetProperty("BlightService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piBlightService?.GetValue(null, null);
        }

        private static object GetRainpunkService()
        {
            if (_piRainpunkService == null)
            {
                _piRainpunkService = typeof(GameMB).GetProperty("RainpunkService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piRainpunkService?.GetValue(null, null);
        }

        private static object GetCalendarService()
        {
            if (_piCalendarService == null)
            {
                _piCalendarService = typeof(GameMB).GetProperty("CalendarService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piCalendarService?.GetValue(null, null);
        }

        private static object GetTimeScaleService()
        {
            if (_piTimeScaleService == null)
            {
                _piTimeScaleService = typeof(GameMB).GetProperty("TimeScaleService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piTimeScaleService?.GetValue(null, null);
        }

        private static object GetNewsService()
        {
            if (_piNewsService == null)
            {
                _piNewsService = typeof(GameMB).GetProperty("NewsService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piNewsService?.GetValue(null, null);
        }

        private static object GetResourcesService()
        {
            if (_piResourcesService == null)
            {
                _piResourcesService = typeof(GameMB).GetProperty("ResourcesService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piResourcesService?.GetValue(null, null);
        }

        private static object GetDepositsService()
        {
            if (_piDepositsService == null)
            {
                _piDepositsService = typeof(GameMB).GetProperty("DepositsService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piDepositsService?.GetValue(null, null);
        }

        private static object GetLakesService()
        {
            if (_piLakesService == null)
            {
                _piLakesService = typeof(GameMB).GetProperty("LakesService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piLakesService?.GetValue(null, null);
        }

        private static object GetOreService()
        {
            if (_piOreService == null)
            {
                _piOreService = typeof(GameMB).GetProperty("OreService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piOreService?.GetValue(null, null);
        }

        private static object GetGladesService()
        {
            if (_piGladesService == null)
            {
                _piGladesService = typeof(GameMB).GetProperty("GladesService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            return _piGladesService?.GetValue(null, null);
        }

        private static void AddRecipeGoodsFromDictionary(object buildingsService, string propertyName, ref PropertyInfo propertyInfo, HashSet<string> goods)
        {
            propertyInfo ??= buildingsService.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var entries = propertyInfo?.GetValue(buildingsService, null) as IDictionary;
            if (entries == null)
            {
                return;
            }

            foreach (DictionaryEntry entry in entries)
            {
                if (entry.Value is not IWorkshop workshop)
                {
                    continue;
                }

                foreach (var recipe in workshop.Recipes.Where(r => r != null && !string.IsNullOrWhiteSpace(r.productName)))
                {
                    goods.Add(recipe.productName);
                }
            }
        }

        private static void AddWorkshopsFromDictionary(object buildingsService, string propertyName, ref PropertyInfo propertyInfo, List<IWorkshop> workshops)
        {
            propertyInfo ??= buildingsService.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var entries = propertyInfo?.GetValue(buildingsService, null) as IDictionary;
            if (entries == null)
            {
                return;
            }

            foreach (DictionaryEntry entry in entries)
            {
                if (entry.Value is IWorkshop workshop)
                {
                    workshops.Add(workshop);
                }
            }
        }

        private static void RefreshLimitsFromDictionary(object buildingsService, string propertyName, ref PropertyInfo propertyInfo)
        {
            propertyInfo ??= buildingsService.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var entries = propertyInfo?.GetValue(buildingsService, null) as IDictionary;
            if (entries == null)
            {
                return;
            }

            foreach (DictionaryEntry entry in entries)
            {
                var refreshMethod = entry.Value?.GetType().GetMethod("RefreshLimits", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                refreshMethod?.Invoke(entry.Value, null);
            }
        }

        private static void AddProductionBuildingsFromDictionary(object buildingsService, string propertyName, ref PropertyInfo propertyInfo, List<ProductionBuilding> buildings)
        {
            propertyInfo ??= buildingsService.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var entries = propertyInfo?.GetValue(buildingsService, null) as IDictionary;
            if (entries == null)
            {
                return;
            }

            foreach (DictionaryEntry entry in entries)
            {
                if (entry.Value is ProductionBuilding building)
                {
                    buildings.Add(building);
                }
            }
        }

        private static IEnumerable<Farmfield> GetFarmfields()
        {
            var buildingsService = GetBuildingsService();
            if (buildingsService == null)
            {
                yield break;
            }

            _piFarmfields ??= buildingsService.GetType().GetProperty("Farmfields", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var entries = _piFarmfields?.GetValue(buildingsService, null) as IDictionary;
            if (entries == null)
            {
                yield break;
            }

            foreach (DictionaryEntry entry in entries)
            {
                if (entry.Value is Farmfield farmfield)
                {
                    yield return farmfield;
                }
            }
        }

        private static void AddBuildingStorageLocations(string goodId, IDictionary<string, TrackedItemLocation> results)
        {
            var storageService = GetStorageService();
            if (storageService == null)
            {
                return;
            }

            _piLocalStorages ??= storageService.GetType().GetProperty("LocalStorages", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var storages = _piLocalStorages?.GetValue(storageService, null) as IDictionary;
            if (storages == null)
            {
                return;
            }

            foreach (DictionaryEntry entry in storages)
            {
                if (entry.Value is not BuildingStorage storage)
                {
                    continue;
                }

                var owner = storage.Owner;
                var amount = storage.GetAmount(goodId);
                if (owner == null || owner is Storage || amount <= 0)
                {
                    continue;
                }

                AddLocation(results, $"building:{owner.Id}", owner, owner.Center, amount);
            }
        }

        private static void AddIngredientStorageLocations(string goodId, IDictionary<string, TrackedItemLocation> results)
        {
            var storageService = GetStorageService();
            if (storageService == null)
            {
                return;
            }

            _piIngredientsStorages ??= storageService.GetType().GetProperty("IngredientsStorages", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var storages = _piIngredientsStorages?.GetValue(storageService, null) as IDictionary;
            if (storages == null)
            {
                return;
            }

            foreach (DictionaryEntry entry in storages)
            {
                if (entry.Value is not BuildingIngredientsStorage storage)
                {
                    continue;
                }

                var owner = storage.owner;
                var amount = storage.goods?.GetAmount(goodId) ?? 0;
                if (owner == null || owner is Storage || storage.goods == null || amount <= 0)
                {
                    continue;
                }

                AddLocation(results, $"building:{owner.Id}", owner, owner.Center, amount);
            }
        }

        private static void AddRelicLocations(string goodId, IDictionary<string, TrackedItemLocation> results)
        {
            var buildingsService = GetBuildingsService();
            if (buildingsService == null)
            {
                return;
            }

            var relicsProperty = buildingsService.GetType().GetProperty("Relics", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var relics = relicsProperty?.GetValue(buildingsService, null) as IDictionary;
            if (relics == null)
            {
                return;
            }

            foreach (DictionaryEntry entry in relics)
            {
                if (entry.Value is not Relic relic || relic.state == null)
                {
                    continue;
                }

                var requiredAmount = relic.state.relicGoods?.GetAmount(goodId) ?? 0;
                var rewardAmount = relic.state.rewards?.GetAmount(goodId) ?? 0;
                if (requiredAmount <= 0 && rewardAmount <= 0)
                {
                    continue;
                }

                AddLocation(results, $"building:{relic.Id}", relic, relic.Center, requiredAmount + rewardAmount);
            }
        }

        private static void AddCarriedGoodDestinations(string goodId, IDictionary<string, TrackedItemLocation> results)
        {
            var villagersService = GetVillagersService();
            var buildingsService = GetBuildingsService();
            if (villagersService == null || buildingsService == null)
            {
                return;
            }

            var villagersProperty = villagersService.GetType().GetProperty("Villagers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var villagers = villagersProperty?.GetValue(villagersService, null) as IDictionary;
            var buildingsProperty = buildingsService.GetType().GetProperty("Buildings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var buildings = buildingsProperty?.GetValue(buildingsService, null) as IDictionary;
            if (villagers == null || buildings == null)
            {
                return;
            }

            foreach (DictionaryEntry entry in villagers)
            {
                if (entry.Value is not Villager villager || villager.state == null || !villager.state.isAlive)
                {
                    continue;
                }

                var amount = 0;
                var carried = villager.state.carriedGood;
                if (string.Equals(carried.name, goodId, StringComparison.OrdinalIgnoreCase) && carried.amount > 0)
                {
                    amount = carried.amount;
                }
                else
                {
                    var requested = villager.state.servedGoodRequest.good;
                    if (string.Equals(requested.name, goodId, StringComparison.OrdinalIgnoreCase) && requested.amount > 0)
                    {
                        amount = requested.amount;
                    }
                }

                if (amount <= 0)
                {
                    continue;
                }

                var toId = villager.state.servedGoodRequest.toId;
                if (toId <= 0 || !buildings.Contains(toId) || buildings[toId] is not Building building || building is Storage)
                {
                    continue;
                }

                AddLocation(results, $"building:{building.Id}", building, building.Center, amount);
            }
        }

        private static void AddNaturalResourceLocations(string goodId, IDictionary<string, TrackedItemLocation> results)
        {
            return;
        }

        private static void AddDepositLocations(string goodId, IDictionary<string, TrackedItemLocation> results)
        {
            var depositsService = GetDepositsService();
            if (depositsService == null)
            {
                return;
            }

            var property = depositsService.GetType().GetProperty("AvailableDeposits", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(depositsService, null) is not IDictionary deposits || !deposits.Contains(goodId))
            {
                return;
            }

            if (deposits[goodId] is not IEnumerable<ResourceDeposit> availableDeposits)
            {
                return;
            }

            foreach (var deposit in availableDeposits)
            {
                if (deposit == null || !deposit.State.isAvailable || !deposit.State.isActive || deposit.State.chargesLeft <= 0)
                {
                    continue;
                }

                AddLocation(results, $"deposit:{deposit.Field.x}:{deposit.Field.y}", deposit, deposit.Center, 0);
            }
        }

        private static void AddLakeLocations(string goodId, IDictionary<string, TrackedItemLocation> results)
        {
            var lakesService = GetLakesService();
            if (lakesService == null)
            {
                return;
            }

            var property = lakesService.GetType().GetProperty("AvailableLakes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(lakesService, null) is not IDictionary lakes || !lakes.Contains(goodId))
            {
                return;
            }

            if (lakes[goodId] is not IEnumerable<Lake> availableLakes)
            {
                return;
            }

            foreach (var lake in availableLakes)
            {
                if (lake == null || !lake.State.isAvailable || lake.State.chargesLeft <= 0)
                {
                    continue;
                }

                AddLocation(results, $"lake:{lake.Field.x}:{lake.Field.y}", lake, lake.Center, 0);
            }
        }

        private static void AddOreLocations(string goodId, IDictionary<string, TrackedItemLocation> results)
        {
            var oreService = GetOreService();
            if (oreService == null)
            {
                return;
            }

            var property = oreService.GetType().GetProperty("Ore", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(oreService, null) is not IDictionary ores)
            {
                return;
            }

            foreach (DictionaryEntry entry in ores)
            {
                if (entry.Value is not Ore ore || !string.Equals(ore.GetRefGood(), goodId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if ((ore.State.mainCharges?.Sum() ?? 0) + (ore.State.extraCharges?.Sum() ?? 0) <= 0)
                {
                    continue;
                }

                if (IsFieldInGlade(ore.Field))
                {
                    continue;
                }

                AddLocation(results, $"ore:{ore.Field.x}:{ore.Field.y}", ore, ore.Center, 0);
            }
        }

        private static bool IsFieldInGlade(Vector2Int field)
        {
            try
            {
                var gladesService = GetGladesService();
                if (gladesService == null)
                {
                    return false;
                }

                _miIsGlade ??= gladesService.GetType().GetMethod("IsGlade", new[] { typeof(Vector2Int) });
                var value = _miIsGlade?.Invoke(gladesService, new object[] { field });
                return value is bool isGlade && isGlade;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void AddLocation(IDictionary<string, TrackedItemLocation> results, string key, UnityEngine.Object source, Vector3 fallbackPosition, int amount)
        {
            if (string.IsNullOrWhiteSpace(key) || source == null)
            {
                return;
            }

            if (results.TryGetValue(key, out var existing) && existing != null)
            {
                existing.Source = source;
                existing.FallbackPosition = fallbackPosition;
                existing.Amount += Math.Max(0, amount);
                return;
            }

            results[key] = new TrackedItemLocation
            {
                Key = key,
                Source = source,
                FallbackPosition = fallbackPosition,
                Amount = Math.Max(0, amount)
            };
        }
    }
}
