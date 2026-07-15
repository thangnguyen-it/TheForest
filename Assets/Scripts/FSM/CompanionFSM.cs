using System;
using UnityEngine;
using UnityEngine.AI;
using Companion.Data;
using Companion.Events;

namespace Companion.FSM
{
    /// <summary>
    /// Event-driven companion FSM. Transitions ONLY in response to channel reception.
    /// No Manager-to-Manager calls — every cross-system signal is an SO channel.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CompanionFSM : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private int companionId = 1;

        [Header("Channels — IN")]
        [SerializeField] private CommandIssuedChannelSO commandIssued;
        [SerializeField] private ResourceGrantedChannelSO resourceGranted;
        [SerializeField] private CompanionDamagedChannelSO companionDamaged;

        [Header("Channels — OUT")]
        [SerializeField] private CompanionStateChannelSO stateChannel;
        [SerializeField] private ResourceRequestChannelSO resourceRequest;
        [SerializeField] private CompanionDiedChannelSO diedChannel;

        [Header("Stats")]
        [SerializeField] private CompanionStatRuntime energy;
        [SerializeField] private CompanionStatRuntime sentiment;
        [SerializeField] private CompanionStatRuntime memory;
        [SerializeField] private CompanionStatRuntime fear;
        [SerializeField] private float fearFleeThreshold = 70f;

        [Header("Resource Catalog")]
        [SerializeField] private GatherableResourceDefinitionSO[] resourceCatalog;

        [Header("Energy → Speed Coupling")]
        [Tooltip("Min speed multiplier when energy hits 0. Never 0 — energy must not freeze behavior.")]
        [SerializeField, Range(0.05f, 1f)] private float minEnergySpeedMultiplier = 0.35f;

        [Header("Permadeath")]
        [SerializeField] private float health = 100f;
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float reviveWindowSeconds = 10f;
        [SerializeField, Range(0f, 1f)] private float revivePercent = 0.5f;

        public CompanionState CurrentState { get; private set; } = CompanionState.Idle;
        public int CompanionId => companionId;

        private NavMeshAgent _agent;
        private float _baseSpeed;
        private bool _isTicking = true;

        // permadeath bookkeeping
        private float _reviveTimer;
        private bool _diedRaised;
        private bool _lastDamageByPlayer;

        // active command context
        private Cmd_Get _activeGet;
        private float _gatherTimer;

        // ------- lifecycle -------
        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _baseSpeed = _agent.speed;
            energy.Init(); sentiment.Init(); memory.Init(); fear.Init();
        }

        private void OnEnable()
        {
            commandIssued?.Register(OnCommandIssued);
            resourceGranted?.Register(OnResourceGranted);
            companionDamaged?.Register(OnDamaged);
        }

        private void OnDisable()
        {
            commandIssued?.Unregister(OnCommandIssued);
            resourceGranted?.Unregister(OnResourceGranted);
            companionDamaged?.Unregister(OnDamaged);
        }

        // ------- transition core -------
        private void TransitionTo(CompanionState next)
        {
            if (CurrentState == CompanionState.Dead) return; // Dead is terminal
            if (CurrentState == next) return;

            CurrentState = next;
            stateChannel?.Raise(next);

            if (next == CompanionState.Dead)
            {
                if (!_diedRaised)
                {
                    _diedRaised = true;
                    diedChannel?.Raise(companionId, _lastDamageByPlayer); // exactly once
                }
                _isTicking = false; // disable update tick
                if (_agent.isOnNavMesh) _agent.isStopped = true;
            }
        }

        // ------- command reception (only entry point for command-driven transitions) -------
        private void OnCommandIssued(CompanionCommandDefinitionSO cmd)
        {
            if (CurrentState == CompanionState.Dead || CurrentState == CompanionState.KnockedDown) return;

            switch (cmd)
            {
                case Cmd_TakeItem:
                    // cosmetic only — NEVER modifies a stat, NEVER changes state.
                    return;

                case Cmd_Get get:
                    _activeGet = get;
                    RequestResource(get.resource);
                    TransitionTo(CompanionState.GatheringResource);
                    return;

                default:
                    TransitionTo(cmd.TargetState);
                    return;
            }
        }

        private void RequestResource(ResourceType type)
        {
            // Ask WorldResourceSystem via channel — never call it directly.
            resourceRequest?.Raise(type, transform.position);
        }

        private void OnResourceGranted(ResourceType type, int amount)
        {
            if (CurrentState != CompanionState.GatheringResource) return;
            // Significant event → selective memory write.
            WriteMemory(2f);
            // Delivery handling would route via DeliveryMode; after delivery, idle/follow.
            TransitionTo(CompanionState.Following);
        }

        // ------- damage / permadeath -------
        private void OnDamaged(float amount, DamageSource source)
        {
            if (CurrentState == CompanionState.Dead) return;

            _lastDamageByPlayer = source == DamageSource.Player;

            if (CurrentState == CompanionState.KnockedDown)
            {
                // Any further damage while down → Dead (terminal).
                TransitionTo(CompanionState.Dead);
                return;
            }

            health = Mathf.Max(0f, health - amount);
            WriteMemory(3f); // combat engaged is significant

            if (health <= 0f)
            {
                _reviveTimer = reviveWindowSeconds;
                TransitionTo(CompanionState.KnockedDown);
            }
        }

        /// <summary>Player revive interaction (called by a PlayerInteract listener, not a manager).</summary>
        public void TryRevive()
        {
            if (CurrentState != CompanionState.KnockedDown) return;
            health = maxHealth * revivePercent;
            TransitionTo(CompanionState.Idle);
        }

        // ------- selective memory -------
        private void WriteMemory(float importanceBoost)
        {
            // Importance-weighted: significant events nudge Memory up; routine decay handled in tick.
            memory.Add(importanceBoost);
        }

        // ------- main tick -------
        private void Update()
        {
            if (!_isTicking) return;
            float dt = Time.deltaTime;

            // Stat decay (Memory etc.) — accumulator-based, no per-frame alloc.
            memory.TickDecay(dt);
            fear.TickDecay(dt);

            // Energy → speed coupling. Energy 0 slows but NEVER freezes.
            ApplyEnergyToSpeed();

            // Fear override: force Fleeing regardless of current command.
            if (fear.Value >= fearFleeThreshold &&
                CurrentState != CompanionState.Fleeing &&
                CurrentState != CompanionState.KnockedDown &&
                CurrentState != CompanionState.Dead)
            {
                TransitionTo(CompanionState.Fleeing);
            }

            switch (CurrentState)
            {
                case CompanionState.GatheringResource: TickGather(dt); break;
                case CompanionState.Resting: TickResting(dt); break;
                case CompanionState.KnockedDown: TickKnockedDown(dt); break;
                    // Following / Building / Clearing / Fleeing handled by their controllers/listeners.
            }
        }

        private void ApplyEnergyToSpeed()
        {
            if (energy.definition == null) return;
            float t = Mathf.InverseLerp(energy.definition.minValue, energy.definition.maxValue, energy.Value);
            float mult = Mathf.Lerp(minEnergySpeedMultiplier, 1f, t);
            _agent.speed = _baseSpeed * mult;       // slows, never 0
            // movement step energy cost
            if (_agent.velocity.sqrMagnitude > 0.01f)
                energy.Add(-0.1f * Time.deltaTime);
        }

        private void TickGather(float dt)
        {
            if (_activeGet == null) return;
            var def = FindResourceDef(_activeGet.resource);
            if (def == null) return;

            _gatherTimer += dt;

            if (def.isInfiniteNode) // Fish — probability-timer, no scene GameObject
            {
                if (_gatherTimer >= def.catchTimerInterval)
                {
                    _gatherTimer = 0f;
                    if (UnityEngine.Random.value <= def.catchProbabilityPerTimer)
                        OnResourceGranted(def.type, def.carryCapacityPerTrip);
                }
            }
            else
            {
                if (_gatherTimer >= def.gatherTimeSeconds)
                {
                    _gatherTimer = 0f;
                    OnResourceGranted(def.type, def.carryCapacityPerTrip);
                }
            }
        }

        private void TickResting(float dt)
        {
            if (energy.definition != null)
                energy.Add(energy.definition.regenRatePerSecond * dt);
        }

        private void TickKnockedDown(float dt)
        {
            _reviveTimer -= dt;
            if (_reviveTimer <= 0f)
                TransitionTo(CompanionState.Dead); // window expiry → Dead
        }

        private GatherableResourceDefinitionSO FindResourceDef(ResourceType t)
        {
            if (resourceCatalog == null) return null;
            for (int i = 0; i < resourceCatalog.Length; i++)
                if (resourceCatalog[i] != null && resourceCatalog[i].type == t)
                    return resourceCatalog[i];
            return null;
        }

        // ------- save/load bridge -------
        public CompanionSaveData CaptureSave(int currentDay) => new CompanionSaveData
        {
            typeId = companionId,
            state = CurrentState,
            health = health,
            energy = energy.Value,
            sentiment = sentiment.Value,
            memory = memory.Value,
            fear = fear.Value,
            isDead = CurrentState == CompanionState.Dead,
            playerKilled = _lastDamageByPlayer && CurrentState == CompanionState.Dead,
            killedOnDay = CurrentState == CompanionState.Dead ? currentDay : -1
        };

        public void RestoreSave(CompanionSaveData d)
        {
            health = d.health;
            energy.SetValue(d.energy);
            sentiment.SetValue(d.sentiment);
            memory.SetValue(d.memory);
            fear.SetValue(d.fear);
            _lastDamageByPlayer = d.playerKilled;

            if (d.isDead || d.state == CompanionState.Dead)
            {
                // Dead persists across load. Mark raised so we don't re-fire on restore.
                _diedRaised = true;
                CurrentState = CompanionState.Dead;
                _isTicking = false;
                if (_agent && _agent.isOnNavMesh) _agent.isStopped = true;
                stateChannel?.Raise(CompanionState.Dead);
            }
            else
            {
                TransitionTo(d.state);
            }
        }
    }
}
