using UnityEngine;
using UnityEditor;

namespace PetGame
{
    /// <summary>
    /// Custom editor for ProjectileSkillEffectData.
    /// Displays a warning if projectilePrefab is not assigned,
    /// and draws a wire disc gizmo in the Scene view to visualize the explosion radius.
    /// </summary>
    [CustomEditor(typeof(ProjectileSkillEffectData))]
    public class ProjectileSkillEffectDataEditor : Editor
    {
        private SerializedProperty projectilePrefab;
        private SerializedProperty projectileFlightDuration;
        private SerializedProperty projectileArcHeight;
        private SerializedProperty explosionRadius;

        private void OnEnable()
        {
            projectilePrefab = serializedObject.FindProperty("projectilePrefab");
            projectileFlightDuration = serializedObject.FindProperty("projectileFlightDuration");
            projectileArcHeight = serializedObject.FindProperty("projectileArcHeight");
            explosionRadius = serializedObject.FindProperty("explosionRadius");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // --- Projectile Settings ---
            EditorGUILayout.LabelField("Projectile Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(projectilePrefab, new GUIContent("Projectile Prefab",
                "Prefab for the projectile. Must have SpriteRenderer, Animator, and ProjectileController components."));

            // Show warning if prefab is not assigned
            if (projectilePrefab.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Projectile Prefab is not assigned! The skill will not work without a prefab.\n" +
                    "Create one via: Tools > PetGame > Create Projectile Prefab",
                    MessageType.Warning);
            }
            else
            {
                // Validate that the prefab has required components
                GameObject prefab = projectilePrefab.objectReferenceValue as GameObject;
                if (prefab != null)
                {
                    bool hasController = prefab.GetComponent<ProjectileController>() != null;
                    bool hasSpriteRenderer = prefab.GetComponent<SpriteRenderer>() != null;
                    bool hasAnimator = prefab.GetComponent<Animator>() != null;

                    if (!hasController || !hasSpriteRenderer || !hasAnimator)
                    {
                        string missing = "";
                        if (!hasController) missing += "ProjectileController, ";
                        if (!hasSpriteRenderer) missing += "SpriteRenderer, ";
                        if (!hasAnimator) missing += "Animator, ";
                        missing = missing.TrimEnd(',', ' ');

                        EditorGUILayout.HelpBox(
                            $"Prefab is missing required components: {missing}",
                            MessageType.Error);
                    }
                }
            }

            EditorGUILayout.PropertyField(projectileFlightDuration, new GUIContent("Flight Duration (s)",
                "Total flight time from launch to landing."));

            EditorGUILayout.PropertyField(projectileArcHeight, new GUIContent("Arc Height",
                "Peak height of the parabolic arc above the start-end line."));

            EditorGUILayout.Space(10);

            // --- Explosion Settings ---
            EditorGUILayout.LabelField("Explosion Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(explosionRadius, new GUIContent("Explosion Radius",
                "Radius of the AOE damage circle at the landing point."));

            // Visual indicator of the radius value
            if (explosionRadius.floatValue > 0f)
            {
                EditorGUILayout.HelpBox(
                    $"Explosion radius: {explosionRadius.floatValue:F2} units (diameter: {explosionRadius.floatValue * 2f:F2} units)\n" +
                    "A circle gizmo will be drawn in the Scene view when a ProjectileController is selected.",
                    MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
