using System.Collections.Generic;
using StockAlert.Config;
using StockAlert.Game;
using StockAlert.Infrastructure.Plugin;
using UnityEngine;
using UnityEngine.Rendering;

namespace StockAlert.UI.World
{
    internal static class BuildingPlacementGrid
    {
        private const float GridHeight = 0.055f;
        private const float LineWidth = 0.018f;

        private static GameObject _root;
        private static Mesh _gridMesh;
        private static Material _gridMaterial;
        private static int _mapWidth;
        private static int _mapHeight;

        public static void Refresh()
        {
            if (!ConfigManager.ShowBuildingPlacementGrid || !IsPlacementModeActive())
            {
                Clear();
                return;
            }

            if (!GameAPI.TryGetMapSize(out var width, out var height))
            {
                Clear();
                return;
            }

            EnsureGrid(width, height);
            if (_root == null)
            {
                return;
            }

            if (!_root.activeSelf)
            {
                _root.SetActive(true);
            }
        }

        public static void Clear()
        {
            if (_root != null && _root.activeSelf)
            {
                _root.SetActive(false);
            }
        }

        private static bool IsPlacementModeActive()
        {
            return GameAPI.IsBuildingPlacementActive();
        }

        private static void EnsureGrid(int width, int height)
        {
            if (_root != null && _mapWidth == width && _mapHeight == height)
            {
                return;
            }

            DestroyGrid();

            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Transparent") ??
                         Shader.Find("GUI/Text Shader");
            if (shader == null)
            {
                Plugin.Log("BuildingPlacementGrid: no compatible transparent shader found");
                return;
            }

            _mapWidth = width;
            _mapHeight = height;
            _root = new GameObject("StockAlertBuildingPlacementGrid");
            _root.transform.position = new Vector3(0f, GridHeight, 0f);

            _gridMesh = CreateGridMesh(width, height);
            _gridMesh.name = "StockAlertBuildingPlacementGridMesh";
            _gridMesh.hideFlags = HideFlags.DontSave;

            _gridMaterial = new Material(shader)
            {
                name = "StockAlertBuildingPlacementGridMaterial",
                color = new Color(1f, 0.82f, 0.32f, 0.34f),
                renderQueue = (int)RenderQueue.Transparent + 20,
                hideFlags = HideFlags.DontSave
            };
            ConfigureTransparentMaterial(_gridMaterial);

            var filter = _root.AddComponent<MeshFilter>();
            filter.sharedMesh = _gridMesh;
            var renderer = _root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _gridMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        }

        private static Mesh CreateGridMesh(int width, int height)
        {
            var vertices = new List<Vector3>((width + height + 2) * 4);
            var triangles = new List<int>((width + height + 2) * 6);
            var halfWidth = LineWidth * 0.5f;

            for (var x = 0; x <= width; x++)
            {
                AddQuad(
                    vertices,
                    triangles,
                    new Vector3(x - halfWidth, 0f, 0f),
                    new Vector3(x + halfWidth, 0f, 0f),
                    new Vector3(x + halfWidth, 0f, height),
                    new Vector3(x - halfWidth, 0f, height));
            }

            for (var z = 0; z <= height; z++)
            {
                AddQuad(
                    vertices,
                    triangles,
                    new Vector3(0f, 0f, z - halfWidth),
                    new Vector3(width, 0f, z - halfWidth),
                    new Vector3(width, 0f, z + halfWidth),
                    new Vector3(0f, 0f, z + halfWidth));
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d)
        {
            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start);
            triangles.Add(start + 3);
            triangles.Add(start + 2);
        }

        private static void DestroyGrid()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }

            if (_gridMesh != null)
            {
                Object.Destroy(_gridMesh);
            }

            if (_gridMaterial != null)
            {
                Object.Destroy(_gridMaterial);
            }

            _root = null;
            _gridMesh = null;
            _gridMaterial = null;
            _mapWidth = 0;
            _mapHeight = 0;
        }
    }
}
