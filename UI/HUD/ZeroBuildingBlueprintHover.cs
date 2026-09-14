using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Eremite.View.HUD.Rainpunk;
using StockAlert.Config;
using StockAlert.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StockAlert.UI.HUD
{
    internal sealed class ZeroBuildingBlueprintHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static FieldInfo _fiCorruptionButton;
        private static GUIStyle _panelStyle;
        private static GUIStyle _titleStyle;
        private static GUIStyle _itemStyle;
        private static GUIStyle _costStyle;
        private static GUIStyle _emptyStyle;
        private static Texture2D _panelTexture;

        private bool _hovered;
        private List<BuildingRow> _buildings = new List<BuildingRow>();
        private float _nextRefreshTime;

        public static void Attach(BlightHUD blightHud)
        {
            if (!ConfigManager.ShowZeroBuildingBlueprintHover || blightHud == null)
            {
                return;
            }

            _fiCorruptionButton ??= typeof(BlightHUD).GetField("corruptionButton", BindingFlags.Instance | BindingFlags.NonPublic);
            var button = _fiCorruptionButton?.GetValue(blightHud) as Button;
            Attach(button != null ? button.gameObject : blightHud.gameObject);
        }

        private static void Attach(GameObject target)
        {
            if (target == null || target.GetComponent<ZeroBuildingBlueprintHover>() != null)
            {
                return;
            }

            target.AddComponent<ZeroBuildingBlueprintHover>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!ConfigManager.ShowZeroBuildingBlueprintHover)
            {
                return;
            }

            _hovered = true;
            RefreshList();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
        }

        private void OnDisable()
        {
            _hovered = false;
        }

        private void Update()
        {
            if (!_hovered || !ConfigManager.ShowZeroBuildingBlueprintHover || Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            RefreshList();
        }

        private void OnGUI()
        {
            if (!_hovered || !ConfigManager.ShowZeroBuildingBlueprintHover)
            {
                return;
            }

            EnsureStyles();

            const float titleHeight = 28f;
            const float lineHeight = 36f;
            const float buildingIconSize = 31f;
            const float materialIconSize = 23f;
            const float padding = 12f;
            const float buildingIconGap = 9f;
            const float costSectionGap = 18f;
            const float materialTextGap = 4f;
            const float materialGap = 12f;
            var shownCount = Mathf.Min(_buildings.Count, 14);
            var hasMore = _buildings.Count > shownCount;
            var nameWidth = GetNameColumnWidth(shownCount);
            var costsWidth = GetCostsColumnWidth(shownCount, materialIconSize, materialTextGap, materialGap);
            var desiredWidth = padding * 2f + buildingIconSize + buildingIconGap + nameWidth;
            if (costsWidth > 0f)
            {
                desiredWidth += costSectionGap + costsWidth;
            }

            var maxWidth = Mathf.Max(420f, Screen.width - 16f);
            if (desiredWidth > maxWidth)
            {
                var nameWidthReduction = Mathf.Min(nameWidth - 100f, desiredWidth - maxWidth);
                nameWidth -= nameWidthReduction;
                desiredWidth -= nameWidthReduction;
            }

            var width = Mathf.Clamp(desiredWidth, 420f, maxWidth);
            var height = padding * 2f + titleHeight + (shownCount > 0 ? shownCount * lineHeight : 28f) + (hasMore ? lineHeight : 0f);
            var x = Screen.width - width - 24f;
            var y = Screen.height - height - 24f;
            x = Mathf.Max(8f, x);
            y = Mathf.Max(8f, y);

            var rect = new Rect(x, y, width, height);
            GUI.Box(rect, GUIContent.none, _panelStyle);
            GUI.Label(new Rect(x + padding, y + padding, width - padding * 2f, titleHeight), "Unlocked buildings with 0 placed", _titleStyle);

            var lineY = y + padding + titleHeight;
            if (_buildings.Count == 0)
            {
                GUI.Label(new Rect(x + padding, lineY, width - padding * 2f, 28f), "None right now.", _emptyStyle);
                return;
            }

            for (var i = 0; i < shownCount; i++)
            {
                var row = _buildings[i];
                var buildingIconX = x + padding;
                var nameX = buildingIconX + buildingIconSize + buildingIconGap;
                DrawSprite(row.Icon, new Rect(buildingIconX, lineY + 2.5f, buildingIconSize, buildingIconSize));
                GUI.Label(new Rect(nameX, lineY, nameWidth, lineHeight), row.Name, _itemStyle);

                if (row.Costs.Count > 0)
                {
                    var costX = nameX + nameWidth + costSectionGap;
                    DrawCosts(row.Costs, costX, lineY, lineHeight, materialIconSize, materialTextGap, materialGap);
                }

                lineY += lineHeight;
            }

            if (hasMore)
            {
                GUI.Label(new Rect(x + padding, lineY, width - padding * 2f, lineHeight), $"+ {_buildings.Count - shownCount} more", _emptyStyle);
            }
        }

        private void RefreshList()
        {
            _nextRefreshTime = Time.unscaledTime + 0.5f;
            _buildings = GameAPI.GetUnlockedZeroCountBuildings()
                .Select(info => new BuildingRow(
                    GameAPI.GetBuildingDisplayName(info.Model),
                    info.Model?.icon,
                    GameAPI.GetBuildingConstructionCosts(info.Model)
                ))
                .Where(row => !string.IsNullOrWhiteSpace(row.Name))
                .GroupBy(row => row.Name)
                .Select(group => group.First())
                .ToList();
        }

        private float GetNameColumnWidth(int shownCount)
        {
            var width = 140f;
            for (var i = 0; i < shownCount; i++)
            {
                width = Mathf.Max(width, _itemStyle.CalcSize(new GUIContent(_buildings[i].Name)).x + 4f);
            }

            return Mathf.Min(width, 240f);
        }

        private float GetCostsColumnWidth(int shownCount, float iconSize, float textGap, float itemGap)
        {
            var maxWidth = 0f;
            for (var i = 0; i < shownCount; i++)
            {
                var rowWidth = 0f;
                var costs = _buildings[i].Costs;
                for (var j = 0; j < costs.Count; j++)
                {
                    var cost = costs[j];
                    rowWidth += iconSize + textGap + GetCostTextWidth(cost);
                    if (j < costs.Count - 1)
                    {
                        rowWidth += itemGap;
                    }
                }

                maxWidth = Mathf.Max(maxWidth, rowWidth);
            }

            return maxWidth;
        }

        private static void DrawCosts(
            IReadOnlyList<GameAPI.BuildingCostInfo> costs,
            float x,
            float y,
            float lineHeight,
            float iconSize,
            float textGap,
            float itemGap)
        {
            var iconY = y + (lineHeight - iconSize) * 0.5f;
            for (var i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];
                DrawSprite(cost.Icon, new Rect(x, iconY, iconSize, iconSize));
                x += iconSize + textGap;

                var text = GetCostText(cost);
                var textWidth = GetCostTextWidth(cost);
                GUI.Label(new Rect(x, y, textWidth, lineHeight), text, _costStyle);
                x += textWidth + itemGap;
            }
        }

        private static string GetCostText(GameAPI.BuildingCostInfo cost)
        {
            if (cost.Available >= cost.Needed)
            {
                return $"{cost.Available}/{cost.Needed}";
            }

            return $"<color=#FF2A00>{cost.Available}</color>/{cost.Needed}";
        }

        private static float GetCostTextWidth(GameAPI.BuildingCostInfo cost)
        {
            return _costStyle.CalcSize(new GUIContent($"{cost.Available}/{cost.Needed}")).x + 2f;
        }

        private static void DrawSprite(Sprite sprite, Rect rect)
        {
            var texture = sprite?.texture;
            if (texture == null)
            {
                return;
            }

            var textureRect = sprite.textureRect;
            var texCoords = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height
            );

            GUI.DrawTextureWithTexCoords(rect, texture, texCoords, true);
        }

        private static void EnsureStyles()
        {
            if (_panelStyle != null && _panelTexture != null)
            {
                return;
            }

            _panelTexture = MakeTexture(new Color32(12, 10, 8, 238), new Color32(178, 143, 72, 255));
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal =
                {
                    background = _panelTexture
                }
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color32(255, 232, 151, 255) }
            };

            _itemStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color32(244, 238, 219, 255) }
            };

            _costStyle = new GUIStyle(_itemStyle)
            {
                fontSize = 12,
                richText = true
            };

            _emptyStyle = new GUIStyle(_itemStyle)
            {
                normal = { textColor = new Color32(186, 178, 158, 255) }
            };
        }

        private static Texture2D MakeTexture(Color32 fill, Color32 border)
        {
            const int size = 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var isBorder = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    texture.SetPixel(x, y, isBorder ? border : fill);
                }
            }

            texture.Apply(false, true);
            DontDestroyOnLoad(texture);
            return texture;
        }

        private readonly struct BuildingRow
        {
            public BuildingRow(string name, Sprite icon, List<GameAPI.BuildingCostInfo> costs)
            {
                Name = name;
                Icon = icon;
                Costs = costs ?? new List<GameAPI.BuildingCostInfo>();
            }

            public string Name { get; }

            public Sprite Icon { get; }

            public List<GameAPI.BuildingCostInfo> Costs { get; }
        }
    }
}
