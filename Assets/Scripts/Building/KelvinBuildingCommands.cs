// ═══════════════════════════════════════════════════════════════════════════════
// 2.14 — KELVIN BUILDING COMMANDS
// ═══════════════════════════════════════════════════════════════════════════════
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;

namespace TheForest.Building.Systems
{
    /// <summary>
    /// 2.14 — Kelvin Building Commands.
    ///
    /// Kelvin's building capabilities (from Data 19):
    ///   FillLogHolder  — gather logs continuously and deposit in nearest LogHolder
    ///   FetchLogs      — bring logs to player position (logsPerTrip at a time)
    ///   BuildFire      — place campfire at target position
    ///   BuildShelter   — place lean-to shelter prefab
    ///   FinishBlueprint — walk to nearest incomplete blueprint and fill materials
    ///   RepairStructure — find damaged pieces and repair them
    ///   ClearArea       — chop trees within configured radius
    ///
    /// Kelvin constraints (from Data research):
    ///   • Cannot build Freeform from scratch — only completes existing blueprints.
    ///   • Does NOT auto-dodge falling trees.
    ///   • Post Patch 12: FinishBlueprint continues until all blueprints done.
    ///   • Never enters caves (enforced by NavMesh area mask — see CompanionNavController).
    ///
    /// Architecture: receives KelvinBuildingCommandIssuedEvent from EventBus,
    ///   processes via task queue, raises KelvinBuildingTaskStartedEvent /
    ///   KelvinBuildingTaskCompletedEvent. Never calls other systems directly.
    /// </summary>
    public class KelvinBuildingCommands : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Config (inject once)")]
        [SerializeField] private KelvinBuildingConfig config;
        [SerializeField] private MaterialDatabase materialDB;

        [Header("References")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;

        // ── Runtime ──────────────────────────────────────────────────────────
        private KelvinBuildingTask _currentTask;
        private Vector3 _taskTarget;
        private bool _taskActive;
        private Coroutine _taskRoutine;

        // Shared log carry list
        private readonly System.Collections.Generic.List<LogType> _carryList = new();

        private static readonly int AnimChop = Animator.StringToHash("Chop");
        private static readonly int AnimCarry = Animator.StringToHash("Carry");
        private static readonly int AnimRepair = Animator.StringToHash("Repair");
        private static readonly int AnimIdle = Animator.StringToHash("Idle");

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (config == null) Debug.LogError("[Kelvin] KelvinBuildingConfig missing.", this);
        }

        private void OnEnable()
        {
            EventBus<KelvinBuildingCommandIssuedEvent>.Subscribe(OnCommandIssued);
            EventBus<KelvinBlueprintQueryResultEvent>.Subscribe(OnBlueprintQueryResult);
        }

        private void OnDisable()
        {
            EventBus<KelvinBuildingCommandIssuedEvent>.Unsubscribe(OnCommandIssued);
            EventBus<KelvinBlueprintQueryResultEvent>.Unsubscribe(OnBlueprintQueryResult);
            CancelCurrentTask();
        }

        // ── FIX Block 7: Kelvin <-> BlueprintSystem QUA EventBus (không FindFirstObjectByType nữa) ──
        private (GameObject Ghost, BlueprintData Data) _lastBlueprintQueryResult;

        private void OnBlueprintQueryResult(KelvinBlueprintQueryResultEvent e)
        {
            _lastBlueprintQueryResult = (e.Ghost, e.Data);
        }

        /// <summary>
        /// Truy vấn blueprint gần nhất qua EventBus thay vì FindFirstObjectByType&lt;BlueprintSystem&gt;() + gọi thẳng.
        /// EventBus&lt;T&gt;.Raise chạy ĐỒNG BỘ (xem EventBus.cs) nên _lastBlueprintQueryResult có giá trị
        /// ngay sau lệnh Raise bên dưới — giữ nguyên ngữ nghĩa gọi-đồng-bộ như code cũ, không cần yield thêm.
        /// </summary>
        private (GameObject ghost, BlueprintData data) QueryNearestBlueprint(Vector3 pos, float radius)
        {
            _lastBlueprintQueryResult = (null, null);
            EventBus<KelvinBlueprintQueryEvent>.Raise(new KelvinBlueprintQueryEvent(pos, radius));
            return _lastBlueprintQueryResult;
        }

        // ── Event handler ─────────────────────────────────────────────────────
        private void OnCommandIssued( KelvinBuildingCommandIssuedEvent e)
        {
            CancelCurrentTask();
            _currentTask = e.Task;
            _taskTarget = e.TargetPosition;
            _taskRoutine = StartCoroutine(ExecuteTaskWithDelay(e.Task, e.TargetPosition));
        }

        // ── Task dispatcher ───────────────────────────────────────────────────
        private IEnumerator ExecuteTaskWithDelay(KelvinBuildingTask task, Vector3 target)
        {
            yield return new WaitForSeconds(config.taskCancellationDelay);

            _taskActive = true;
            EventBus<KelvinBuildingTaskStartedEvent>.Raise(new KelvinBuildingTaskStartedEvent(task));

            bool success = false;
            switch (task)
            {
                case KelvinBuildingTask.FillLogHolder: yield return RunTask(FillLogHolderRoutine()); break;
                case KelvinBuildingTask.FetchLogs: yield return RunTask(FetchLogsRoutine(target)); break;
                case KelvinBuildingTask.BuildFire: yield return RunTask(BuildFireRoutine(target)); break;
                case KelvinBuildingTask.BuildShelter: yield return RunTask(BuildShelterRoutine(target)); break;
                case KelvinBuildingTask.FinishBlueprint: yield return RunTask(FinishBlueprintRoutine()); break;
                case KelvinBuildingTask.RepairStructure: yield return RunTask(RepairStructureRoutine()); break;
                case KelvinBuildingTask.ClearArea: yield return RunTask(ClearAreaRoutine(target)); break;
            }

            _taskActive = false;
            EventBus<KelvinBuildingTaskCompletedEvent>.Raise(
                new KelvinBuildingTaskCompletedEvent(task, success));
        }

        // ── Coroutine helper: yield from IEnumerator and capture bool result ──
        private bool _taskResult;
        private IEnumerator RunTask(IEnumerator routine)
        {
            yield return routine;
        }

        // ═════════════════════════════════════════════════════════════════════
        // TASK IMPLEMENTATIONS
        // ═════════════════════════════════════════════════════════════════════

        // ── FillLogHolder ─────────────────────────────────────────────────────
        /// <summary>
        /// Kelvin continuously finds loose logs on the ground, picks them up
        /// (2 per trip), walks to nearest LogHolder, deposits, repeats until
        /// no logs remain or holder is full.
        /// Patch 12 behaviour: does NOT stop after 1 trip.
        /// </summary>
        private IEnumerator FillLogHolderRoutine()
        {
            var holder = FindNearestLogHolder();
            if (holder == null) { Debug.Log("[Kelvin] No LogHolder found."); yield break; }

            animator.SetTrigger(AnimCarry);

            while (true)
            {
                // Find loose log pieces on the ground (not in holders)
                var logs = FindLooseLogsNear(transform.position, config.logSearchRadius);
                if (logs.Count == 0 || holder.IsFull) break;

                // Pick up up to logsPerBasicTrip
                _carryList.Clear();
                int toCarry = Mathf.Min(config.logsPerBasicTrip, logs.Count);
                for (int i = 0; i < toCarry; i++)
                {
                    var lp = logs[i];
                    _carryList.Add(lp.LogType);
                    lp.transform.SetParent(transform, true); // attach to Kelvin
                }

                // Walk to holder
                yield return WalkTo(holder.transform.position, config.logHolderFillRadius);

                // Deposit
                holder.FillFrom(_carryList);
                _carryList.Clear();

                // Release carried logs visually
                foreach (Transform child in transform)
                {
                    var lp = child.GetComponent<LogPiece>();
                    if (lp != null) { child.SetParent(null); Destroy(child.gameObject); }
                }

                yield return new WaitForSeconds(0.5f);
            }

            animator.SetTrigger(AnimIdle);
        }

        // ── FetchLogs ─────────────────────────────────────────────────────────
        private IEnumerator FetchLogsRoutine(Vector3 playerPos)
        {
            animator.SetTrigger(AnimCarry);
            int trips = 0;

            while (trips < 3)  // reasonable limit per command
            {
                var logs = FindLooseLogsNear(transform.position, config.logSearchRadius);
                if (logs.Count == 0) break;

                _carryList.Clear();
                int toCarry = Mathf.Min(config.logsPerBasicTrip, logs.Count);
                for (int i = 0; i < toCarry; i++)
                {
                    _carryList.Add(logs[i].LogType);
                    logs[i].transform.SetParent(transform, true);
                }

                yield return WalkTo(playerPos, 2f);

                // Drop logs near player
                foreach (Transform child in transform)
                {
                    var lp = child.GetComponent<LogPiece>();
                    if (lp == null) continue;
                    child.SetParent(null);
                    child.position = playerPos + Random.insideUnitSphere * 1.5f;
                    child.position = new Vector3(child.position.x, playerPos.y, child.position.z);
                    EventBus<LogPlacedEvent>.Raise(
                        new LogPlacedEvent(child.gameObject, lp.LogType, child.position, child.rotation, 0));
                }

                trips++;
                yield return new WaitForSeconds(0.3f);
            }

            animator.SetTrigger(AnimIdle);
        }

        // ── BuildFire ────────────────────────────────────────────────────────
        private IEnumerator BuildFireRoutine(Vector3 pos)
        {
            yield return WalkTo(pos, 2f);
            animator.SetTrigger(AnimChop);
            yield return new WaitForSeconds(2f);
            // Fire prefab instantiation happens via EventBus listener in BuildingController
            EventBus<StructureFirePlacedEvent>.Raise(new StructureFirePlacedEvent(pos));
            animator.SetTrigger(AnimIdle);
        }

        // ── BuildShelter ──────────────────────────────────────────────────────
        private IEnumerator BuildShelterRoutine(Vector3 pos)
        {
            yield return WalkTo(pos, 2f);
            animator.SetTrigger(AnimChop);
            yield return new WaitForSeconds(3f);
            // Lean-to blueprint auto-complete
            EventBus<BlueprintCompletedEvent>.Raise(new BlueprintCompletedEvent("lean_to", pos));
            animator.SetTrigger(AnimIdle);
        }

        // ── FinishBlueprint ───────────────────────────────────────────────────
        /// <summary>
        /// Patch 12: Kelvin finishes ALL incomplete blueprints in range,
        /// not just one. He loops until none remain.
        /// </summary>
        private IEnumerator FinishBlueprintRoutine()
        {
            animator.SetTrigger(AnimCarry);

            int maxIterations = 20;
            while (maxIterations-- > 0)
            {
                // FIX Block 7: không FindFirstObjectByType<BlueprintSystem>() + gọi thẳng nữa (vi phạm
                // kiến trúc "no Manager-to-Manager") -> truy vấn qua EventBus (xem KelvinBlueprintQueryEvents.cs).
                var (ghost, data) = QueryNearestBlueprint(transform.position, config.blueprintDetectRadius);

                if (ghost == null || data == null) break;

                yield return WalkTo(ghost.transform.position, 2.5f);

                // Fill each material in the blueprint
                foreach (var cost in data.materialCosts)
                {
                    for (int i = 0; i < cost.amount; i++)
                    {
                        // Find matching log near Kelvin, consume and fill
                        var logs = FindLooseLogsNear(transform.position, 10f);
                        bool added = false;
                        foreach (var log in logs)
                        {
                            if (MaterialDatabase.LogTypeForId(cost.materialId) != log.LogType) continue;
                            EventBus<KelvinBlueprintAddMaterialEvent>.Raise(
                                new KelvinBlueprintAddMaterialEvent(cost.materialId, 1));
                            Destroy(log.gameObject);
                            added = true;
                            break;
                        }
                        if (!added) break;
                        yield return new WaitForSeconds(0.5f);
                    }
                }

                yield return new WaitForSeconds(config.blueprintTransitionDelay);
            }

            animator.SetTrigger(AnimIdle);
        }

        // ── RepairStructure ───────────────────────────────────────────────────
        private IEnumerator RepairStructureRoutine()
        {
            animator.SetTrigger(AnimRepair);

            // Find all damaged pieces within repairSearchRadius
            var buf = new Collider[32];
            int count = Physics.OverlapSphereNonAlloc(transform.position,
                config.repairSearchRadius, buf);

            for (int i = 0; i < count; i++)
            {
                var piece = buf[i].GetComponentInParent<LogPiece>();
                if (piece == null || piece.DamageState != DamageState.Damaged) continue;

                yield return WalkTo(piece.transform.position, 1.5f);
                yield return new WaitForSeconds(config.repairTimePerPiece);

                piece.Repair(piece.MaxHealth); // full repair
                EventBus<BuildingPieceRepairedEvent>.Raise(
                    new BuildingPieceRepairedEvent(piece.gameObject, piece.transform.position));
            }

            animator.SetTrigger(AnimIdle);
        }

        // ── ClearArea ─────────────────────────────────────────────────────────
        private IEnumerator ClearAreaRoutine(Vector3 center)
        {
            float radius = _currentTask == KelvinBuildingTask.ClearArea
                ? config.clearRadius10m : config.clearRadius5m;

            animator.SetTrigger(AnimChop);

            var buf = new Collider[32];
            int count = Physics.OverlapSphereNonAlloc(center, radius, buf);

            for (int i = 0; i < count; i++)
            {
                var tree = buf[i].GetComponent<TheForest.World.TreeCutting>();
                if (tree == null || !tree.CanInteract()) continue;

                yield return WalkTo(buf[i].transform.position, 1.5f);

                // Simulate axe swings
                for (int swing = 0; swing < 8; swing++)
                {
                    animator.SetTrigger(AnimChop);
                    bool fell = tree.ApplyChop(30f, transform);
                    yield return new WaitForSeconds(0.6f);
                    if (fell) break;
                }
            }

            animator.SetTrigger(AnimIdle);
        }

        // ── NavMesh walk helper ───────────────────────────────────────────────
        private IEnumerator WalkTo(Vector3 destination, float stoppingDist)
        {
            if (agent == null || !agent.isOnNavMesh) yield break;

            agent.stoppingDistance = stoppingDist;
            agent.SetDestination(destination);

            while (!agent.pathPending &&
                   agent.remainingDistance > stoppingDist)
            {
                yield return null;
            }
        }

        // ── Scene scanning helpers ────────────────────────────────────────────
        private static LogHolder FindNearestLogHolder()
        {
            // Simple Object.FindObjectsOfType scan — replace with registry for performance
            var holders = FindObjectsByType<LogHolder>(FindObjectsSortMode.None);
            if (holders.Length == 0) return null;

            LogHolder best = null;
            float bestD = float.MaxValue;
            foreach (var h in holders)
            {
                if (h.IsFull) continue;
                float d = (h.transform.position - Camera.main.transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = h; }
            }
            return best;
        }

        private static System.Collections.Generic.List<LogPiece> FindLooseLogsNear(
            Vector3 origin, float radius)
        {
            var result = new System.Collections.Generic.List<LogPiece>();
            var buf = new Collider[32];
            int count = Physics.OverlapSphereNonAlloc(origin, radius, buf);
            for (int i = 0; i < count; i++)
            {
                var lp = buf[i].GetComponentInParent<LogPiece>();
                if (lp != null && !result.Contains(lp) && lp.transform.parent == null)
                    result.Add(lp);
            }
            return result;
        }

        // ── Cancel ───────────────────────────────────────────────────────────
        private void CancelCurrentTask()
        {
            if (_taskRoutine != null) StopCoroutine(_taskRoutine);
            _taskActive = false;
            _carryList.Clear();

            // Detach any carried logs
            foreach (Transform child in transform)
            {
                if (child.GetComponent<LogPiece>() != null)
                {
                    child.SetParent(null);
                    child.position += Vector3.up * 0.1f;
                }
            }
        }
    }
}