// ═══════════════════════════════════════════════════════════════════════════════
// 2.6  STRUCTURAL CONFIG
// ═══════════════════════════════════════════════════════════════════════════════
using UnityEngine;

namespace TheForest.Building.Config
{
    [CreateAssetMenu(fileName = "StructuralConfig", menuName = "The Forest/Building/Structural Config")]
    public class StructuralConfig : ScriptableObject
    {
        [Header("Strut System")]
        [Tooltip("Max span a single strut pair can bridge (in log-unit count)")]
        public int maxStrutSpan = 5;
        [Tooltip("How many middle columns can be removed per strut pair")]
        public int maxColumnsPerStrut = 3;
        public float strutCornerDetectionRadius = 0.60f;
        public float strutPlacementAngleTolerance = 15f;   // degrees

        [Header("Stacking")]
        public int standardWallHeight = 6;     // logs for a standard wall
        public int maxStackHeight = 20;

        [Header("Foundation")]
        public int pillarFoundationCorners = 4;
        public float minSplitLogsForRainBlock = 3f;    // split logs on one frame

        [Header("Support Detection")]
        [Tooltip("Sphere radius to detect what a newly placed piece rests on")]
        public float supportDetectionRadius = 0.35f;
        public LayerMask buildingPieceLayer;
    }
}