using Eremite;
using Eremite.Buildings;
using Eremite.Model;
using StockAlert.Infrastructure.Plugin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace StockAlert.Game
{
    internal static class RainwaterProductionStopper
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly string[] BuildingCollections = { "Workshops", "BlightPosts" };
        private static readonly string[] StorageCollections = { "IngredientsStorages", "LocalStorages" };
        private static readonly string[] StorageMemberNames =
        {
            "Storage",
            "storage",
            "LocalStorage",
            "localStorage",
            "IngredientsStorage",
            "ingredientsStorage",
            "InputStorage",
            "inputStorage",
            "WaterStorage",
            "waterStorage",
            "RainwaterStorage",
            "rainwaterStorage"
        };

        private static readonly string[] BuildingLinkMembers =
        {
            "Building",
            "building",
            "Owner",
            "owner",
            "Parent",
            "parent",
            "ProductionBuilding",
            "productionBuilding",
            "SourceBuilding",
            "sourceBuilding"
        };

        private static readonly string[] IdentityMembers =
        {
            "Id",
            "ID",
            "id",
            "Guid",
            "GUID",
            "guid",
            "Uid",
            "UID",
            "uid",
            "UniqueId",
            "uniqueId",
            "EntityId",
            "entityId"
        };

        private static readonly string[] RecipeEnabledMembers =
        {
            "IsEnabled",
            "Enabled",
            "isEnabled",
            "enabled",
            "IsActive",
            "Active",
            "isActive",
            "active",
            "CanProduce",
            "canProduce"
        };

        private static readonly string[] RecipePausedMembers =
        {
            "IsPaused",
            "Paused",
            "isPaused",
            "paused"
        };

        private static readonly string[] SetEnabledMethods =
        {
            "SetEnabled",
            "SetIsEnabled",
            "SetActive",
            "SetIsActive",
            "SetProductionEnabled",
            "SetCanProduce"
        };

        private static readonly string[] DisableMethods =
        {
            "Disable",
            "Deactivate",
            "StopProduction"
        };

        private static readonly string[] KnownRainwaterGoodIds =
        {
            "Rainwater",
            "Rain Water",
            "Rain_Water",
            "Drizzle Water",
            "DrizzleWater",
            "Drizzle_Water",
            "WaterDrizzle",
            "Water_Drizzle",
            "Clearance Water",
            "ClearanceWater",
            "Clearance_Water",
            "WaterClearance",
            "Water_Clearance",
            "Storm Water",
            "StormWater",
            "Storm_Water",
            "WaterStorm",
            "Water_Storm"
        };

        private static HashSet<string> _rainwaterGoodIds;

        public static bool Apply()
        {
            var changed = false;

            try
            {
                foreach (var workshop in GetRecipeWorkshops())
                {
                    var building = workshop?.Base;
                    if (building == null || workshop.Recipes == null)
                    {
                        continue;
                    }

                    if (!TryGetRainwaterState(workshop, building, out var usesRainwater, out var hasRainwater) ||
                        !usesRainwater ||
                        hasRainwater)
                    {
                        continue;
                    }

                    var stoppedAny = false;
                    foreach (var recipeState in workshop.Recipes)
                    {
                        if (!TryIsRecipeEnabled(recipeState, out var isEnabled) || !isEnabled)
                        {
                            continue;
                        }

                        if (TrySetRecipeEnabled(recipeState, false))
                        {
                            changed = true;
                            stoppedAny = true;
                        }
                    }

                    if (stoppedAny)
                    {
                        Plugin.Log($"Auto-stopped production in {GetBuildingLabel(workshop, building)}: building rainwater is empty.");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log("RainwaterProductionStopper failed: " + ex.Message);
            }

            return changed;
        }

        private static IEnumerable<IWorkshop> GetRecipeWorkshops()
        {
            var buildingsService = GetGameMbService("BuildingsService");
            if (buildingsService == null)
            {
                yield break;
            }

            var seen = new HashSet<int>();
            foreach (var collectionName in BuildingCollections)
            {
                if (!(GetMemberValue(buildingsService, collectionName) is IDictionary buildings))
                {
                    continue;
                }

                foreach (DictionaryEntry entry in buildings)
                {
                    if (entry.Value is IWorkshop workshop)
                    {
                        var id = RuntimeHelpers.GetHashCode(workshop);
                        if (seen.Add(id))
                        {
                            yield return workshop;
                        }
                    }
                }
            }
        }

        private static bool TryGetRainwaterState(IWorkshop workshop, ProductionBuilding building, out bool usesRainwater, out bool hasRainwater)
        {
            usesRainwater = false;
            hasRainwater = false;

            foreach (var metadata in GetBuildingMetadataObjects(workshop, building))
            {
                usesRainwater |= IsRainwaterMarked(metadata);
            }

            var rainwaterIds = GetRainwaterGoodIds();
            foreach (var candidate in GetCandidateStorageObjects(building))
            {
                if (candidate == null)
                {
                    continue;
                }

                usesRainwater |= IsRainwaterMarked(candidate);
                var amount = GetRainwaterAmount(candidate, rainwaterIds, out var storageMentionsRainwater);

                if (storageMentionsRainwater)
                {
                    usesRainwater = true;
                }

                if (amount > 0)
                {
                    usesRainwater = true;
                    hasRainwater = true;
                }
            }

            return usesRainwater;
        }

        private static IEnumerable<object> GetBuildingMetadataObjects(IWorkshop workshop, ProductionBuilding building)
        {
            yield return workshop;
            yield return workshop.BaseModel;
            yield return building;
            yield return building.BuildingModel;
        }

        private static IEnumerable<object> GetCandidateStorageObjects(ProductionBuilding building)
        {
            var candidates = new List<object>();
            var seen = new HashSet<int>();

            AddCandidate(building, candidates, seen);

            foreach (var memberName in StorageMemberNames)
            {
                AddCandidate(GetMemberValue(building, memberName), candidates, seen);
            }

            var storageService = GetGameMbService("StorageService");
            if (storageService != null)
            {
                foreach (var collectionName in StorageCollections)
                {
                    if (!(GetMemberValue(storageService, collectionName) is IDictionary storages))
                    {
                        continue;
                    }

                    foreach (DictionaryEntry entry in storages)
                    {
                        if (MatchesBuilding(entry.Key, building) || MatchesBuilding(entry.Value, building))
                        {
                            AddCandidate(entry.Value, candidates, seen);
                        }
                    }
                }
            }

            return candidates;
        }

        private static bool AddCandidate(object candidate, ICollection<object> candidates, ISet<int> seen)
        {
            if (candidate == null)
            {
                return false;
            }

            var id = RuntimeHelpers.GetHashCode(candidate);
            if (!seen.Add(id))
            {
                return false;
            }

            candidates.Add(candidate);
            return true;
        }

        private static int GetRainwaterAmount(object storageCandidate, IEnumerable<string> rainwaterIds, out bool storageMentionsRainwater)
        {
            var total = 0;
            storageMentionsRainwater = false;

            foreach (var container in GetGoodsContainers(storageCandidate))
            {
                if (container == null)
                {
                    continue;
                }

                storageMentionsRainwater |= IsRainwaterMarked(container);
                storageMentionsRainwater |= ContainsRainwaterKey(container);

                foreach (var goodId in rainwaterIds)
                {
                    var amount = TryGetAmount(container, goodId);
                    if (amount.HasValue)
                    {
                        total += amount.Value;
                    }
                }
            }

            return total;
        }

        private static IEnumerable<object> GetGoodsContainers(object storageCandidate)
        {
            yield return storageCandidate;

            foreach (var memberName in new[] { "goods", "Goods", "storage", "Storage" })
            {
                var value = GetMemberValue(storageCandidate, memberName);
                if (value != null)
                {
                    yield return value;
                }
            }
        }

        private static int? TryGetAmount(object container, string goodId)
        {
            if (container == null || string.IsNullOrWhiteSpace(goodId))
            {
                return null;
            }

            try
            {
                var method = container.GetType().GetMethod(
                    "GetAmount",
                    InstanceFlags,
                    null,
                    new[] { typeof(string) },
                    null
                );

                if (method == null)
                {
                    return null;
                }

                var result = method.Invoke(container, new object[] { goodId });
                return result == null ? 0 : Convert.ToInt32(result);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool ContainsRainwaterKey(object container)
        {
            if (container is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (IsRainwaterMarked(entry.Key) || IsRainwaterMarked(entry.Value))
                    {
                        return true;
                    }
                }
            }

            var type = container?.GetType();
            if (type == null)
            {
                return false;
            }

            foreach (var member in type.GetMembers(InstanceFlags))
            {
                if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
                {
                    continue;
                }

                if (!ContainsText(member.Name, "goods") && !ContainsText(member.Name, "amount") && !ContainsText(member.Name, "storage"))
                {
                    continue;
                }

                var value = GetMemberValue(container, member);
                if (!(value is IDictionary nestedDictionary))
                {
                    continue;
                }

                foreach (DictionaryEntry entry in nestedDictionary)
                {
                    if (IsRainwaterMarked(entry.Key) || IsRainwaterMarked(entry.Value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryIsRecipeEnabled(object recipeState, out bool enabled)
        {
            enabled = false;
            if (recipeState == null)
            {
                return false;
            }

            foreach (var memberName in RecipeEnabledMembers)
            {
                if (TryGetBoolMember(recipeState, memberName, out enabled))
                {
                    return true;
                }
            }

            foreach (var memberName in RecipePausedMembers)
            {
                if (TryGetBoolMember(recipeState, memberName, out var paused))
                {
                    enabled = !paused;
                    return true;
                }
            }

            return false;
        }

        private static bool TrySetRecipeEnabled(object recipeState, bool enabled)
        {
            if (recipeState == null)
            {
                return false;
            }

            foreach (var memberName in RecipeEnabledMembers)
            {
                if (TrySetBoolMember(recipeState, memberName, enabled))
                {
                    return true;
                }
            }

            foreach (var memberName in RecipePausedMembers)
            {
                if (TrySetBoolMember(recipeState, memberName, !enabled))
                {
                    return true;
                }
            }

            foreach (var methodName in SetEnabledMethods)
            {
                if (TryInvokeBoolMethod(recipeState, methodName, enabled))
                {
                    return true;
                }
            }

            if (!enabled)
            {
                foreach (var methodName in DisableMethods)
                {
                    if (TryInvokeParameterlessMethod(recipeState, methodName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetBoolMember(object instance, string memberName, out bool value)
        {
            value = false;

            try
            {
                var type = instance.GetType();
                var property = type.GetProperty(memberName, InstanceFlags | BindingFlags.IgnoreCase);
                if (property != null && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
                {
                    value = (bool)property.GetValue(instance, null);
                    return true;
                }

                var field = type.GetField(memberName, InstanceFlags | BindingFlags.IgnoreCase);
                if (field != null && field.FieldType == typeof(bool))
                {
                    value = (bool)field.GetValue(instance);
                    return true;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        private static bool TrySetBoolMember(object instance, string memberName, bool value)
        {
            try
            {
                var type = instance.GetType();
                var property = type.GetProperty(memberName, InstanceFlags | BindingFlags.IgnoreCase);
                if (property != null &&
                    property.PropertyType == typeof(bool) &&
                    property.CanWrite &&
                    property.GetIndexParameters().Length == 0)
                {
                    property.SetValue(instance, value, null);
                    return true;
                }

                var field = type.GetField(memberName, InstanceFlags | BindingFlags.IgnoreCase);
                if (field != null && field.FieldType == typeof(bool) && !field.IsInitOnly)
                {
                    field.SetValue(instance, value);
                    return true;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        private static bool TryInvokeBoolMethod(object instance, string methodName, bool value)
        {
            try
            {
                var method = instance.GetType().GetMethod(
                    methodName,
                    InstanceFlags | BindingFlags.IgnoreCase,
                    null,
                    new[] { typeof(bool) },
                    null
                );

                if (method == null)
                {
                    return false;
                }

                method.Invoke(instance, new object[] { value });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryInvokeParameterlessMethod(object instance, string methodName)
        {
            try
            {
                var method = instance.GetType().GetMethod(
                    methodName,
                    InstanceFlags | BindingFlags.IgnoreCase,
                    null,
                    Type.EmptyTypes,
                    null
                );

                if (method == null)
                {
                    return false;
                }

                method.Invoke(instance, null);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static HashSet<string> GetRainwaterGoodIds()
        {
            if (_rainwaterGoodIds != null)
            {
                return _rainwaterGoodIds;
            }

            var result = new HashSet<string>(KnownRainwaterGoodIds, StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var good in EnumerateGoodModels(GameAPI.GetSettings()))
                {
                    if (IsRainwaterMarked(good) && !string.IsNullOrWhiteSpace(good.Name))
                    {
                        result.Add(good.Name);
                    }
                }
            }
            catch (Exception)
            {
            }

            _rainwaterGoodIds = result;
            return _rainwaterGoodIds;
        }

        private static IEnumerable<GoodModel> EnumerateGoodModels(object settings)
        {
            if (settings == null)
            {
                yield break;
            }

            foreach (var member in settings.GetType().GetMembers(InstanceFlags))
            {
                if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
                {
                    continue;
                }

                var value = GetMemberValue(settings, member);
                if (value == null || value is string)
                {
                    continue;
                }

                if (value is GoodModel directGood)
                {
                    yield return directGood;
                    continue;
                }

                if (value is IDictionary dictionary)
                {
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        if (entry.Value is GoodModel good)
                        {
                            yield return good;
                        }
                    }

                    continue;
                }

                if (value is IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item is GoodModel good)
                        {
                            yield return good;
                        }
                    }
                }
            }
        }

        private static bool IsRainwaterMarked(object instance)
        {
            if (instance == null)
            {
                return false;
            }

            if (instance is string text)
            {
                return IsRainwaterText(text);
            }

            if (instance is GoodModel good)
            {
                return IsRainwaterText(good.Name) || IsRainwaterText(GetMemberValue(good, "displayName")?.ToString());
            }

            var type = instance.GetType();
            if (IsRainwaterText(type.Name))
            {
                return true;
            }

            foreach (var memberName in new[] { "Name", "name", "Id", "id", "DisplayName", "displayName", "Label", "label" })
            {
                if (IsRainwaterText(GetMemberValue(instance, memberName)?.ToString()))
                {
                    return true;
                }
            }

            foreach (var member in type.GetMembers(InstanceFlags))
            {
                if (IsRainwaterText(member.Name))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRainwaterText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var normalized = new string(text
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());

            if (normalized.Contains("rainwater") ||
                normalized.Contains("rainpunk") ||
                normalized.Contains("rainengine"))
            {
                return true;
            }

            return normalized.Contains("water") &&
                   (normalized.Contains("rain") ||
                    normalized.Contains("drizzle") ||
                    normalized.Contains("clearance") ||
                    normalized.Contains("storm"));
        }

        private static bool MatchesBuilding(object candidate, ProductionBuilding building)
        {
            if (candidate == null || building == null)
            {
                return false;
            }

            if (ReferenceEquals(candidate, building))
            {
                return true;
            }

            if (candidate is ProductionBuilding candidateBuilding)
            {
                return ReferenceEquals(candidateBuilding, building);
            }

            if (HasMatchingIdentity(candidate, building))
            {
                return true;
            }

            foreach (var memberName in BuildingLinkMembers)
            {
                var linked = GetMemberValue(candidate, memberName);
                if (ReferenceEquals(linked, building) || HasMatchingIdentity(linked, building))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasMatchingIdentity(object candidate, object building)
        {
            if (candidate == null || building == null)
            {
                return false;
            }

            var candidateIdentities = GetIdentityValues(candidate).ToArray();
            if (candidateIdentities.Length == 0)
            {
                return false;
            }

            var buildingIdentities = new HashSet<string>(GetIdentityValues(building), StringComparer.OrdinalIgnoreCase);
            return candidateIdentities.Any(buildingIdentities.Contains);
        }

        private static IEnumerable<string> GetIdentityValues(object instance)
        {
            if (instance == null)
            {
                yield break;
            }

            if (instance is string text)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return text;
                }

                yield break;
            }

            foreach (var memberName in IdentityMembers)
            {
                var value = GetMemberValue(instance, memberName);
                if (value != null)
                {
                    var textValue = value.ToString();
                    if (!string.IsNullOrWhiteSpace(textValue))
                    {
                        yield return textValue;
                    }
                }
            }
        }

        private static object GetGameMbService(string propertyName)
        {
            try
            {
                return typeof(GameMB)
                    .GetProperty(propertyName, StaticFlags | BindingFlags.IgnoreCase)
                    ?.GetValue(null, null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            try
            {
                var type = instance.GetType();
                var property = type.GetProperty(memberName, InstanceFlags | BindingFlags.IgnoreCase);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    return property.GetValue(instance, null);
                }

                var field = type.GetField(memberName, InstanceFlags | BindingFlags.IgnoreCase);
                return field?.GetValue(instance);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static object GetMemberValue(object instance, MemberInfo member)
        {
            if (instance == null || member == null)
            {
                return null;
            }

            try
            {
                if (member is PropertyInfo property && property.GetIndexParameters().Length == 0)
                {
                    return property.GetValue(instance, null);
                }

                if (member is FieldInfo field)
                {
                    return field.GetValue(instance);
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static string GetBuildingLabel(IWorkshop workshop, ProductionBuilding building)
        {
            var model = workshop?.BaseModel ?? building?.BuildingModel;
            if (model != null && !string.IsNullOrWhiteSpace(model.Name))
            {
                return model.Name;
            }

            return building?.name ?? "unknown building";
        }

        private static bool ContainsText(string text, string fragment)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   !string.IsNullOrWhiteSpace(fragment) &&
                   text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
