using UnityEngine;
using UnityEditor;

namespace PetGame
{
    /// <summary>
    /// Custom Inspector for CharacterData ScriptableObject.
    /// Draws the default inspector with a note about viewing attack distance in Scene view.
    /// </summary>
    [CustomEditor(typeof(CharacterData))]
    public class CharacterDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector fields
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Attack distance is visualized in the Scene view via Gizmos.\n" +
                "Select the character in the scene to see the attack range drawn as a red rectangle in the facing direction.\n" +
                "Green circle = Min Attack Distance.",
                MessageType.Info);

            // Force repaint when values change
            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }
    }
}
