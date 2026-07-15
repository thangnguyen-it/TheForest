// ═══════════════════════════════════════════════════════════════════════════
// 2.10 — TARP ROPE SYSTEM (orchestrator)
// ═══════════════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using TheForest.Building;
using TheForest.Building.Data;
using TheForest.Building.Systems;
using UnityEngine;

/// <summary>
/// 2.10 — Tarp &amp; Rope System.
///
/// Tarp modes:
///   BasicShelter  — Tarp + 1–2 sticks  → lean-to sleep spot
///   FourCorner    — 4 log anchors       → roof or wall
///   Trampoline    — 4 log anchors level → bounce collider added
///
/// Rope Bridge: 2 parallel rope anchors → fill with split logs → walkable bridge.
/// Zipline:     Rope Gun fires anchor; second shot = endpoint → ZiplineRide starts.
/// </summary>
public class TarpRopeSystem : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject tarpPiecePrefab;
    [SerializeField] private GameObject ropeSegmentPrefab;

    [Header("Detection")]
    [SerializeField] private float anchorDetectRadius = 1.5f;

    // Rope Gun state
    private Vector3? _ziplineAnchorA;
    private GameObject _currentZipliner;

    // Active ropes (for bridge detection)
    private readonly List<RopeSegment> _ropes = new();

    // ── Tarp ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Player drops tarp near sticks/logs. System detects nearby anchors
    /// and determines mode.
    /// </summary>
    public void DeployTarp(Vector3 dropPosition)
    {
        var anchors = FindAnchorPoints(dropPosition, anchorDetectRadius);

        TarpMode mode;
        if (anchors.Count >= 4) mode = DetermineAdvancedMode(anchors);
        else if (anchors.Count >= 1) mode = TarpMode.BasicShelter;
        else mode = TarpMode.BasicShelter;

        if (tarpPiecePrefab == null) return;
        var go = Instantiate(tarpPiecePrefab, dropPosition, Quaternion.identity);
        var tarp = go.GetComponent<TarpPiece>() ?? go.AddComponent<TarpPiece>();
        tarp.Initialize(mode, anchors);
    }

    private TarpMode DetermineAdvancedMode(List<Transform> anchors)
    {
        // Check if all 4 anchors are co-planar and horizontal → trampoline
        float maxDeltaY = 0f;
        for (int i = 1; i < anchors.Count; i++)
            maxDeltaY = Mathf.Max(maxDeltaY,
                Mathf.Abs(anchors[i].position.y - anchors[0].position.y));
        return maxDeltaY < 0.3f ? TarpMode.Trampoline : TarpMode.FourCorner;
    }

    private List<Transform> FindAnchorPoints(Vector3 origin, float radius)
    {
        var results = new List<Transform>();
        var buf = new Collider[12];
        int count = Physics.OverlapSphereNonAlloc(origin, radius, buf);
        for (int i = 0; i < count; i++)
        {
            var piece = buf[i].GetComponentInParent<LogPiece>();
            if (piece != null) results.Add(piece.transform);
        }
        return results;
    }

    // ── Rope ─────────────────────────────────────────────────────────────
    public void AttachRope(Vector3 anchorA, Vector3 anchorB, bool isBridge)
    {
        if (ropeSegmentPrefab == null) return;
        var go = Instantiate(ropeSegmentPrefab, anchorA, Quaternion.identity);
        var rope = go.GetComponent<RopeSegment>() ?? go.AddComponent<RopeSegment>();
        rope.Initialize(anchorA, anchorB, isBridge, false);
        _ropes.Add(rope);
    }

    // ── Zipline ──────────────────────────────────────────────────────────
    public void FireRopeGun(Vector3 hitPoint)
    {
        if (_ziplineAnchorA == null)
        {
            _ziplineAnchorA = hitPoint;
            Debug.Log("[TarpRope] Zipline anchor A set at " + hitPoint);
        }
        else
        {
            CreateZipline(_ziplineAnchorA.Value, hitPoint);
            _ziplineAnchorA = null;
        }
    }

    private void CreateZipline(Vector3 a, Vector3 b)
    {
        if (ropeSegmentPrefab == null) return;
        var go = Instantiate(ropeSegmentPrefab, a, Quaternion.identity);
        var rope = go.GetComponent<RopeSegment>() ?? go.AddComponent<RopeSegment>();
        rope.Initialize(a, b, false, true);
        _ropes.Add(rope);
    }

    /// <summary>Player approaches zipline — start ride.</summary>
    public void StartZiplineRide(RopeSegment zipline, Transform rider, CharacterController cc)
    {
        if (!zipline.IsZipline) return;
        zipline.StartZiplineRide(this, rider, cc);
    }
}
