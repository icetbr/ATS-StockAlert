using Eremite.Buildings.UI;
using Eremite.Characters;
using HarmonyLib;
using StockAlert.Config;
using TMPro;
using UnityEngine;

namespace StockAlert.Infrastructure.Hooks
{
    [HarmonyPatch(typeof(WorkerStatusSlot), "SetUpIcon")]
    internal static class WarehouseDeliveryStatusIconPatch
    {
        private const string ArrowName = "arrow_green_right";
        private static Sprite _arrow;
        private static float _nextSpriteSearch;

        [HarmonyPrefix]
        private static void Prefix(Actor ___actor, ref Sprite sprite)
        {
            if (!ConfigManager.ShowWarehouseDeliveryStatusIcon || ___actor == null ||
                !IsWarehouseDelivery(___actor.ActorState))
            {
                return;
            }

            var arrow = GetArrow();
            if (arrow != null)
            {
                sprite = arrow;
            }
        }

        internal static bool IsWarehouseDelivery(ActorState state)
        {
            if (state == null || !state.isAlive || state.carriedGood.IsNone() ||
                state.tasksStack == null || state.tasksStack.Count == 0)
            {
                return false;
            }

            // The top task is the current trip, rather than the worker's overall job.
            var task = state.tasksStack.Peek();
            return task != null && (task.type == ActorTaskType.GoToNearestStorage ||
                task.type == ActorTaskType.ReturnWithRelicGoods ||
                task.type == ActorTaskType.StoreRequestedProduct);
        }

        private static Sprite GetArrow()
        {
            if (_arrow != null && _arrow.texture != null)
            {
                return _arrow;
            }
            if (Time.unscaledTime < _nextSpriteSearch)
            {
                return null;
            }
            _nextSpriteSearch = Time.unscaledTime + 5f;

            _arrow = FindArrow(TMP_Settings.defaultSpriteAsset);
            if (_arrow == null)
            {
                foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_SpriteAsset>())
                {
                    _arrow = FindArrow(asset);
                    if (_arrow != null)
                    {
                        break;
                    }
                }
            }
            return _arrow;
        }

        private static Sprite FindArrow(TMP_SpriteAsset asset)
        {
            if (asset == null)
            {
                return null;
            }
            // This TMP version's name helper uses a different hash from its lookup table.
            var index = asset.GetSpriteIndexFromHashcode(TMP_TextUtilities.GetHashCode(ArrowName));
            if (index < 0 || index >= asset.spriteCharacterTable.Count)
            {
                return null;
            }
            var glyph = asset.spriteCharacterTable[index].glyph as TMP_SpriteGlyph;
            if (glyph == null)
            {
                return null;
            }
            if (glyph.sprite != null)
            {
                return glyph.sprite;
            }
            var texture = asset.spriteSheet as Texture2D;
            var rect = glyph.glyphRect;
            if (texture == null || rect.width <= 0 || rect.height <= 0)
            {
                return null;
            }

            // Reuse the game's atlas region; no new artwork or texture is generated.
            var sprite = Sprite.Create(texture, new Rect(rect.x, rect.y, rect.width, rect.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "StockAlertWarehouseDeliveryArrow";
            return sprite;
        }
    }
}
