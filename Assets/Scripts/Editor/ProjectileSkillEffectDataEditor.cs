using UnityEngine;
using UnityEditor;

namespace PetGame
{
    /// <summary>
    /// Custom editor for ProjectileSkillEffectData.
    /// Displays a warning if projectilePrefab is not assigned,
    /// and validates that the prefab has required components.
    /// </summary>
    [CustomEditor(typeof(ProjectileSkillEffectData))]
    public class ProjectileSkillEffectDataEditor : Editor
    {
        private SerializedProperty projectilePrefab;
        private SerializedProperty projectileFlightDuration;
        private SerializedProperty projectileArcHeight;

        private void OnEnable()
        {
            projectilePrefab = serializedObject.FindProperty("projectilePrefab");
            projectileFlightDuration = serializedObject.FindProperty("projectileFlightDuration");
            projectileArcHeight = serializedObject.FindProperty("projectileArcHeight");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // --- Projectile Settings ---
            EditorGUILayout.LabelField("Projectile Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(projectilePrefab, new GUIContent("Projectile Prefab",
                "Prefab for the projectile. Must have SpriteRenderer, Animator, ProjectileController, and ProjectileDamageArea components."));

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
                    bool hasDamageArea = prefab.GetComponent<ProjectileDamageArea>() != null;

                    if (!hasController || !hasSpriteRenderer || !hasAnimator || !hasDamageArea)
                    {
                        string missing = "";
                        if (!hasController) missing += "ProjectileController, ";
                        if (!hasDamageArea) missing += "ProjectileDamageArea, ";
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

            // --- Info about damage configuration ---
            EditorGUILayout.HelpBox(
                "Damage area and trigger mode are now configured on the projectile prefab's ProjectileDamageArea component.\n" +
                "Select the prefab to configure damage shapes and trigger mode (AnimationEvent or OnCollision).",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
