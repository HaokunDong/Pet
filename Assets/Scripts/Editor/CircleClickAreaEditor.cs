using UnityEditor;
using UnityEditor.UI;
using PetGame.UI;

/// <summary>
/// Custom Inspector for CircleClickArea.
/// Extends the default Image editor so that the additional clickRadius field
/// is visible in the Inspector alongside the standard Image properties.
/// </summary>
[CustomEditor(typeof(CircleClickArea), true)]
[CanEditMultipleObjects]
public class CircleClickAreaEditor : ImageEditor
{
    private SerializedProperty clickRadiusProp;

    protected override void OnEnable()
    {
        base.OnEnable();
        clickRadiusProp = serializedObject.FindProperty("clickRadius");
    }

    public override void OnInspectorGUI()
    {
        // Draw the default Image inspector
        base.OnInspectorGUI();

        // Draw the custom CircleClickArea fields
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Circle Click Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(clickRadiusProp, new UnityEngine.GUIContent(
            "Click Radius (px)",
            "Pixel radius of the clickable circle area. Set to 0 for auto mode (half of the smallest RectTransform dimension)."));
        if (clickRadiusProp.floatValue < 0f)
            clickRadiusProp.floatValue = 0f;

        serializedObject.ApplyModifiedProperties();
    }
}
