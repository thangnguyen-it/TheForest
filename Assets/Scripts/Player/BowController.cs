using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.Items;
using TheForest.AI;

namespace TheForest.Player
{
    /// <summary>
    /// Bắn cung: chỉ hoạt động khi đang cầm BowItemData. Giữ LMB kéo căng (charge),
    /// nhả bắn arrow đang chọn (tiêu 1 arrow từ Inventory). Chọn loại tên bằng phím.
    /// </summary>
    public class BowController : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private EquipmentController equipment;
        [SerializeField] private Inventory inventory;
        [SerializeField] private Transform shootOrigin; // camera/eye
        [SerializeField] private WeaponSwinger swinger;  // để tắt swing khi cầm cung

        [Header("Loại tên đang chọn")]
        [SerializeField] private ArrowItemData[] arrowTypes; // normal/poison/fire
        [SerializeField] private bool loadArrowsFromResources = true;
        [SerializeField] private string arrowResourcePath = "SotFData/Items";
        [SerializeField] private int selectedArrow = 0;

        private float _charge;
        private bool _drawing;

        public bool HoldingBow => equipment != null && equipment.EquippedItem is BowItemData;

        private void Awake()
        {
            if (equipment == null) equipment = GetComponent<EquipmentController>();
            if (inventory == null) inventory = GetComponent<Inventory>();
            if (shootOrigin == null && Camera.main != null) shootOrigin = Camera.main.transform;
            LoadResourceArrows();
        }

        private void LoadResourceArrows()
        {
            if (!loadArrowsFromResources) return;

            var resourceArrows = Resources.LoadAll<ArrowItemData>(arrowResourcePath);
            if (resourceArrows == null || resourceArrows.Length == 0) return;

            var merged = new System.Collections.Generic.List<ArrowItemData>();
            if (arrowTypes != null)
            {
                foreach (var arrow in arrowTypes)
                    if (arrow != null && !merged.Contains(arrow)) merged.Add(arrow);
            }

            foreach (var arrow in resourceArrows)
                if (arrow != null && !merged.Contains(arrow)) merged.Add(arrow);

            arrowTypes = merged.ToArray();
            selectedArrow = Mathf.Clamp(selectedArrow, 0, arrowTypes.Length - 1);
        }

        private void Update()
        {
            if (_drawing && HoldingBow)
            {
                var bow = (BowItemData)equipment.EquippedItem;
                _charge = Mathf.Min(1f, _charge + Time.deltaTime / bow.drawTime);
            }
        }

        // Tái dùng input "Attack" (LMB) nhưng chỉ khi cầm cung
        public void OnAttack(InputValue value)
        {
            if (!HoldingBow) return; // không cầm cung -> để WeaponSwinger xử lý
            if (value.isPressed) { _drawing = true; _charge = 0f; }
            else if (_drawing) { _drawing = false; Fire(); }
        }

        // Đổi loại tên (phím riêng, vd Q)
        public void OnCycleArrow(InputValue value)
        {
            if (!value.isPressed || arrowTypes == null || arrowTypes.Length == 0) return;
            selectedArrow = (selectedArrow + 1) % arrowTypes.Length;
        }

        private void Fire()
        {
            if (arrowTypes == null || arrowTypes.Length == 0) return;
            var arrow = arrowTypes[selectedArrow];
            if (arrow == null || arrow.arrowProjectilePrefab == null) return;

            // Cần có tên trong túi
            if (inventory == null || !inventory.TryConsume(arrow, 1))
            {
                Debug.Log("[Bow] Hết tên loại này.");
                return;
            }

            var bow = (BowItemData)equipment.EquippedItem;
            float speed = Mathf.Lerp(bow.minLaunchSpeed, bow.maxLaunchSpeed, _charge);
            float damage = (bow.bowDamage + arrow.arrowDamage) * Mathf.Lerp(0.5f, 1f, _charge);

            Vector3 dir = shootOrigin.forward;
            var go = Instantiate(arrow.arrowProjectilePrefab,
                shootOrigin.position + dir * 0.5f, Quaternion.LookRotation(dir));
            var arr = go.GetComponent<Arrow>();
            if (arr != null) arr.Launch(dir * speed, damage, arrow, transform.root);

            _charge = 0f;
            // emit noise: bắn tên gây tiếng nhỏ (kể cả notch — GDD); để player emit ở nơi khác
        }
    }
}
