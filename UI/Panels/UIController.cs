using UnityEngine;
using UnityEngine.UI;
using StockAlert.Config;
using StockAlert.Game;
using StockAlert.Infrastructure.Plugin;
using StockAlert.UI.World;
using UnityEngine.EventSystems;

namespace StockAlert.UI.Panels
{
    public static class UI
    {
        private static GameObject _uiRoot;
        private static SettingsPanelBehaviour _panel;

        public static void Initialize()
        {
            if (_uiRoot != null)
            {
                return;
            }

            Plugin.Log("UI.Initialize()");

            _uiRoot = new GameObject("StockAlertUI");
            Object.DontDestroyOnLoad(_uiRoot);
            _panel = _uiRoot.AddComponent<SettingsPanelBehaviour>();
        }

        public static void Refresh()
        {
            _panel?.RefreshInputs();
        }

        public static void Toggle()
        {
            if (_panel == null)
            {
                return;
            }

            _panel.Toggle();
            Plugin.Log("UI toggled: " + _panel.Visible);
        }

        private sealed class SettingsPanelBehaviour : MonoBehaviour
        {
            private static Texture2D _windowBackground;
            private static Texture2D _panelBackground;
            private static Texture2D _buttonBackground;
            private static Texture2D _buttonHoverBackground;
            private static Texture2D _fieldBackground;
            private static Font _uiFont;
            private static GUIStyle _windowStyle;
            private static GUIStyle _titleStyle;
            private static GUIStyle _bodyStyle;
            private static GUIStyle _hintStyle;
            private static GUIStyle _toggleStyle;
            private static GUIStyle _buttonStyle;
            private static GUIStyle _fieldStyle;
            private static GUIStyle _sectionStyle;

            private Rect _windowRect = new Rect(40f, 40f, 430f, 510f);
            private string _multiplierInput = "2.0";
            private string _buildingShortageIconScaleInput = "0.90";
            private GameObject _clickBlockerCanvasObject;
            private RectTransform _clickBlockerRect;

            public bool Visible { get; private set; }

            public void Toggle()
            {
                Visible = !Visible;
                EnsureClickBlocker();
                UpdateClickBlockerState();
            }

            private void OnGUI()
            {
                if (!Visible)
                {
                    UpdateClickBlockerState();
                    return;
                }

                EnsureStyles();
                EnsureClickBlocker();
                _windowRect = GUILayout.Window(
                    GetInstanceID(),
                    _windowRect,
                    DrawWindow,
                    GUIContent.none,
                    _windowStyle
                );
                UpdateClickBlockerRect();
                UpdateClickBlockerState();
            }

            private void DrawWindow(int windowId)
            {
                var titleRect = new Rect(20f, 14f, _windowRect.width - 40f, 36f);
                GUI.Label(titleRect, $"Stock Alert Settings ({StockAlertInfo.Version})", _titleStyle);
                GUI.Box(new Rect(18f, 50f, _windowRect.width - 36f, 1f), GUIContent.none, _sectionStyle);

                GUILayout.Space(42f);
                GUILayout.BeginVertical();
                GUILayout.Label("Toggle key: " + ConfigManager.ToggleSettingsKey, _bodyStyle);
                GUILayout.Label("Use these settings to control the HUD and optional production automation.", _hintStyle);
                GUILayout.Space(12f);

                var showHud = GUILayout.Toggle(ConfigManager.ShowHud, "Show HUD", _toggleStyle);
                if (showHud != ConfigManager.ShowHud)
                {
                    ConfigManager.ShowHud = showHud;
                }

                var movableHud = GUILayout.Toggle(ConfigManager.MovableHud, "Movable HUD", _toggleStyle);
                if (movableHud != ConfigManager.MovableHud)
                {
                    ConfigManager.MovableHud = movableHud;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                GUILayout.Label("HUD anchor", _bodyStyle, GUILayout.Width(92f));
                if (GUILayout.Toggle(ConfigManager.HudAnchor == ConfigManager.HudHorizontalAnchor.Left, "Left", _buttonStyle, GUILayout.Width(72f)) &&
                    ConfigManager.HudAnchor != ConfigManager.HudHorizontalAnchor.Left)
                {
                    ConfigManager.HudAnchor = ConfigManager.HudHorizontalAnchor.Left;
                    ConfigManager.HudPosition = new Vector2(-1f, -1f);
                }
                if (GUILayout.Toggle(ConfigManager.HudAnchor == ConfigManager.HudHorizontalAnchor.Right, "Right", _buttonStyle, GUILayout.Width(72f)) &&
                    ConfigManager.HudAnchor != ConfigManager.HudHorizontalAnchor.Right)
                {
                    ConfigManager.HudAnchor = ConfigManager.HudHorizontalAnchor.Right;
                    ConfigManager.HudPosition = new Vector2(-1f, -1f);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                var buildingIndicators = GUILayout.Toggle(
                    ConfigManager.ShowBuildingAlertIndicators,
                    "Building shortage indicators",
                    _toggleStyle
                );
                if (buildingIndicators != ConfigManager.ShowBuildingAlertIndicators)
                {
                    ConfigManager.ShowBuildingAlertIndicators = buildingIndicators;
                    if (buildingIndicators)
                    {
                        BuildingAlertIndicators.Refresh();
                    }
                    else
                    {
                        BuildingAlertIndicators.RestoreVanilla();
                    }
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                var specificItemIndicator = GUILayout.Toggle(
                    ConfigManager.ShowBuildingSpecificItemIndicators,
                    "Specific item indicator",
                    _toggleStyle,
                    GUILayout.Width(210f)
                );
                if (specificItemIndicator != ConfigManager.ShowBuildingSpecificItemIndicators)
                {
                    ConfigManager.ShowBuildingSpecificItemIndicators = specificItemIndicator;
                    if (ConfigManager.ShowBuildingAlertIndicators)
                    {
                        BuildingAlertIndicators.Refresh();
                    }
                }

                var previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && ConfigManager.ShowBuildingSpecificItemIndicators;
                GUILayout.Label("Size", _bodyStyle, GUILayout.Width(34f));
                var nextShortageIconScaleInput = GUILayout.TextField(_buildingShortageIconScaleInput ?? string.Empty, _fieldStyle, GUILayout.Width(64f));
                if (nextShortageIconScaleInput != _buildingShortageIconScaleInput)
                {
                    _buildingShortageIconScaleInput = nextShortageIconScaleInput;
                    if (TryParseBuildingShortageIconScale(_buildingShortageIconScaleInput, out var shortageIconScale))
                    {
                        ConfigManager.BuildingShortageIconScale = shortageIconScale;
                        _buildingShortageIconScaleInput = ConfigManager.BuildingShortageIconScale.ToString("0.00");
                        if (ConfigManager.ShowBuildingAlertIndicators)
                        {
                            BuildingAlertIndicators.Refresh();
                        }
                    }
                }
                GUILayout.Label("(0.20 - 2.00)", _hintStyle);
                GUI.enabled = previousEnabled;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                var builderStatusIcons = GUILayout.Toggle(
                    ConfigManager.ShowBuilderStatusIcons,
                    "Builder status icons",
                    _toggleStyle
                );
                if (builderStatusIcons != ConfigManager.ShowBuilderStatusIcons)
                {
                    ConfigManager.ShowBuilderStatusIcons = builderStatusIcons;
                    if (builderStatusIcons)
                    {
                        BuilderStatusIndicators.Refresh();
                    }
                    else
                    {
                        BuilderStatusIndicators.Clear();
                    }
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                var previousBuilderChildrenEnabled = GUI.enabled;
                GUI.enabled = previousBuilderChildrenEnabled && ConfigManager.ShowBuilderStatusIcons;
                var idleBuilderStatusIcons = GUILayout.Toggle(
                    ConfigManager.ShowIdleBuilderStatusIcons,
                    "Idle builders",
                    _toggleStyle,
                    GUILayout.Width(160f)
                );
                if (idleBuilderStatusIcons != ConfigManager.ShowIdleBuilderStatusIcons)
                {
                    ConfigManager.ShowIdleBuilderStatusIcons = idleBuilderStatusIcons;
                    if (ConfigManager.ShowBuilderStatusIcons)
                    {
                        BuilderStatusIndicators.Refresh();
                    }
                }

                var busyBuilderStatusIcons = GUILayout.Toggle(
                    ConfigManager.ShowBusyBuilderStatusIcons,
                    "Busy builders",
                    _toggleStyle,
                    GUILayout.Width(160f)
                );
                if (busyBuilderStatusIcons != ConfigManager.ShowBusyBuilderStatusIcons)
                {
                    ConfigManager.ShowBusyBuilderStatusIcons = busyBuilderStatusIcons;
                    if (ConfigManager.ShowBuilderStatusIcons)
                    {
                        BuilderStatusIndicators.Refresh();
                    }
                }
                GUI.enabled = previousBuilderChildrenEnabled;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                var idleBuildersAlert = GUILayout.Toggle(
                    ConfigManager.ShowIdleBuildersAlert,
                    "Idle builders alert",
                    _toggleStyle
                );
                if (idleBuildersAlert != ConfigManager.ShowIdleBuildersAlert)
                {
                    ConfigManager.ShowIdleBuildersAlert = idleBuildersAlert;
                    if (!idleBuildersAlert)
                    {
                        IdleBuildersAlert.Clear();
                    }
                }

                var queuedWorkerAssignments = GUILayout.Toggle(
                    ConfigManager.EnableQueuedWorkerAssignments,
                    "Queued worker assignments",
                    _toggleStyle
                );
                if (queuedWorkerAssignments != ConfigManager.EnableQueuedWorkerAssignments)
                {
                    ConfigManager.EnableQueuedWorkerAssignments = queuedWorkerAssignments;
                    if (!queuedWorkerAssignments)
                    {
                        WorkerAssignmentQueue.ClearAll();
                        QueuedWorkerSlotCompanion.ClearAll();
                    }
                }

                var showForestEaterDirection = GUILayout.Toggle(
                    ConfigManager.ShowForestEaterDirection,
                    "Grazer direction overlay",
                    _toggleStyle
                );
                if (showForestEaterDirection != ConfigManager.ShowForestEaterDirection)
                {
                    ConfigManager.ShowForestEaterDirection = showForestEaterDirection;
                    if (showForestEaterDirection)
                    {
                        ForestEaterDirectionOverlay.Refresh();
                    }
                    else
                    {
                        ForestEaterDirectionOverlay.Clear();
                    }
                }

                var highQualityShadows = GUILayout.Toggle(
                    ConfigManager.HighQualityShadows,
                    "High quality shadows",
                    _toggleStyle
                );
                if (highQualityShadows != ConfigManager.HighQualityShadows)
                {
                    ConfigManager.HighQualityShadows = highQualityShadows;
                    if (highQualityShadows)
                    {
                        HighQualityShadowTweak.Refresh();
                    }
                    else
                    {
                        HighQualityShadowTweak.Restore();
                    }
                }

                var showIngredientWheelBuildingStock = GUILayout.Toggle(
                    ConfigManager.ShowIngredientWheelBuildingStock,
                    "Ingredient wheel building stock",
                    _toggleStyle
                );
                if (showIngredientWheelBuildingStock != ConfigManager.ShowIngredientWheelBuildingStock)
                {
                    ConfigManager.ShowIngredientWheelBuildingStock = showIngredientWheelBuildingStock;
                }

                var showEmbarkationCostRanges = GUILayout.Toggle(
                    ConfigManager.ShowEmbarkationCostRanges,
                    "Embarkation cost ranges",
                    _toggleStyle
                );
                if (showEmbarkationCostRanges != ConfigManager.ShowEmbarkationCostRanges)
                {
                    ConfigManager.ShowEmbarkationCostRanges = showEmbarkationCostRanges;
                }

                var showTradeRouteProfit = GUILayout.Toggle(
                    ConfigManager.ShowTradeRouteProfit,
                    "Trade route profit",
                    _toggleStyle
                );
                if (showTradeRouteProfit != ConfigManager.ShowTradeRouteProfit)
                {
                    ConfigManager.ShowTradeRouteProfit = showTradeRouteProfit;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                var previousTradeRouteProfitChildrenEnabled = GUI.enabled;
                GUI.enabled = previousTradeRouteProfitChildrenEnabled && ConfigManager.ShowTradeRouteProfit;
                var ignoreDisabledRecipes = GUILayout.Toggle(
                    ConfigManager.TradeRouteProfitIgnoreDisabledRecipes,
                    "Ignore disabled recipes",
                    _toggleStyle
                );
                if (ignoreDisabledRecipes != ConfigManager.TradeRouteProfitIgnoreDisabledRecipes)
                {
                    ConfigManager.TradeRouteProfitIgnoreDisabledRecipes = ignoreDisabledRecipes;
                }
                GUI.enabled = previousTradeRouteProfitChildrenEnabled;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                var seasonEndingTradeRoutesAlert = GUILayout.Toggle(
                    ConfigManager.SeasonEndingTradeRoutesAlert,
                    "Season ending trade routes alert",
                    _toggleStyle
                );
                if (seasonEndingTradeRoutesAlert != ConfigManager.SeasonEndingTradeRoutesAlert)
                {
                    ConfigManager.SeasonEndingTradeRoutesAlert = seasonEndingTradeRoutesAlert;
                }

                var timedOrdersAlert = GUILayout.Toggle(
                    ConfigManager.TimedOrdersAlert,
                    "Timed orders alert",
                    _toggleStyle
                );
                if (timedOrdersAlert != ConfigManager.TimedOrdersAlert)
                {
                    ConfigManager.TimedOrdersAlert = timedOrdersAlert;
                    if (!timedOrdersAlert)
                    {
                        TimedOrdersAlert.Clear();
                    }
                }

                GUILayout.Space(10f);

                var autoAdjust = GUILayout.Toggle(
                    ConfigManager.AutoAdjustProductionLimits,
                    "Auto-adjust production limits from consumers",
                    _toggleStyle
                );
                if (autoAdjust != ConfigManager.AutoAdjustProductionLimits)
                {
                    ConfigManager.AutoAdjustProductionLimits = autoAdjust;
                    if (autoAdjust)
                    {
                        AutoProductionLimits.ApplyCurrentTargets();
                    }
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Consumer multiplier", _bodyStyle, GUILayout.Width(140f));
                var nextInput = GUILayout.TextField(_multiplierInput ?? string.Empty, _fieldStyle, GUILayout.Width(64f));
                if (nextInput != _multiplierInput)
                {
                    _multiplierInput = nextInput;
                    if (TryParseMultiplier(_multiplierInput, out var multiplier))
                    {
                        ConfigManager.AutoAdjustMultiplier = multiplier;
                        _multiplierInput = ConfigManager.AutoAdjustMultiplier.ToString("0.0");
                        if (ConfigManager.AutoAdjustProductionLimits)
                        {
                            AutoProductionLimits.ApplyCurrentTargets();
                        }
                    }
                }
                GUILayout.Label("(1.0 - 9.0)", _hintStyle);
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(4f);

                var autoAdjustPurgingFire = GUILayout.Toggle(
                    ConfigManager.AutoAdjustPurgingFire,
                    "Auto-adjust Purging Fire to cysts + 1",
                    _toggleStyle
                );
                if (autoAdjustPurgingFire != ConfigManager.AutoAdjustPurgingFire)
                {
                    ConfigManager.AutoAdjustPurgingFire = autoAdjustPurgingFire;
                    if (autoAdjustPurgingFire)
                    {
                        AutoProductionLimits.ApplyCurrentTargets();
                    }
                }

                var autoPauseOnNoRainwater = GUILayout.Toggle(
                    ConfigManager.AutoPauseOnNoRainwater,
                    "Auto-disable recipes when out of rainwater",
                    _toggleStyle
                );
                if (autoPauseOnNoRainwater != ConfigManager.AutoPauseOnNoRainwater)
                {
                    ConfigManager.AutoPauseOnNoRainwater = autoPauseOnNoRainwater;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                var autoResumeOnRainwater = GUILayout.Toggle(
                    ConfigManager.AutoResumeOnRainwater,
                    "Auto-enable recipes when rainwater returns",
                    _toggleStyle,
                    GUILayout.Width(280f)
                );
                if (autoResumeOnRainwater != ConfigManager.AutoResumeOnRainwater)
                {
                    ConfigManager.AutoResumeOnRainwater = autoResumeOnRainwater;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(12f);
                if (GUILayout.Button("Close", _buttonStyle, GUILayout.Height(28f)))
                {
                    Visible = false;
                }

                GUILayout.EndVertical();
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
            }

            public void RefreshInputs()
            {
                _multiplierInput = ConfigManager.AutoAdjustMultiplier.ToString("0.0");
                _buildingShortageIconScaleInput = ConfigManager.BuildingShortageIconScale.ToString("0.00");
            }

            private void EnsureClickBlocker()
            {
                if (_clickBlockerCanvasObject != null)
                {
                    return;
                }

                _clickBlockerCanvasObject = new GameObject("StockAlertClickBlockerCanvas");
                Object.DontDestroyOnLoad(_clickBlockerCanvasObject);

                var canvas = _clickBlockerCanvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;

                _clickBlockerCanvasObject.AddComponent<GraphicRaycaster>();

                var blockerObject = new GameObject("StockAlertClickBlocker");
                blockerObject.transform.SetParent(_clickBlockerCanvasObject.transform, false);

                _clickBlockerRect = blockerObject.AddComponent<RectTransform>();
                _clickBlockerRect.anchorMin = new Vector2(0f, 1f);
                _clickBlockerRect.anchorMax = new Vector2(0f, 1f);
                _clickBlockerRect.pivot = new Vector2(0f, 1f);

                var image = blockerObject.AddComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0.001f);
                image.raycastTarget = true;
            }

            private void UpdateClickBlockerRect()
            {
                if (_clickBlockerRect == null)
                {
                    return;
                }

                _clickBlockerRect.anchoredPosition = new Vector2(_windowRect.x, -_windowRect.y);
                _clickBlockerRect.sizeDelta = new Vector2(_windowRect.width, _windowRect.height);
            }

            private void UpdateClickBlockerState()
            {
                if (_clickBlockerCanvasObject != null)
                {
                    _clickBlockerCanvasObject.SetActive(Visible);
                }
            }

            private static void EnsureStyles()
            {
                if (_windowStyle != null)
                {
                    return;
                }

                _windowBackground = MakeTexture(new Color32(24, 26, 28, 235), new Color32(112, 90, 56, 255));
                _panelBackground = MakeTexture(new Color32(34, 37, 40, 210), new Color32(72, 60, 42, 255));
                _buttonBackground = MakeTexture(new Color32(70, 56, 37, 235), new Color32(145, 116, 66, 255));
                _buttonHoverBackground = MakeTexture(new Color32(96, 73, 44, 245), new Color32(182, 146, 82, 255));
                _fieldBackground = MakeTexture(new Color32(28, 30, 33, 240), new Color32(114, 97, 63, 255));
                _uiFont = ResolveUIFont();

                _windowStyle = new GUIStyle(GUI.skin.window)
                {
                    border = new RectOffset(3, 3, 3, 3),
                    padding = new RectOffset(14, 14, 16, 12)
                };
                _windowStyle.normal.background = _windowBackground;
                _windowStyle.hover.background = _windowBackground;
                _windowStyle.onNormal.background = _windowBackground;
                _windowStyle.onHover.background = _windowBackground;
                _windowStyle.active.background = _windowBackground;
                _windowStyle.onActive.background = _windowBackground;
                _windowStyle.focused.background = _windowBackground;
                _windowStyle.onFocused.background = _windowBackground;
                _windowStyle.normal.textColor = new Color32(0, 0, 0, 0);
                _windowStyle.hover.textColor = new Color32(0, 0, 0, 0);
                _windowStyle.onNormal.textColor = new Color32(0, 0, 0, 0);
                _windowStyle.onHover.textColor = new Color32(0, 0, 0, 0);
                _windowStyle.active.textColor = new Color32(0, 0, 0, 0);
                _windowStyle.onActive.textColor = new Color32(0, 0, 0, 0);
                _windowStyle.focused.textColor = new Color32(0, 0, 0, 0);
                _windowStyle.onFocused.textColor = new Color32(0, 0, 0, 0);

                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    font = _uiFont,
                    fontSize = 16,
                    richText = true,
                    normal = { textColor = new Color32(222, 196, 132, 255) }
                };
                _titleStyle.clipping = TextClipping.Overflow;

                _bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    font = _uiFont,
                    fontSize = 13,
                    wordWrap = true,
                    clipping = TextClipping.Overflow,
                    padding = new RectOffset(0, 0, 1, 3),
                    normal = { textColor = new Color32(220, 216, 204, 255) }
                };

                _hintStyle = new GUIStyle(_bodyStyle)
                {
                    fontSize = 12,
                    normal = { textColor = new Color32(176, 170, 156, 255) }
                };

                _toggleStyle = new GUIStyle(GUI.skin.toggle)
                {
                    font = _uiFont,
                    fontSize = 13,
                    margin = new RectOffset(4, 4, 3, 3),
                    padding = new RectOffset(20, 4, 1, 4),
                    clipping = TextClipping.Overflow,
                    normal = { textColor = new Color32(226, 221, 210, 255) },
                    onNormal = { textColor = new Color32(236, 228, 214, 255) },
                    hover = { textColor = new Color32(245, 235, 216, 255) },
                    onHover = { textColor = new Color32(245, 235, 216, 255) }
                };

                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    font = _uiFont,
                    fontSize = 13,
                    normal =
                    {
                        background = _buttonBackground,
                        textColor = new Color32(241, 231, 207, 255)
                    },
                    hover =
                    {
                        background = _buttonHoverBackground,
                        textColor = new Color32(255, 245, 221, 255)
                    },
                    active =
                    {
                        background = _buttonBackground,
                        textColor = new Color32(228, 215, 188, 255)
                    },
                    border = new RectOffset(3, 3, 3, 3)
                };

                _fieldStyle = new GUIStyle(GUI.skin.textField)
                {
                    font = _uiFont,
                    fontSize = 13,
                    padding = new RectOffset(6, 6, 2, 4),
                    normal =
                    {
                        background = _fieldBackground,
                        textColor = new Color32(235, 229, 216, 255)
                    },
                    focused =
                    {
                        background = _fieldBackground,
                        textColor = new Color32(248, 240, 223, 255)
                    },
                    border = new RectOffset(3, 3, 3, 3)
                };

                _sectionStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = _panelBackground },
                    border = new RectOffset(1, 1, 1, 1),
                    margin = new RectOffset(0, 0, 0, 0),
                    padding = new RectOffset(0, 0, 0, 0)
                };
            }

            private static Texture2D MakeTexture(Color32 fill, Color32 border)
            {
                var texture = new Texture2D(8, 8, TextureFormat.ARGB32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };

                var pixels = new Color32[64];
                for (var y = 0; y < 8; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        var isBorder = x == 0 || x == 7 || y == 0 || y == 7;
                        pixels[(y * 8) + x] = isBorder ? border : fill;
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                return texture;
            }

            private static Font ResolveUIFont()
            {
                try
                {
                    foreach (var text in Resources.FindObjectsOfTypeAll<Text>())
                    {
                        if (text != null && text.font != null)
                        {
                            return text.font;
                        }
                    }
                }
                catch
                {
                }

                try
                {
                    var dynamicFont = Font.CreateDynamicFontFromOSFont(
                        new[]
                        {
                            "Segoe UI",
                            "Liberation Sans",
                            "DejaVu Sans",
                            "Noto Sans",
                            "Ubuntu",
                            "Arial"
                        },
                        14
                    );
                    if (dynamicFont != null)
                    {
                        return dynamicFont;
                    }
                }
                catch
                {
                }

                try
                {
                    return Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch
                {
                    return GUI.skin?.font;
                }
            }

            private static bool TryParseMultiplier(string raw, out float value)
            {
                value = 0f;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return false;
                }

                var normalized = raw.Trim().Replace(',', '.');
                if (!float.TryParse(normalized, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                {
                    return false;
                }

                value = Mathf.Clamp(Mathf.Round(value * 10f) / 10f, 1f, 9f);
                return true;
            }

            private static bool TryParseBuildingShortageIconScale(string raw, out float value)
            {
                value = 0f;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return false;
                }

                var normalized = raw.Trim().Replace(',', '.');
                if (!float.TryParse(normalized, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                {
                    return false;
                }

                value = Mathf.Clamp(Mathf.Round(value * 100f) / 100f, 0.2f, 2f);
                return true;
            }
        }
    }
}
