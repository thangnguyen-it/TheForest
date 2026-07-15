using UnityEngine;
using UnityEngine.AI;
using Companion.Data;
using Companion.Events;

namespace Companion.Navigation
{
    /// <summary>
    /// Movement-only controller. Listens to state changes and drives the NavMeshAgent.
    /// Excludes the Underground area so the companion never enters caves/bunkers.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CompanionNavController : MonoBehaviour
    {
        [SerializeField] private CompanionStateChannelSO stateChannel;
        [SerializeField] private Transform player;
        [SerializeField] private float followStopDistance = 2.5f;
        [SerializeField] private float repathInterval = 0.25f;

        private NavMeshAgent _agent;
        private CompanionState _state = CompanionState.Idle;
        private float _repathTimer;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            ConfigureAreaMask();
        }

        private void ConfigureAreaMask()
        {
            // Include all, then strip Underground.
            int underground = 1 << NavMesh.GetAreaFromName("Underground");
            int all = NavMesh.AllAreas;
            _agent.areaMask = all & ~underground; // never pathfind Underground
        }

        private void OnEnable() { if (stateChannel) stateChannel.Register(OnState); }
        private void OnDisable() { if (stateChannel) stateChannel.Unregister(OnState); }

        private void OnState(CompanionState s)
        {
            _state = s;
            if (s == CompanionState.Dead || s == CompanionState.KnockedDown)
            {
                if (_agent.isOnNavMesh) _agent.isStopped = true;
            }
            else if (_agent.isOnNavMesh)
            {
                _agent.isStopped = false;
            }
        }

        private void Update()
        {
            if (_state != CompanionState.Following || player == null || !_agent.isOnNavMesh) return;
            _repathTimer -= Time.deltaTime;
            if (_repathTimer > 0f) return;
            _repathTimer = repathInterval;

            float d = Vector3.Distance(transform.position, player.position);
            if (d > followStopDistance) _agent.SetDestination(player.position);
            else _agent.ResetPath();
        }
    }
}
