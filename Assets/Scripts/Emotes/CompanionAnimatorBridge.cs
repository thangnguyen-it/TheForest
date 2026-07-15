using UnityEngine;
using UnityEngine.AI;
using Companion.Data;
using Companion.Events;

namespace Companion.FSM
{
    /// <summary>
    /// Cầu nối State → Animator. Là LISTENER của CompanionStateChannelSO.
    /// Không giữ tham chiếu tới CompanionFSM (chỉ nghe channel) — khớp §1.5.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CompanionAnimatorBridge : MonoBehaviour
    {
        [SerializeField] private CompanionStateChannelSO stateChannel;
        [SerializeField] private NavMeshAgent agent; // để feed Speed
        private Animator _anim;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int StateHash = Animator.StringToHash("State");
        private static readonly int KnockedHash = Animator.StringToHash("KnockedDown");
        private static readonly int DeadHash = Animator.StringToHash("Dead");

        private void Awake() => _anim = GetComponent<Animator>();
        private void OnEnable() { if (stateChannel) stateChannel.Register(OnState); }
        private void OnDisable() { if (stateChannel) stateChannel.Unregister(OnState); }

        private void OnState(CompanionState s)
        {
            _anim.SetInteger(StateHash, (int)s);
            _anim.SetBool(KnockedHash, s == CompanionState.KnockedDown);
            if (s == CompanionState.Dead) _anim.SetTrigger(DeadHash);
        }

        private void Update()
        {
            // Feed Speed cho locomotion blend tree.
            if (agent != null && agent.isOnNavMesh)
                _anim.SetFloat(SpeedHash, agent.velocity.magnitude);
        }
    }
}
