// ═══════════════════════════════════════════════════════════════════════════════
// 2.10 — TARP & ROPE SYSTEM
// ═══════════════════════════════════════════════════════════════════════════════
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TheForest.Building.Config;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;
using TheForest.Interaction;
using TheForest.Player;

namespace TheForest.Building.Systems
{
    // ── Tarp Piece ────────────────────────────────────────────────────────────
    /// <summary>
    /// Tarp stretched between 2–4 anchor points.
    /// Uses MeshFilter + MeshRenderer to dynamically deform the tarp mesh.
    /// Trampoline mode: trigger collider gives player upward velocity.
    /// </summary>
    public class TarpPiece : MonoBehaviour
    {
        [Header("Anchors")]
        [SerializeField] private List<Transform> anchors = new();  // 2 or 4 log ends

        [Header("Trampoline")]
        [SerializeField] private float bounceForce = 12f;
        [SerializeField] private float fallDamageReduction = 1f; // 0..1, 1 = full immunity

        private TarpMode _mode;
        private MeshFilter _meshFilter;
        private bool _isTrampoline;
        private Collider _triggerCollider;

        public TarpMode Mode => _mode;

        public void Initialize(TarpMode mode, List<Transform> attachPoints)
        {
            _mode = mode;
            anchors = new List<Transform>(attachPoints);

            _meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
            gameObject.AddComponent<MeshRenderer>();

            _isTrampoline = mode == TarpMode.Trampoline;
            if (_isTrampoline) SetupTrampolineCollider();

            RebuildMesh();
            EventBus<TarpDeployedEvent>.Raise(new TarpDeployedEvent(gameObject, mode));
        }

        private void SetupTrampolineCollider()
        {
            _triggerCollider = gameObject.AddComponent<BoxCollider>();
            ((BoxCollider)_triggerCollider).isTrigger = true;
            ((BoxCollider)_triggerCollider).size = new Vector3(3f, 0.2f, 3f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isTrampoline) return;
            var rb = other.GetComponentInParent<Rigidbody>() ?? other.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddForce(Vector3.up * bounceForce, ForceMode.Impulse);

            // Suppress fall damage
            var stats = other.GetComponentInParent<SurvivalStats>();
            stats?.Ignite(0f); // No-op — hook for fall damage reset (extend SurvivalStats)
        }

        private void RebuildMesh()
        {
            if (anchors.Count < 2) return;
            var mesh = new Mesh { name = "TarpMesh" };

            if (anchors.Count == 2)
                BuildTwoPointMesh(mesh);
            else
                BuildFourPointMesh(mesh);

            _meshFilter.mesh = mesh;
        }

        private void BuildTwoPointMesh(Mesh mesh)
        {
            var a = anchors[0].position;
            var b = anchors[1].position;
            float w = 1.5f;
            Vector3 side = Vector3.Cross((b - a).normalized, Vector3.up) * w;

            mesh.vertices = new[] { a - side, a + side, b - side, b + side };
            mesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateUVDistributionMetrics();
        }

        private void BuildFourPointMesh(Mesh mesh)
        {
            var verts = new Vector3[anchors.Count];
            for (int i = 0; i < anchors.Count; i++) verts[i] = anchors[i].position;

            mesh.vertices = verts;
            mesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
            mesh.RecalculateNormals();
        }
    }
}