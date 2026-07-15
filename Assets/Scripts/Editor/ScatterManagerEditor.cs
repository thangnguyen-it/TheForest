using UnityEditor;
using UnityEngine;
using TheForest.World;

namespace TheForest.EditorTools
{
    [CustomEditor(typeof(ScatterManager))]
    public class ScatterManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var manager = (ScatterManager)target;
            EditorGUILayout.Space();

            if (GUILayout.Button("Generate (Rải cây)", GUILayout.Height(32)))
            {
                manager.Generate();
                EditorUtility.SetDirty(manager); // đánh dấu scene cần lưu
            }

            if (GUILayout.Button("Clear (Xóa hết)"))
            {
                manager.Clear();
                EditorUtility.SetDirty(manager);
            }
        }
    }
}
