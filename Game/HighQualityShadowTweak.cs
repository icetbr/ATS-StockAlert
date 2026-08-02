using System;
using System.Reflection;
using StockAlert.Config;
using UnityEngine;
using UnityEngine.Rendering;

namespace StockAlert.Game
{
    internal static class HighQualityShadowTweak
    {
        private const int TargetMainShadowmapResolution = 8192;
        private const int TargetAdditionalShadowmapResolution = 4096;
        private const int TargetShadowCascadeCount = 4;
        private static readonly Vector3 TargetCascade4Split = new Vector3(0.25f, 0.5f, 0.75f);

        private static object _capturedAsset;
        private static ShadowSettings _originalSettings;
        private static bool _hasOriginalSettings;
        private static bool _wasEnabled;

        public static void Refresh()
        {
            var enabled = ConfigManager.HighQualityShadows;
            var asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null)
            {
                _wasEnabled = enabled;
                return;
            }

            if (!enabled)
            {
                if (_wasEnabled)
                {
                    Restore(asset);
                }

                _wasEnabled = false;
                return;
            }

            if (!ReferenceEquals(asset, _capturedAsset))
            {
                _capturedAsset = asset;
                _originalSettings = ShadowSettings.Capture(asset);
                _hasOriginalSettings = true;
            }

            Apply(asset);
            _wasEnabled = true;
        }

        public static void Restore()
        {
            var asset = GraphicsSettings.currentRenderPipeline;
            if (asset != null)
            {
                Restore(asset);
            }

            _wasEnabled = false;
        }

        private static void Apply(object asset)
        {
            SetBool(asset, "supportsMainLightShadows", true);
            SetBool(asset, "supportsSoftShadows", true);
            SetIntAtLeast(asset, "mainLightShadowmapResolution", TargetMainShadowmapResolution);
            SetIntAtLeast(asset, "additionalLightsShadowmapResolution", TargetAdditionalShadowmapResolution);
            SetIntAtLeast(asset, "additionalLightsShadowResolutionTierLow", TargetAdditionalShadowmapResolution);
            SetIntAtLeast(asset, "additionalLightsShadowResolutionTierMedium", TargetAdditionalShadowmapResolution);
            SetIntAtLeast(asset, "additionalLightsShadowResolutionTierHigh", TargetAdditionalShadowmapResolution);
            SetIntAtLeast(asset, "shadowCascadeCount", TargetShadowCascadeCount);
            SetObject(asset, "cascade4Split", TargetCascade4Split);
        }

        private static void Restore(object asset)
        {
            if (!_hasOriginalSettings || !ReferenceEquals(asset, _capturedAsset))
            {
                return;
            }

            _originalSettings.Apply(asset);
            _hasOriginalSettings = false;
            _capturedAsset = null;
        }

        private static void SetIntAtLeast(object target, string propertyName, int value)
        {
            var property = GetWritableProperty(target, propertyName);
            if (property == null)
            {
                return;
            }

            var current = property.GetValue(target, null);
            if (current is int currentValue && currentValue >= value)
            {
                return;
            }

            property.SetValue(target, value, null);
        }

        private static void SetBool(object target, string propertyName, bool value)
        {
            var property = GetWritableProperty(target, propertyName);
            property?.SetValue(target, value, null);
        }

        private static void SetObject(object target, string propertyName, object value)
        {
            var property = GetWritableProperty(target, propertyName);
            if (property != null && property.PropertyType.IsInstanceOfType(value))
            {
                property.SetValue(target, value, null);
            }
        }

        private static PropertyInfo GetWritableProperty(object target, string propertyName)
        {
            return target?.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
        }

        private readonly struct ShadowSettings
        {
            private readonly Tuple<string, object>[] _values;

            private ShadowSettings(Tuple<string, object>[] values)
            {
                _values = values;
            }

            public static ShadowSettings Capture(object asset)
            {
                var propertyNames = new[]
                {
                    "supportsMainLightShadows",
                    "supportsSoftShadows",
                    "mainLightShadowmapResolution",
                    "additionalLightsShadowmapResolution",
                    "additionalLightsShadowResolutionTierLow",
                    "additionalLightsShadowResolutionTierMedium",
                    "additionalLightsShadowResolutionTierHigh",
                    "shadowCascadeCount",
                    "cascade4Split"
                };

                var values = new Tuple<string, object>[propertyNames.Length];
                for (var i = 0; i < propertyNames.Length; i++)
                {
                    var property = GetWritableProperty(asset, propertyNames[i]);
                    values[i] = Tuple.Create(propertyNames[i], property?.GetValue(asset, null));
                }

                return new ShadowSettings(values);
            }

            public void Apply(object asset)
            {
                if (_values == null)
                {
                    return;
                }

                foreach (var entry in _values)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    var property = GetWritableProperty(asset, entry.Item1);
                    if (property != null)
                    {
                        property.SetValue(asset, entry.Item2, null);
                    }
                }
            }
        }
    }
}
