using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// 2.1  INDICATOR CONFIG
// ═══════════════════════════════════════════════════════════════════════════════
namespace TheForest.Building.Config
{
    [CreateAssetMenu(fileName = "IndicatorConfig", menuName = "The Forest/Building/Indicator Config")]
    public class IndicatorConfig : ScriptableObject
    {
        [Header("Raycasting")]
        public float maxRaycastDistance = 5f;
        public float snapDetectionRadius = 0.8f;
        public LayerMask groundLayer = ~0;
        public LayerMask logLayer;

        [Header("Indicator Prefabs")]
        public GameObject horizontalPrefab;   // white rectangle
        public GameObject verticalPrefab;     // white circle + dot
        public GameObject snapArrowPrefab;    // white arrow (snap direction)
        public GameObject connectionDashPrefab; // white dashed connector
        public GameObject diagonalArrowPrefab;
        public GameObject cutMarkWidthPrefab; // red dashed horizontal
        public GameObject cutMarkSplitPrefab; // red vertical line
        public GameObject invalidPrefab;      // red silhouette
        public GameObject strutPrefab;        // L-shape at corner
        public GameObject stairsPrefab;       // wavy arrow
        public GameObject doorCutPrefab;      // red column

        [Header("HDRP Decal Materials")]
        [Tooltip("Assign HDRP Decal material for surface-conforming indicators")]
        public Material decalValidMaterial;
        public Material decalInvalidMaterial;

        [Header("Update Rate")]
        [Tooltip("How many times per second the indicator raycasts")]
        public float updateRate = 30f;

        [Header("Angle Thresholds (degrees below camera horizon)")]
        [Tooltip("Camera pitch where horizontal mode activates")]
        public float horizontalModeAngle = 30f;
        [Tooltip("Camera pitch where vertical mode activates")]
        public float verticalModeAngle = 70f;
    }
}