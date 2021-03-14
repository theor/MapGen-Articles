using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Generation.Editor
{
    [CustomEditor(typeof(MapGenerator))]
    public class MapGeneratorEditor : UnityEditor.Editor
    {

        [SerializeField] private bool _showTriData = true;

        [SerializeField] private int _pointsPage, _triPage;
        [SerializeField] private bool _hideDeleted;
        public override void OnInspectorGUI()
        {
            var mapGenerator = (MapGenerator) target;
            if (GUILayout.Button("Generate"))
            {
                mapGenerator.Generate();
            }

            if (mapGenerator.Storage.IsCreated)
            {
                var deletedTris = 0;
                for (var index = 0; index < mapGenerator.Storage.Triangles.Length; index++)
                    if (mapGenerator.Storage.Triangles[index].IsDeleted)
                        deletedTris++;

                var tris = mapGenerator.Storage.Triangles.Length;
                EditorGUILayout.LabelField("Deleted triangles:", $"{deletedTris} / {tris}, {deletedTris / (float)tris * 100}%");
            }

            // Force a gizmo redraw
            SceneView.lastActiveSceneView.Repaint();
            base.OnInspectorGUI();

            _showTriData = EditorGUILayout.Toggle("Show data", _showTriData);
            if (mapGenerator.Storage.Points.IsCreated && mapGenerator.Tridata.IsCreated)
            {
                Paginate("Triangles", ref _triPage, mapGenerator.Storage.Triangles.Length, i =>
                {
                    var t = mapGenerator.Storage.Triangles[i];
                    if (_hideDeleted && t.IsDeleted)
                        return;
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button($"{(i == mapGenerator.GizmosOptions._selectedTriangle ? "-" : " ")} {i}",
                        EditorStyles.miniButton))
                    {
                        mapGenerator.GizmosOptions._selectedTriangle = i;
                        mapGenerator.GizmosOptions._selectedPoint = -1;
                    }

                    if (_showTriData)
                    {
                        GUILayout.Label(
                            $"H{(mapGenerator.Tridata.IsCreated ? mapGenerator.Tridata[i].Height.ToString("F2") : "")}");
                        GUILayout.Label(
                            $"C{(mapGenerator.Tridata.IsCreated ? mapGenerator.Tridata[i].Centroid.ToString("F2", CultureInfo.InvariantCulture) : "")}");
                        // GUILayout.Label(
                            // $"S{(mapGenerator.Tridata.IsCreated ? mapGenerator.Tridata[i].TriSlope.ToString("F2", CultureInfo.InvariantCulture) : "")}");
                    }
                    else
                    {
                        GUILayout.Label($"{t.V1}");
                        GUILayout.Label($"{t.V2}");
                        GUILayout.Label($"{t.V3}");
                        GUILayout.Label($"{(t.IsDeleted ? "D" : "")}");
                        if (GUILayout.Button($"{t.T1}"))
                            mapGenerator.GizmosOptions._selectedTriangle = t.T1;
                        if (GUILayout.Button($"{t.T2}"))
                            mapGenerator.GizmosOptions._selectedTriangle = t.T2;
                        if (GUILayout.Button($"{t.T3}"))
                            mapGenerator.GizmosOptions._selectedTriangle = t.T3;
                    }

                    EditorGUILayout.EndHorizontal();
                });
                Paginate("Points", ref _pointsPage, mapGenerator.Storage.Points.Length, index =>
                {
                    var pVoronoi = mapGenerator.Storage.Points[index];
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(index.ToString()))
                        mapGenerator.GizmosOptions._selectedPoint = index;
                    pVoronoi.Position =
                        EditorGUILayout.Vector2Field(pVoronoi.TriangleIndex.ToString(), pVoronoi.Position);
                    EditorGUILayout.EndHorizontal();
                });
            }
        }
        
        void Paginate(string title, ref int currentPage, int count, Action<int> drawItem)
        {
            const int pageLength = 30;
            int pageCount = (count / pageLength) + 1;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(title);
            if (GUILayout.Button("<", EditorStyles.toolbarButton))
                currentPage = (currentPage + pageCount - 1) % pageCount;
            if (GUILayout.Button(">", EditorStyles.toolbarButton))
                currentPage = (currentPage + 1) % pageCount;
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel++;
            for (int i = currentPage * pageLength; i < (currentPage + 1) * pageLength && i < count; i++)
            {
                drawItem(i);
            }

            EditorGUI.indentLevel--;
        }

    }
}