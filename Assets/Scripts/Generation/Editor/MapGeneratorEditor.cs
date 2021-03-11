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
            if (GUILayout.Button("Generate"))
                ((MapGenerator) target).Generate();

            // Force a gizmo redraw
            SceneView.lastActiveSceneView.Repaint();
            base.OnInspectorGUI();
        }
    }
}