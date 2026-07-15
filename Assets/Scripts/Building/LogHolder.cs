// ═══════════════════════════════════════════════════════════════════════════════
// 2.11 — LOG STORAGE & TRANSPORT
// ═══════════════════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using TheForest.Building.Core;
using TheForest.Building.Data;
using TheForest.Building.Events;
using TheForest.Interaction;
using UnityEngine;

namespace TheForest.Building.Systems
{
    /// <summary>
    /// LogHolder — persistent storage that survives log-out (saved to disk via ISaveable).
    /// Kelvin's "Get Logs → Fill Holder" deposits here.
    /// Stacked logs render as visible pile prefabs at holder position.
    /// </summary>
    public class LogHolder : MonoBehaviour, IInteractable
    {
        [Header("Config")]
        [SerializeField] private int maxCapacity = 20;
        [SerializeField] private float stackSpacingY = 0.2f;

        [Header("Visual")]
        [SerializeField] private GameObject logVisualPrefab;
        [SerializeField] private Transform stackOrigin;

        // Persistent storage — serialised by SaveSystem
        [SerializeField] private List<LogType> _storedLogs = new();

        private readonly List<GameObject> _visuals = new();

        public int Count => _storedLogs.Count;
        public int Capacity => maxCapacity;
        public bool IsFull => _storedLogs.Count >= maxCapacity;

        // ── Storage ───────────────────────────────────────────────────────────
        public bool TryStore(LogType type)
        {
            if (IsFull) return false;
            _storedLogs.Add(type);
            SpawnVisual(type, _storedLogs.Count - 1);
            EventBus<LogStoredEvent>.Raise(new LogStoredEvent(type, 1, gameObject));
            return true;
        }

        public bool TryRetrieve(out LogType type)
        {
            type = LogType.Full;
            if (_storedLogs.Count == 0) return false;
            type = _storedLogs[_storedLogs.Count - 1];
            _storedLogs.RemoveAt(_storedLogs.Count - 1);
            RemoveTopVisual();
            EventBus<LogRetrievedEvent>.Raise(new LogRetrievedEvent(type, 1));
            return true;
        }

        /// <summary>Called by Kelvin to deposit logs in bulk.</summary>
        public int FillFrom(List<LogType> logs)
        {
            int deposited = 0;
            foreach (var t in logs)
            {
                if (!TryStore(t)) break;
                deposited++;
            }
            return deposited;
        }

        // ── Visual ────────────────────────────────────────────────────────────
        private void SpawnVisual(LogType type, int index)
        {
            if (logVisualPrefab == null || stackOrigin == null) return;
            var pos = stackOrigin.position + Vector3.up * (index * stackSpacingY);
            var go = Instantiate(logVisualPrefab, pos, Quaternion.identity, stackOrigin);
            _visuals.Add(go);
        }

        private void RemoveTopVisual()
        {
            if (_visuals.Count == 0) return;
            Destroy(_visuals[_visuals.Count - 1]);
            _visuals.RemoveAt(_visuals.Count - 1);
        }

        // ── IInteractable ─────────────────────────────────────────────────────
        public string GetPrompt() => $"[E] Log Holder ({_storedLogs.Count}/{maxCapacity})";
        public bool CanInteract() => _storedLogs.Count > 0;

        public void Interact(GameObject interactor)
        {
            // FIX (Block 7): bug cũ gọi inv.Add(null, 1) — thêm "item null" vào túi, không làm gì cả và
            // sai hoàn toàn về mặt khái niệm vì LOG KHÔNG PHẢI inventory item trong hệ Building System này
            // (log là world object LogPiece, "cầm" log nghĩa là giữ 1 ghost qua FreeformPlacementSystem).
            //
            // Fix: lấy log ra khỏi holder xong giao THẲNG cho FreeformPlacementSystem để người chơi
            // CẦM SẴN ghost log đó và đặt ngay — đúng luồng "hold log" đã có sẵn trong hệ thống
            // (GiveLog được FreeformPlacementSystem.OnPlace/OnDropLog dùng lại y hệt khi nhặt log ngoài đời).
            // Fallback (nếu người chơi chưa có FreeformPlacementSystem gắn sẵn): sinh LogPiece vật lý
            // ra đất trước mặt, giống hệt cách FreeformPlacementSystem tự spawn khi PlacePiece().
            if (!TryRetrieve(out var type)) return;

            var placement = interactor.GetComponent<FreeformPlacementSystem>();
            if (placement != null)
            {
                placement.GiveLog(type);
                return;
            }

            SpawnPhysicalFallback(interactor, type);
        }

        private void SpawnPhysicalFallback(GameObject interactor, LogType type)
        {
            var db = FindFirstObjectByType<MaterialDatabase>();
            var prefab = db != null ? db.GetPrefab(TypeToId(type)) : null;
            if (prefab == null)
            {
                Debug.LogWarning("[LogHolder] Không có FreeformPlacementSystem lẫn prefab hợp lệ để trả log ra.");
                return;
            }

            Vector3 pos = interactor.transform.position + interactor.transform.forward * 1.5f;
            var go = Instantiate(prefab, pos, Quaternion.identity);
            var piece = go.GetComponent<LogPiece>();
            if (piece == null) piece = go.AddComponent<LogPiece>();
            piece.Initialize(type, PlacementMode.Horizontal, 100f);
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }

        private static string TypeToId(LogType t) => t switch
        {
            LogType.Full => "full_log",
            LogType.Split => "split_log",
            LogType.Half => "half_log",
            LogType.Stick => "stick",
            _ => "full_log"
        };
    }
}