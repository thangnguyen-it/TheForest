// ═══════════════════════════════════════════════════════════════════════════════
// 2.5 — BLUEPRINT SYSTEM
// ═══════════════════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;

namespace TheForest.Building.Systems
{
    /// <summary>
    /// 2.5 — Blueprint System.
    ///
    /// Workflow:
    ///   1. Player opens Guide Book (B) and selects blueprint.
    ///   2. System spawns a semi-transparent ghost prefab.
    ///   3. Player rotates (Q/R) and places (Left Click).
    ///   4. Ghost turns red if terrain / overlap invalid.
    ///   5. Player walks up with correct materials → each material slot appears.
    ///   6. When all materials filled → ghost becomes solid completed prefab.
    ///
    /// Hidden blueprints start as locked until discovered in the world.
    /// </summary>
    public class BlueprintSystem : MonoBehaviour
    {
        [Header("Config (inject once)")]
        [SerializeField] private BlueprintConfig blueprintConfig;
        [SerializeField] private MaterialDatabase materialDB;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform playerTransform;

        // Active ghost state
        private BlueprintData _selectedBlueprint;
        private GameObject _ghostInstance;
        private bool _ghostPlaced;        // ghost is anchored in world
        private float _ghostYRot;

        // Fill progress
        private Dictionary<string, int> _filledMaterials;   // materialId → amount filled
        private int _totalFilled;

        // Unlocked hidden blueprints
        private readonly HashSet<string> _unlockedHidden = new();

        // Ghost overlap buffer
        private readonly Collider[] _overlapBuf = new Collider[8];

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            if (blueprintConfig == null) Debug.LogError("[Blueprint] BlueprintConfig missing.", this);
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            // FIX Block 7: cầu nối cho Kelvin (KelvinBuildingCommands) qua EventBus, thay vì phía Kelvin
            // tự FindFirstObjectByType<BlueprintSystem>() rồi gọi thẳng FindNearestIncomplete/TryAddMaterial
            // (vi phạm kiến trúc "no Manager-to-Manager"). Xem KelvinBlueprintQueryEvents.cs.
            EventBus<KelvinBlueprintQueryEvent>.Subscribe(OnKelvinQuery);
            EventBus<KelvinBlueprintAddMaterialEvent>.Subscribe(OnKelvinAddMaterial);
        }

        private void OnDisable()
        {
            CancelGhost();
            EventBus<KelvinBlueprintQueryEvent>.Unsubscribe(OnKelvinQuery);
            EventBus<KelvinBlueprintAddMaterialEvent>.Unsubscribe(OnKelvinAddMaterial);
        }

        private void OnKelvinQuery(KelvinBlueprintQueryEvent e)
        {
            // Gọi lại chính phương thức public CỦA MÌNH (FindNearestIncomplete) — không phải manager-to-manager,
            // chỉ là BlueprintSystem tự phục vụ yêu cầu rồi trả lời qua bus.
            var (ghost, data) = FindNearestIncomplete(e.Position, e.Radius);
            EventBus<KelvinBlueprintQueryResultEvent>.Raise(new KelvinBlueprintQueryResultEvent(ghost, data));
        }

        private void OnKelvinAddMaterial(KelvinBlueprintAddMaterialEvent e)
        {
            TryAddMaterial(e.MaterialId, e.Amount);
        }

        // ── Update ───────────────────────────────────────────────────────────
        private void Update()
        {
            if (_ghostInstance == null || _ghostPlaced) return;
            UpdateGhostPositionAndRotation();
            ValidateGhostPlacement();
        }

        // ── Guide Book (B key) ────────────────────────────────────────────────
        public void OnGuideBook(InputValue v)
        {
            if (v.isPressed) OpenGuideBook();
        }

        private void OpenGuideBook()
        {
            // UI opens — selection handled externally via SelectBlueprint()
            // Guide Book in-world: book appears in player hand (animation event)
        }

        /// <summary>Called by UI when player taps a blueprint in the Guide Book.</summary>
        public void SelectBlueprint(BlueprintData data)
        {
            if (data == null) return;
            if (data.isHidden && !_unlockedHidden.Contains(data.blueprintId)) return;

            CancelGhost();
            _selectedBlueprint = data;
            SpawnGhost(data);
            EventBus<BlueprintSelectedEvent>.Raise(
                new BlueprintSelectedEvent(data.blueprintId, data.isHidden));
        }

        // ── Ghost placement ───────────────────────────────────────────────────
        private void SpawnGhost(BlueprintData data)
        {
            if (data.ghostPrefab == null) return;

            _ghostInstance = Instantiate(data.ghostPrefab,
                cameraTransform.position + cameraTransform.forward * 3f,
                Quaternion.identity);
            SetGhostMaterial(_ghostInstance, blueprintConfig.ghostMaterialValid);
        }

        private void UpdateGhostPositionAndRotation()
        {
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                _ghostInstance.transform.position = hit.point;

            _ghostInstance.transform.rotation = Quaternion.Euler(0f, _ghostYRot, 0f);
        }

        private void ValidateGhostPlacement()
        {
            if (_ghostInstance == null) return;

            var center = _ghostInstance.transform.position;
            var rot = _ghostInstance.transform.rotation;
            var half = Vector3.one * blueprintConfig.ghostCheckRadius;

            int hits = Physics.OverlapBoxNonAlloc(center, half, _overlapBuf, rot,
                blueprintConfig.overlapCheckMask, QueryTriggerInteraction.Ignore);

            bool valid = hits == 0;
            SetGhostMaterial(_ghostInstance, valid
                ? blueprintConfig.ghostMaterialValid
                : blueprintConfig.ghostMaterialInvalid);

            EventBus<PlacementValidityChangedEvent>.Raise(
                new PlacementValidityChangedEvent(valid, valid ? null : "Blueprint overlap"));
        }

        // ── Rotate / Place / Cancel ───────────────────────────────────────────
        public void OnBlueprintRotateCCW(InputValue v)
        { if (v.isPressed && _ghostInstance != null) _ghostYRot -= blueprintConfig.blueprintRotStep; }

        public void OnBlueprintRotateCW(InputValue v)
        { if (v.isPressed && _ghostInstance != null) _ghostYRot += blueprintConfig.blueprintRotStep; }

        public void OnBlueprintPlace(InputValue v)
        {
            if (!v.isPressed || _ghostInstance == null || _ghostPlaced) return;

            // Final validation
            ValidateGhostPlacement();

            _ghostPlaced = true;
            _filledMaterials = new Dictionary<string, int>();
            _totalFilled = 0;

            foreach (var cost in _selectedBlueprint.materialCosts)
                _filledMaterials[cost.materialId] = 0;

            EventBus<BlueprintPlacedEvent>.Raise(
                new BlueprintPlacedEvent(_selectedBlueprint.blueprintId,
                    _ghostInstance.transform.position,
                    _ghostInstance.transform.rotation));
        }

        public void OnBlueprintCancel(InputValue v)
        { if (v.isPressed) CancelGhost(); }

        // ── Material filling ──────────────────────────────────────────────────
        /// <summary>
        /// Called when player stands near blueprint holding a matching material.
        /// Triggered by interaction system detecting player proximity.
        /// </summary>
        public bool TryAddMaterial(string materialId, int amount = 1)
        {
            if (!_ghostPlaced || _filledMaterials == null) return false;
            if (!_filledMaterials.ContainsKey(materialId)) return false;

            // Find how many more are needed
            int needed = 0;
            foreach (var cost in _selectedBlueprint.materialCosts)
                if (cost.materialId == materialId) { needed = cost.amount; break; }

            int already = _filledMaterials[materialId];
            int add = Mathf.Min(amount, needed - already);
            if (add <= 0) return false;

            _filledMaterials[materialId] = already + add;
            _totalFilled += add;

            int remaining = _selectedBlueprint.TotalMaterialCount - _totalFilled;
            EventBus<BlueprintMaterialAddedEvent>.Raise(
                new BlueprintMaterialAddedEvent(_selectedBlueprint.blueprintId,
                    materialId, add, remaining));

            if (remaining <= 0) CompleteBluprint();
            return true;
        }

        private void CompleteBluprint()
        {
            if (_ghostInstance == null) return;

            var pos = _ghostInstance.transform.position;
            var rot = _ghostInstance.transform.rotation;

            Destroy(_ghostInstance);
            _ghostInstance = null;

            if (_selectedBlueprint.completedPrefab != null)
                Instantiate(_selectedBlueprint.completedPrefab, pos, rot);

            EventBus<BlueprintCompletedEvent>.Raise(
                new BlueprintCompletedEvent(_selectedBlueprint.blueprintId, pos));

            _selectedBlueprint = null;
            _filledMaterials = null;
            _totalFilled = 0;
            _ghostPlaced = false;
        }

        // ── Hidden blueprint unlock ───────────────────────────────────────────
        public void UnlockHiddenBlueprint(string blueprintId)
            => _unlockedHidden.Add(blueprintId);

        // ── Kelvin API ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns the nearest incomplete blueprint within radius.
        /// Used by KelvinBuildingCommands to finish blueprints.
        /// </summary>
        public (GameObject ghost, BlueprintData data) FindNearestIncomplete(Vector3 pos, float radius)
        {
            if (!_ghostPlaced || _ghostInstance == null) return (null, null);
            if ((pos - _ghostInstance.transform.position).sqrMagnitude > radius * radius)
                return (null, null);
            return (_ghostInstance, _selectedBlueprint);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void CancelGhost()
        {
            if (_ghostInstance != null) Destroy(_ghostInstance);
            _ghostInstance = null;
            _selectedBlueprint = null;
            _filledMaterials = null;
            _ghostPlaced = false;
            _totalFilled = 0;
        }

        private static void SetGhostMaterial(GameObject ghost, Material mat)
        {
            if (ghost == null || mat == null) return;
            foreach (var r in ghost.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.materials = mats;
            }
        }
    }
}