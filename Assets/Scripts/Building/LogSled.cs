// ── Log Sled ─────────────────────────────────────────────────────────────
using System.Collections.Generic;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;
using UnityEngine;

public class LogSled : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private int maxLogs = 12;
    [SerializeField] private Transform attachPoint;

    private readonly List<LogPiece> _payload = new();
    private bool _attached;
    private Transform _puller;

    public bool IsFull => _payload.Count >= maxLogs;

    public bool AddLog(LogPiece piece)
    {
        if (IsFull) return false;
        _payload.Add(piece);
        piece.transform.SetParent(attachPoint, true);
        EventBus<LogStoredEvent>.Raise(new LogStoredEvent(piece.LogType, 1, gameObject));
        return true;
    }

    /// <summary>Player grabs sled handle — physics drag begins.</summary>
    public void Attach(Transform puller)
    {
        _attached = true;
        _puller = puller;
        EventBus<SledAttachedEvent>.Raise(new SledAttachedEvent(gameObject, maxLogs));
    }

    public void Detach() { _attached = false; _puller = null; }

    private void FixedUpdate()
    {
        if (!_attached || _puller == null) return;
        Vector3 target = _puller.position - _puller.forward * 1.5f;
        transform.position = Vector3.MoveTowards(transform.position, target, 3f * Time.fixedDeltaTime);
    }
}

 