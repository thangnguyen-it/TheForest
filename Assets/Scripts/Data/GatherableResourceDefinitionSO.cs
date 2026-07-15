using UnityEngine;

namespace Companion.Data
{
    [CreateAssetMenu(menuName = "Companion/Data/Gatherable Resource", fileName = "Res_New")]
    public class GatherableResourceDefinitionSO : ScriptableObject
    {
        public ResourceType type;
        [Tooltip("Logs = 2 (confirmed balance value, not 1).")]
        public int carryCapacityPerTrip = 1;
        [Tooltip("TRUE only for Fish — probability-timer, no scene GameObject required.")]
        public bool isInfiniteNode;
        public float gatherTimeSeconds = 3f;
        public GameObject dropPrefab;

        [Header("Fish-only (isInfiniteNode)")]
        [Range(0f, 1f)] public float catchProbabilityPerTimer = 0.5f;
        public float catchTimerInterval = 4f;
        public string waterNavMeshAreaName = "Water";
    }
}
