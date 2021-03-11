using System.Linq;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEngine;

namespace Generation.Editor
{
    [CustomEditor(typeof(MapGenerator))]
    public class MapGeneratorEditor : UnityEditor.Editor
    {
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
        }
    }
}