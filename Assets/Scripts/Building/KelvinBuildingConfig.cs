// ═══════════════════════════════════════════════════════════════════════════════
// 2.14  KELVIN BUILDING CONFIG
// ═══════════════════════════════════════════════════════════════════════════════
using UnityEngine;

namespace TheForest.Building.Config
{
    [CreateAssetMenu(fileName = "KelvinBuildingConfig", menuName = "The Forest/Building/Kelvin Config")]
    public class KelvinBuildingConfig : ScriptableObject
    {
        [Header("Log Fetching")]
        public int logsPerBasicTrip = 2;
        public int logsPerSledTrip = 12;
        public float logSearchRadius = 30f;
        public float logHolderFillRadius = 5f;

        [Header("Blueprint Completion")]
        public float blueprintDetectRadius = 20f;
        [Tooltip("Pause between consecutive blueprint jobs")]
        public float blueprintTransitionDelay = 1f;

        [Header("Area Clearing")]
        public float clearRadius5m = 5f;
        public float clearRadius10m = 10f;
        public float clearRadius20m = 20f;

        [Header("Repair")]
        public float repairSearchRadius = 25f;
        public float repairTimePerPiece = 3f;

        [Header("Behaviour")]
        [Tooltip("Seconds before a new command cancels current task gracefully")]
        public float taskCancellationDelay = 0.5f;
        [Tooltip("Seconds idle before Kelvin auto-returns to Follow state")]
        public float idleReturnFollowDelay = 5f;
    }
}
