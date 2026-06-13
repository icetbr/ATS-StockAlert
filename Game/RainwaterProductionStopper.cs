using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Eremite.Buildings;
using StockAlert.Config;
using StockAlert.Infrastructure.Plugin;

namespace StockAlert.Game
{
    internal static class RainwaterProductionStopper
    {
        private static readonly HashSet<string> ManagedRecipeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly string[] RecipeEnabledMemberNames =
        {
            "active",
            "Active",
            "isActive",
            "IsActive",
            "enabled",
            "Enabled",
            "isEnabled",
            "IsEnabled"
        };

        private static readonly string[] RecipeSetterNames =
        {
            "SetRecipeActive",
            "SetRecipeEnabled",
            "SetActiveRecipe",
            "SetRecipeState"
        };

        private static readonly string[] RefreshMethodNames =
        {
            "RefreshRecipes",
            "RefreshLimits",
            "Refresh",
            "UpdateRecipes",
            "UpdateProduction"
        };

        public static void Apply()
        {
            if (!ConfigManager.AutoStopProductionWithoutRainwater || !GameAPI.IsGameActive())
            {
                return;
            }

            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var workshop in GameAPI.GetRecipeBuildings())
            {
                if (workshop?.Recipes == null || workshop.Base == null)
                {
                    continue;
                }

                if (!TryGetRainwaterAmount(workshop.Base, out var rainwaterAmount))
                {
                    continue;
                }

                var hasRainwater = rainwaterAmount > 0;
                var index = 0;
                foreach (var recipeState in workshop.Recipes)
                {
                    if (recipeState == null)
                    {
                        index++;
                        continue;
                    }

                    var key = BuildRecipeKey(workshop.Base, recipeState, index);
                    seenKeys.Add(key);

                    if (hasRainwater)
                    {
                        if (ManagedRecipeKeys.Contains(key) && TrySetRecipeEnabled(workshop, recipeState, index, true))
                        {
                            ManagedRecipeKeys.Remove(key);
                        }

                        index++;
                        continue;
                    }

                    if (ManagedRecipeKeys.Contains(key))
                    {
                        index++;
                        continue;
                    }

                    if (TryGetRecipeEnabled(recipeState, out var enabled) && enabled && TrySetRecipeEnabled(workshop, recipeState, index, false))
                    {
                        ManagedRecipeKeys.Add(key);
                        Plugin.Log($"Stopped recipe {GetRecipeId(recipeState, index)} in {GetBuildingName(workshop.Base)} because the building has no rainwater");
                    }

                    index++;
                }
            }

            ManagedRecipeKeys.RemoveWhere(key => !seenKeys.Contains(key));
        }

        public static void RestoreManagedRecipes()
        {
            if (ManagedRecipeKeys.Count == 0)
            {
                return;
            }

            if (!GameAPI.IsGameActive())
            {
                ManagedRecipeKeys.Clear();
                return;
            }

            var restored = new List<string>();

            foreach (var workshop in GameAPI.GetRecipeBuildings())
            {
                if (workshop?.Recipes == null || workshop.Base == null)
                {
                    continue;
                }

                var index = 0;
                foreach (var recipeState in workshop.Recipes)
                {
                    if (recipeState == null)
                    {
                        index++;
                        continue;
                    }

                    var key = BuildRecipeKey(workshop.Base, recipeState, index);
                    if (ManagedRecipeKeys.Contains(key) && TrySetRecipeEnabled(workshop, recipeState, index, true))
                    {
                        restored.Add(key);
                    }

                    index++;
                }
            }

            foreach (var key in restored)
            {
                ManagedRecipeKeys.Remove(key);
            }
        }

        public static void Clear()
        {
            ManagedRecipeKeys.Clear();
        }

        private static bool TryGetRainwaterAmount(object source, out int amount)
        {
            var total = 0;
            var found = false;
            ScanRainwaterAmount(source, 0, false, new HashSet<object>(ReferenceComparer.Instance), ref found, ref total);
            amount = total;
            return found;
        }

        private static void ScanRainwaterAmount(object source, int depth, bool rainwaterContext, HashSet<object> visited, ref bool found, ref int total)
        {
            if (source == null || depth > 4)
            {
                return;
            }

            var sourceType = source.GetType();
            if (IsSimpleType(sourceType) || !visited.Add(source))
            {
                return;
            }

            foreach (var methodName in new[] { "GetRainwater", "GetRainWater", "GetRainwaterAmount", "GetRainWaterAmount", "GetWaterAmount" })
            {
                var method = sourceType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (method == null)
                {
                    continue;
                }

                try
                {
                    var value = method.Invoke(source, null);
                    if (TryConvertAmount(value, out var methodAmount))
                    {
                        found = true;
                        total += methodAmount;
                    }
                    else
                    {
                        ScanRainwaterAmount(value, depth + 1, true, visited, ref found, ref total);
                    }
                }
                catch (Exception)
                {
                }
            }

            foreach (var member in GetReadableMembers(sourceType))
            {
                var memberName = member.Name;
                if (ShouldSkipRainwaterMember(memberName) || !TryGetMemberValue(source, member, out var value))
                {
                    continue;
                }

                var memberContext = rainwaterContext || LooksLikeRainwaterContext(memberName) || LooksLikeRainwaterContext(value?.GetType().Name);
                if (!memberContext)
                {
                    continue;
                }

                if (LooksLikeRainwaterAmountName(memberName) && TryConvertAmount(value, out var memberAmount))
                {
                    found = true;
                    total += memberAmount;
                    continue;
                }

                if (value == null || IsSimpleType(value.GetType()))
                {
                    continue;
                }

                if (value is IEnumerable enumerable && !(value is string))
                {
                    var scanned = 0;
                    foreach (var item in enumerable)
                    {
                        ScanRainwaterAmount(item, depth + 1, true, visited, ref found, ref total);
                        scanned++;
                        if (scanned >= 12)
                        {
                            break;
                        }
                    }

                    continue;
                }

                ScanRainwaterAmount(value, depth + 1, true, visited, ref found, ref total);
            }
        }

        private static IEnumerable<MemberInfo> GetReadableMembers(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var field in type.GetFields(flags))
            {
                yield return field;
            }

            foreach (var property in type.GetProperties(flags))
            {
                if (property.GetIndexParameters().Length == 0 && property.GetGetMethod(true) != null)
                {
                    yield return property;
                }
            }
        }

        private static bool TryGetMemberValue(object source, MemberInfo member, out object value)
        {
            value = null;

            try
            {
                if (member is FieldInfo field)
                {
                    value = field.GetValue(source);
                    return true;
                }

                if (member is PropertyInfo property)
                {
                    value = property.GetValue(source, null);
                    return true;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        private static bool TryGetRecipeEnabled(object recipeState, out bool enabled)
        {
            foreach (var name in RecipeEnabledMemberNames)
            {
                if (TryGetBoolMember(recipeState, name, out enabled))
                {
                    return true;
                }
            }

            enabled = false;
            return false;
        }

        private static bool TrySetRecipeEnabled(IWorkshop workshop, object recipeState, int index, bool enabled)
        {
            if (TryInvokeRecipeSetter(workshop, recipeState, index, enabled) || TrySetBoolMember(recipeState, enabled))
            {
                InvokeRefresh(workshop);
                return true;
            }

            return false;
        }

        private static bool TryInvokeRecipeSetter(IWorkshop workshop, object recipeState, int index, bool enabled)
        {
            var recipeId = GetRecipeId(recipeState, index);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var method in workshop.GetType().GetMethods(flags))
            {
                if (!IsNameIn(method.Name, RecipeSetterNames))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != 2 || parameters[1].ParameterType != typeof(bool))
                {
                    continue;
                }

                var firstArgument = BuildRecipeSetterArgument(parameters[0].ParameterType, recipeState, recipeId, index);
                if (firstArgument == null)
                {
                    continue;
                }

                try
                {
                    method.Invoke(workshop, new[] { firstArgument, enabled });
                    return true;
                }
                catch (Exception)
                {
                }
            }

            return false;
        }

        private static object BuildRecipeSetterArgument(Type parameterType, object recipeState, string recipeId, int index)
        {
            if (parameterType == typeof(string) && !string.IsNullOrWhiteSpace(recipeId))
            {
                return recipeId;
            }

            if (parameterType == typeof(int))
            {
                return index;
            }

            return parameterType.IsInstanceOfType(recipeState) ? recipeState : null;
        }

        private static bool TryGetBoolMember(object source, string name, out bool value)
        {
            value = false;
            if (source == null)
            {
                return false;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = source.GetType();
            var field = type.GetField(name, flags);
            if (field != null && field.FieldType == typeof(bool))
            {
                value = (bool)field.GetValue(source);
                return true;
            }

            var property = type.GetProperty(name, flags);
            if (property != null && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
            {
                value = (bool)property.GetValue(source, null);
                return true;
            }

            var method = type.GetMethod(name, flags, null, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(bool))
            {
                value = (bool)method.Invoke(source, null);
                return true;
            }

            return false;
        }

        private static bool TrySetBoolMember(object source, bool value)
        {
            foreach (var name in RecipeEnabledMemberNames)
            {
                if (TrySetBoolMember(source, name, value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TrySetBoolMember(object source, string name, bool value)
        {
            if (source == null)
            {
                return false;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = source.GetType();

            try
            {
                var field = type.GetField(name, flags);
                if (field != null && field.FieldType == typeof(bool) && !field.IsInitOnly)
                {
                    field.SetValue(source, value);
                    return true;
                }

                var property = type.GetProperty(name, flags);
                if (property != null && property.PropertyType == typeof(bool) && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    property.SetValue(source, value, null);
                    return true;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        private static void InvokeRefresh(IWorkshop workshop)
        {
            foreach (var target in new object[] { workshop, workshop.Base })
            {
                if (target == null)
                {
                    continue;
                }

                var type = target.GetType();
                foreach (var methodName in RefreshMethodNames)
                {
                    var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    if (method == null)
                    {
                        continue;
                    }

                    try
                    {
                        method.Invoke(target, null);
                        return;
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private static string BuildRecipeKey(ProductionBuilding building, object recipeState, int index)
        {
            return RuntimeHelpers.GetHashCode(building) + ":" + GetRecipeId(recipeState, index);
        }

        private static string GetRecipeId(object recipeState, int index)
        {
            foreach (var name in new[] { "model", "Model", "recipe", "Recipe", "recipeName", "RecipeName", "Name", "productName", "ProductName" })
            {
                if (TryReadStableString(recipeState, name, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return index.ToString();
        }

        private static string GetBuildingName(ProductionBuilding building)
        {
            if (TryReadStableString(building, "ModelName", out var modelName) && !string.IsNullOrWhiteSpace(modelName))
            {
                return modelName;
            }

            if (TryReadStableString(building, "name", out var name) && !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return building.GetType().Name;
        }

        private static bool TryReadStableString(object source, string memberName, out string value)
        {
            value = null;
            if (source == null)
            {
                return false;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = source.GetType();

            try
            {
                var field = type.GetField(memberName, flags);
                if (field != null)
                {
                    return TryConvertStableString(field.GetValue(source), out value);
                }

                var property = type.GetProperty(memberName, flags);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    return TryConvertStableString(property.GetValue(source, null), out value);
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        private static bool TryConvertStableString(object source, out string value)
        {
            value = null;
            if (source == null)
            {
                return false;
            }

            if (source is string text)
            {
                value = text;
                return true;
            }

            if (TryReadStableString(source, "Name", out value) && !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            value = source.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryConvertAmount(object value, out int amount)
        {
            amount = 0;
            if (value == null)
            {
                return false;
            }

            var type = value.GetType();
            if (type.IsEnum)
            {
                return false;
            }

            try
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Int32:
                    case TypeCode.UInt32:
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                    case TypeCode.Single:
                    case TypeCode.Double:
                    case TypeCode.Decimal:
                        var number = Convert.ToDouble(value);
                        if (double.IsNaN(number) || double.IsInfinity(number))
                        {
                            return false;
                        }

                        amount = number <= 0d ? 0 : (int)Math.Ceiling(number);
                        return true;
                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool LooksLikeRainwaterContext(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var lower = name.ToLowerInvariant();
            return lower.Contains("rain") || lower.Contains("rainpunk") || lower.Contains("water");
        }

        private static bool LooksLikeRainwaterAmountName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || ShouldSkipRainwaterMember(name))
            {
                return false;
            }

            var lower = name.ToLowerInvariant();
            return lower.Contains("amount") ||
                   lower.Contains("current") ||
                   lower.Contains("stored") ||
                   lower.Contains("value") ||
                   lower.Contains("level") ||
                   lower.Contains("rainwater") ||
                   lower.Contains("water");
        }

        private static bool ShouldSkipRainwaterMember(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return true;
            }

            var lower = name.ToLowerInvariant();
            return lower.Contains("capacity") ||
                   lower.Contains("max") ||
                   lower.Contains("limit") ||
                   lower.Contains("bonus") ||
                   lower.Contains("consumption") ||
                   lower.Contains("usage") ||
                   lower.Contains("rate") ||
                   lower.Contains("required") ||
                   lower.Contains("need") ||
                   lower.Contains("cost") ||
                   lower.Contains("speed");
        }

        private static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
        }

        private static bool IsNameIn(string name, IEnumerable<string> values)
        {
            foreach (var value in values)
            {
                if (string.Equals(name, value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
