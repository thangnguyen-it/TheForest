using UnityEngine;
using TheForest.Building.Systems;

namespace TheForest.World
{
    /// <summary>
    /// Kiểm tra "an toàn để ngủ" theo GDD (Block 3): game chỉ coi là an toàn khi vị trí HOÀN TOÀN
    /// được bao quanh bởi tường phòng thủ (Defensive Wall) hoặc trong công trình có cửa ĐANG KHOÁ.
    /// Tường thường KHÔNG đủ vì cannibal có thể trèo qua (theo GDD) — ở đây đơn giản hoá bằng cách
    /// raycast ra các hướng quanh giường; nếu MỌI hướng đều bị chặn trong checkDistance -> coi là kín.
    ///
    /// Đây là bản đơn giản hoá thực dụng (giống tinh thần các "simplified" khác đã có trong dự án,
    /// vd WallBuilder.FindPalisadeRowRoot cũ) — nếu sau này có StructuralDependencyGraph flood-fill
    /// đầy đủ thì có thể thay IsFullyWalled() bằng truy vấn đồ thị thật.
    ///
    /// Ngủ ở nơi KHÔNG an toàn: tối đa maxInterruptionsPerNight lần bị đánh thức bởi cannibal search party.
    /// </summary>
    public class SafeSleepChecker : MonoBehaviour
    {
        [Header("Kiểm tra bao quanh (Defensive Wall)")]
        [Tooltip("Các hướng (local space) cần bị chặn để coi là kín. Mặc định 4 hướng ngang.")]
        [SerializeField]
        private Vector3[] checkDirections =
        {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right
        };
        [SerializeField] private float checkDistance = 6f;
        [SerializeField] private LayerMask wallMask = ~0;

        [Header("Cửa khoá (thay thế cho bao quanh tường)")]
        [Tooltip("Nếu BẤT KỲ cửa nào trong danh sách đang khoá -> coi là an toàn (giường nằm trong cùng công trình).")]
        [SerializeField] private DoorPiece[] relevantDoors;

        [Header("Quấy rối khi ngủ không an toàn")]
        [SerializeField] private int maxInterruptionsPerNight = 2;
        [Range(0f, 1f)][SerializeField] private float interruptionChancePerRoll = 0.5f;

        public bool IsSafeNow => IsFullyWalled() || HasLockedDoor();

        private bool IsFullyWalled()
        {
            if (checkDirections == null || checkDirections.Length == 0) return false;

            foreach (var dir in checkDirections)
            {
                Vector3 worldDir = transform.TransformDirection(dir.normalized);
                if (!Physics.Raycast(transform.position, worldDir, checkDistance, wallMask, QueryTriggerInteraction.Ignore))
                    return false; // hướng này hở -> không kín
            }
            return true;
        }

        private bool HasLockedDoor()
        {
            if (relevantDoors == null) return false;
            foreach (var d in relevantDoors)
                if (d != null && d.IsLocked) return true;
            return false;
        }

        /// <summary>Gọi khi player ngủ ở nơi KHÔNG an toàn: trả về số lần bị quấy rối đêm đó (0..maxInterruptionsPerNight).</summary>
        public int RollNightInterruptions()
        {
            int count = 0;
            for (int i = 0; i < maxInterruptionsPerNight; i++)
                if (Random.value < interruptionChancePerRoll) count++;
            return count;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsSafeNow ? Color.green : new Color(1f, 0.4f, 0f, 0.8f);
            if (checkDirections == null) return;
            foreach (var dir in checkDirections)
                Gizmos.DrawRay(transform.position, transform.TransformDirection(dir.normalized) * checkDistance);
        }
    }
}
