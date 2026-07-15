// ═══════════════════════════════════════════════════════════════════════════════
// 2.3  PLACEMENT CONFIG
// ═══════════════════════════════════════════════════════════════════════════════
using UnityEngine;

namespace TheForest.Building.Config
{
    [CreateAssetMenu(fileName = "PlacementConfig", menuName = "The Forest/Building/Placement Config")]
    public class PlacementConfig : ScriptableObject
    {
        [Header("Snap System")]
        public float snapRadius = 0.8f;
        public float minPlacementDistance = 0.05f;

        [Header("Terrain Limits")]
        public float maxGroundAngle = 45f;
        public float maxTerrainSlope = 60f;
        public LayerMask terrainLayer = ~0;
        public LayerMask obstacleMask = ~0;

        [Header("Log Dimensions (meters)")]
        public float fullLogLength = 3.0f;
        public float logRadius = 0.20f;
        public float wallLogHeight = 0.35f;  // vertical step per stacked log
        public float palisadeSpacing = 0.42f;  // gap between palisade logs

        [Header("Rotation")]
        public float rotationStep = 22.5f;  // degrees per Q/R tap

        [Header("Ghost Visual")]
        public float ghostAlpha = 0.50f;
        public Color ghostValidColor = new Color(1.0f, 1.0f, 1.0f, 0.50f);
        public Color ghostInvalidColor = new Color(1.0f, 0.2f, 0.2f, 0.50f);
        public string ghostShaderAlphaParam = "_Alpha";
    }
}