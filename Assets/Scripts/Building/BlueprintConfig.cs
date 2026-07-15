// ═══════════════════════════════════════════════════════════════════════════════
// 2.5  BLUEPRINT CONFIG
// ═══════════════════════════════════════════════════════════════════════════════
namespace TheForest.Building.Config
{
    using TheForest.Building.Data;
    using UnityEngine;

    [CreateAssetMenu(fileName = "BlueprintConfig", menuName = "The Forest/Building/Blueprint Config")]
    public class BlueprintConfig : ScriptableObject
    {
        [Header("Ghost")]
        public Material ghostMaterialValid;
        public Material ghostMaterialInvalid;
        public float ghostCheckRadius = 0.30f;

        [Header("Placement")]
        public float blueprintRotStep = 90f;
        public LayerMask overlapCheckMask;

        [Header("Material Range")]
        [Tooltip("Max distance to add materials to blueprint ghost")]
        public float materialAddRange = 3.0f;

        [Header("Guide Book")]
        public KeyCode guideBookKey = KeyCode.B;
        public KeyCode modeToggleKey = KeyCode.X;
        public KeyCode rotateCCWKey = KeyCode.Q;
        public KeyCode rotateCWKey = KeyCode.R;

        [Header("Registry")]
        public BlueprintData[] defaultBlueprints;  // 23 standard
        public BlueprintData[] hiddenBlueprints;   // 16 hidden (locked by default)
    }
}