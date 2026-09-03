using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Custom inspector for <see cref="RingSectorGraphic"/>.
    /// Because RingSectorGraphic inherits from Image, Unity's default ImageEditor
    /// only draws the base Image fields and hides any subclass [SerializeField] members.
    /// This editor draws the standard Image inspector first, then appends all
    /// RingSectorGraphic-specific fields (hit-test parameters and state sprites).
    ///
    /// <para>
    /// Also draws a wireframe visualization of the ring-sector hit area in the Scene view
    /// when the component is selected, making it easy to verify hit-test coverage.
    /// </para>
    /// </summary>
    [CustomEditor(typeof(RingSectorGraphic), true)]
    [CanEditMultipleObjects]
    public class RingSectorGraphicEditor : ImageEditor
    {
        // Ring Sector Hit-Test
        private SerializedProperty innerRadius;
        private SerializedProperty outerRadius;
        private SerializedProperty sectorCenterDegrees;
        private SerializedProperty sectorAngleSize;
        private SerializedProperty useSectorAngleCheck;
        private SerializedProperty ringCenter;

        // State Sprites
        private SerializedProperty idleSprite;
        private SerializedProperty suspendedSprite;
        private SerializedProperty selectedSprite;

        // Linked Panel
        private SerializedProperty linkedPanel;

        /// <summary>
        /// Number of line segments used to approximate each arc when drawing the sector gizmo.
        /// Higher values produce smoother arcs.
        /// </summary>
        private const int ArcSegments = 64;

        /// <summary>
        /// Color of the sector outline in the Scene view.
        /// </summary>
        private static readonly Color SectorOutlineColor = new Color(0f, 1f, 0.4f, 0.9f);

        /// <summary>
        /// Color of the semi-transparent fill in the Scene view.
        /// </summary>
        private static readonly Color SectorFillColor = new Color(0f, 1f, 0.4f, 0.12f);

        protected override void OnEnable()
        {
            base.OnEnable();

            // Ring Sector Hit-Test
            innerRadius = serializedObject.FindProperty("innerRadius");
            outerRadius = serializedObject.FindProperty("outerRadius");
            sectorCenterDegrees = serializedObject.FindProperty("sectorCenterDegrees");
            sectorAngleSize = serializedObject.FindProperty("sectorAngleSize");
            useSectorAngleCheck = serializedObject.FindProperty("useSectorAngleCheck");
            ringCenter = serializedObject.FindProperty("ringCenter");

            // State Sprites
            idleSprite = serializedObject.FindProperty("idleSprite");
            suspendedSprite = serializedObject.FindProperty("suspendedSprite");
            selectedSprite = serializedObject.FindProperty("selectedSprite");

            // Linked Panel
            linkedPanel = serializedObject.FindProperty("linkedPanel");
        }

        public override void OnInspectorGUI()
        {
            // Draw the default Image inspector (Source Image, Color, Material, etc.)
            base.OnInspectorGUI();

            serializedObject.Update();

            EditorGUILayout.Space();

            // --- Ring Sector Hit-Test ---
            EditorGUILayout.LabelField("Ring Sector Hit-Test", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(innerRadius);
            EditorGUILayout.PropertyField(outerRadius);
            EditorGUILayout.PropertyField(sectorCenterDegrees);
            EditorGUILayout.PropertyField(sectorAngleSize);
            EditorGUILayout.PropertyField(useSectorAngleCheck);
            EditorGUILayout.PropertyField(ringCenter);

            EditorGUILayout.Space();

            // --- State Sprites ---
            EditorGUILayout.LabelField("State Sprites", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(idleSprite);
            EditorGUILayout.PropertyField(suspendedSprite);
            EditorGUILayout.PropertyField(selectedSprite);

            EditorGUILayout.Space();

            // --- Linked Panel ---
            EditorGUILayout.LabelField("Linked Panel", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(linkedPanel);

            serializedObject.ApplyModifiedProperties();
        }

        // -----------------------------------------------------------------
        // Scene view gizmo — draws the ring-sector hit area
        // -----------------------------------------------------------------

        private void OnSceneGUI()
        {
            RingSectorGraphic sector = target as RingSectorGraphic;
            if (sector == null) return;

            serializedObject.Update();

            float inner = innerRadius.floatValue;
            float outer = outerRadius.floatValue;
            float centerDeg = sectorCenterDegrees.floatValue;
            float sizeDeg = sectorAngleSize.floatValue;
            bool useAngle = useSectorAngleCheck.boolValue;

            // Determine the ring center RectTransform.
            RectTransform centerRT = ringCenter.objectReferenceValue as RectTransform;
            if (centerRT == null) centerRT = sector.rectTransform;

            // We draw in the local space of the ring center RectTransform.
            // Handles.matrix transforms local coordinates to world coordinates.
            Matrix4x4 originalMatrix = Handles.matrix;
            Handles.matrix = centerRT.localToWorldMatrix;

            if (useAngle && sizeDeg < 360f)
            {
                // Draw a sector (arc wedge) shape.
                DrawSector(inner, outer, centerDeg, sizeDeg);
            }
            else
            {
                // No angular constraint — draw full annulus (two concentric circles).
                DrawFullAnnulus(inner, outer);
            }

            Handles.matrix = originalMatrix;
        }

        /// <summary>
        /// Draws a ring-sector shape: inner arc, outer arc, and two radial edges,
        /// plus a semi-transparent fill.
        /// </summary>
        private static void DrawSector(float inner, float outer, float centerDeg, float sizeDeg)
        {
            float halfSize = sizeDeg * 0.5f;
            float startDeg = centerDeg - halfSize;
            float endDeg = centerDeg + halfSize;

            // Compute arc points.
            Vector3[] outerArc = ComputeArc(outer, startDeg, endDeg, ArcSegments);
            Vector3[] innerArc = ComputeArc(inner, startDeg, endDeg, ArcSegments);

            // --- Fill (semi-transparent) ---
            Handles.color = SectorFillColor;
            // Build a triangle fan for the filled region.
            int totalVerts = outerArc.Length + innerArc.Length;
            Vector3[] fillVerts = new Vector3[totalVerts];
            // Outer arc forward, inner arc reversed to form a closed ring-sector polygon.
            for (int i = 0; i < outerArc.Length; i++)
                fillVerts[i] = outerArc[i];
            for (int i = 0; i < innerArc.Length; i++)
                fillVerts[outerArc.Length + i] = innerArc[innerArc.Length - 1 - i];

            Handles.DrawAAConvexPolygon(fillVerts);

            // --- Outline ---
            Handles.color = SectorOutlineColor;

            // Outer arc
            Handles.DrawAAPolyLine(2f, outerArc);
            // Inner arc
            Handles.DrawAAPolyLine(2f, innerArc);

            // Two radial edges connecting inner and outer arcs.
            Vector3 startOuter = PointOnCircle(outer, startDeg);
            Vector3 startInner = PointOnCircle(inner, startDeg);
            Vector3 endOuter = PointOnCircle(outer, endDeg);
            Vector3 endInner = PointOnCircle(inner, endDeg);

            Handles.DrawAAPolyLine(2f, startInner, startOuter);
            Handles.DrawAAPolyLine(2f, endInner, endOuter);
        }

        /// <summary>
        /// Draws a full annulus (two concentric circles) when angular check is disabled.
        /// </summary>
        private static void DrawFullAnnulus(float inner, float outer)
        {
            Handles.color = SectorOutlineColor;

            Vector3[] outerCircle = ComputeArc(outer, 0f, 360f, ArcSegments);
            Vector3[] innerCircle = ComputeArc(inner, 0f, 360f, ArcSegments);

            Handles.DrawAAPolyLine(2f, outerCircle);
            Handles.DrawAAPolyLine(2f, innerCircle);
        }

        /// <summary>
        /// Computes an array of points along a circular arc on the XY plane (Z=0).
        /// </summary>
        private static Vector3[] ComputeArc(float radius, float startDeg, float endDeg, int segments)
        {
            // +1 because we need both endpoints.
            Vector3[] points = new Vector3[segments + 1];
            float step = (endDeg - startDeg) / segments;
            for (int i = 0; i <= segments; i++)
            {
                float deg = startDeg + step * i;
                points[i] = PointOnCircle(radius, deg);
            }
            return points;
        }

        /// <summary>
        /// Returns a point on a circle at the given angle (degrees) and radius, on the XY plane.
        /// 0° = +X, 90° = +Y (standard math convention).
        /// </summary>
        private static Vector3 PointOnCircle(float radius, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0f);
        }
    }
}
