// ─── SnapPoint.cs ─────────────────────────────────────────────────────────────
using System.Collections.Generic;
using TheForest.Building.Core;
using TheForest.Building.Events;
using UnityEngine;

namespace TheForest.Building.Data
{
    /// <summary>Attachment point on a building piece where other pieces can snap.</summary>
    public class SnapPoint : MonoBehaviour
    {
        public enum SnapType { Top, Bottom, SideLeft, SideRight, End, Corner }

        [SerializeField] private SnapType type;
        [SerializeField] private PlacementMode allowedOrientation = PlacementMode.Horizontal;
        [SerializeField] private bool isOccupied;

        public SnapType Type => type;
        public PlacementMode AllowedMode => allowedOrientation;
        public bool IsOccupied => isOccupied;
        public LogPiece OwnerPiece { get; private set; }

        private void Awake() => OwnerPiece = GetComponentInParent<LogPiece>();

        /// <summary>Try to occupy this snap point. Returns false if already occupied.</summary>
        public bool TryOccupy()
        {
            if (isOccupied) return false;
            isOccupied = true;
            return true;
        }

        public void Release() => isOccupied = false;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isOccupied ? Color.red : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.12f);
        }
    }
}