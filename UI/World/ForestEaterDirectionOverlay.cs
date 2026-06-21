using System;
using System.Collections.Generic;
using System.Linq;
using Eremite;
using StockAlert.Config;
using StockAlert.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace StockAlert.UI.World
{
    internal static class ForestEaterDirectionOverlay
    {
        private const float LineHeight = 1.25f;
        private const float LineWidth = 0.18f;
        private const float ArrowLength = 0.55f;
        private const float ArrowWidth = 0.35f;

        private static readonly Dictionary<int, PathMarker> Markers = new Dictionary<int, PathMarker>();
        private static Material _lineMaterial;

        public static void Refresh()
        {
            if (!ConfigManager.ShowForestEaterDirection || !GameAPI.IsGameActive())
            {
                Clear();
                return;
            }

            var directions = GameAPI.GetForestEaterDirections();
            var seen = new HashSet<int>();
            foreach (var direction in directions)
            {
                if (direction == null || !TryBuildPath(direction, out var points))
                {
                    continue;
                }

                seen.Add(direction.Index);
                if (!Markers.TryGetValue(direction.Index, out var marker) || marker == null)
                {
                    marker = new PathMarker(direction.Index, GetLineMaterial());
                    Markers[direction.Index] = marker;
                }

                marker.SetPath(points);
            }

            foreach (var staleKey in Markers.Keys.Where(key => !seen.Contains(key)).ToList())
            {
                Markers[staleKey]?.Destroy();
                Markers.Remove(staleKey);
            }
        }

        public static void Clear()
        {
            foreach (var marker in Markers.Values)
            {
                marker?.Destroy();
            }

            Markers.Clear();
        }

        private static bool TryBuildPath(GameAPI.ForestEaterDirectionInfo direction, out List<Vector3> points)
        {
            points = new List<Vector3>();
            if (direction == null)
            {
                return false;
            }

            var path = direction.CurrentPath;
            if (path != null && path.Count > 0)
            {
                var currentIndex = path.IndexOf(direction.Current);
                if (currentIndex < 0)
                {
                    currentIndex = 0;
                }

                for (var i = currentIndex; i < path.Count; i++)
                {
                    AddPoint(points, path[i]);
                }
            }

            if (points.Count == 0)
            {
                AddPoint(points, direction.Current);
                AddPoint(points, direction.Target);
            }
            else if ((points[points.Count - 1] - ToWorld(direction.Target)).sqrMagnitude > 0.05f)
            {
                AddPoint(points, direction.Target);
            }

            return points.Count >= 2 && (points[0] - points[points.Count - 1]).sqrMagnitude > 0.05f;
        }

        private static void AddPoint(List<Vector3> points, Vector2Int field)
        {
            var point = ToWorld(field);
            if (points.Count == 0 || (points[points.Count - 1] - point).sqrMagnitude > 0.05f)
            {
                points.Add(point);
            }
        }

        private static Vector3 ToWorld(Vector2Int field)
        {
            return field.ToWorld() + new Vector3(0.5f, LineHeight, 0.5f);
        }

        private static Material GetLineMaterial()
        {
            if (_lineMaterial != null)
            {
                return _lineMaterial;
            }

            var shader = Shader.Find("GUI/Text Shader") ?? Shader.Find("Sprites/Default");
            _lineMaterial = new Material(shader)
            {
                name = "StockAlertForestEaterDirectionMaterial",
                renderQueue = (int)RenderQueue.Overlay
            };
            _lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            _lineMaterial.SetInt("_ZWrite", 0);
            _lineMaterial.color = new Color(1f, 0.9f, 0.15f, 0.9f);
            return _lineMaterial;
        }

        private sealed class PathMarker
        {
            private readonly GameObject _root;
            private readonly LineRenderer _pathLine;
            private readonly LineRenderer _leftHead;
            private readonly LineRenderer _rightHead;

            public PathMarker(int index, Material material)
            {
                _root = new GameObject("StockAlertForestEaterDirection_" + index);
                _pathLine = CreateLine("Path", material, LineWidth);
                _leftHead = CreateLine("ArrowLeft", material, LineWidth);
                _rightHead = CreateLine("ArrowRight", material, LineWidth);
            }

            public void SetPath(List<Vector3> points)
            {
                if (_root == null || points == null || points.Count < 2)
                {
                    return;
                }

                _root.SetActive(true);
                _pathLine.positionCount = points.Count;
                _pathLine.SetPositions(points.ToArray());
                SetArrowHead(points);
            }

            public void Destroy()
            {
                if (_root != null)
                {
                    UnityEngine.Object.Destroy(_root);
                }
            }

            private LineRenderer CreateLine(string name, Material material, float width)
            {
                var child = new GameObject(name);
                child.transform.SetParent(_root.transform, false);
                var line = child.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.material = material;
                line.startColor = new Color(1f, 0.9f, 0.15f, 0.92f);
                line.endColor = new Color(1f, 0.75f, 0.05f, 0.92f);
                line.startWidth = width;
                line.endWidth = width;
                line.numCapVertices = 4;
                line.numCornerVertices = 4;
                line.textureMode = LineTextureMode.Stretch;
                return line;
            }

            private void SetArrowHead(List<Vector3> points)
            {
                var start = points[0];
                var end = points.Count > 1 ? points[1] : points[points.Count - 1];
                var direction = end - start;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.001f)
                {
                    direction = points[points.Count - 1] - points[0];
                    direction.y = 0f;
                }

                if (direction.sqrMagnitude < 0.001f)
                {
                    _leftHead.positionCount = 0;
                    _rightHead.positionCount = 0;
                    return;
                }

                direction.Normalize();
                var side = new Vector3(-direction.z, 0f, direction.x);
                var tip = end;
                var back = tip - direction * ArrowLength;
                _leftHead.positionCount = 2;
                _leftHead.SetPosition(0, tip);
                _leftHead.SetPosition(1, back + side * ArrowWidth);
                _rightHead.positionCount = 2;
                _rightHead.SetPosition(0, tip);
                _rightHead.SetPosition(1, back - side * ArrowWidth);
            }
        }
    }
}
