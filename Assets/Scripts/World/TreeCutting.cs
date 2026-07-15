using UnityEngine;
using TheForest.Interaction;

namespace TheForest.World
{
    public class TreeCutting : MonoBehaviour, IInteractable, IChoppable
    {
        [Header("Sức bền cây")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        [Header("Loại cây")]
        [SerializeField] private bool isSmallTree;

        [Header("Model & vật lý đổ")]
        [SerializeField] private GameObject standingModel;
        [SerializeField] private GameObject stumpModel;
        [SerializeField] private GameObject fallingLogPrefab;
        [SerializeField] private float fallForce = 6f;
        [SerializeField] private float fallenLogLifetime = 20f;

        [Header("Loot rơi ra - Log (FIX Block 1: WORLD OBJECT, KHÔNG phải Inventory Item nữa)")]
        [Tooltip("Prefab log Full: PHẢI có component Building.WorldLog + Rigidbody + Collider. " +
                 "Đây là fix cốt lõi unblock toàn bộ pipeline vác gỗ (PlayerLogCarry) & building freeform.")]
        [SerializeField] private GameObject logPrefab;
        [SerializeField] private int logAmount = 3;
        [Tooltip("Lực văng log ra khi cây đổ (physics impulse ngẫu nhiên).")]
        [SerializeField] private float logScatterForce = 2.5f;

        [Header("Loot rơi ra - Stick (Inventory Item, không đổi)")]
        [SerializeField] private Items.ItemData stickItem;
        [Tooltip("Cây nhỏ/bụi cây (isSmallTree=true): không cho log, cộng thêm số stick này để bù.")]
        [SerializeField] private int stickAmount = 2;
        [SerializeField] private GameObject lootPickupPrefab;

        public TreeState State { get; private set; } = TreeState.Standing;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        // ===================== IInteractable =====================
        public void Interact(GameObject interactor)
        {
            if (State != TreeState.Standing) return;

            var swinger = interactor.GetComponent<TheForest.Player.WeaponSwinger>();
            if (swinger == null)
            {
                Debug.LogWarning("[TreeCutting] Không tìm thấy thành phần WeaponSwinger trên người chơi tương tác.");
                return;
            }

            swinger.RequestSwing(this);
        }

        public string GetPrompt()
        {
            return "Chặt cây";
        }

        public bool CanInteract()
        {
            return State == TreeState.Standing;
        }

        public void OnFocus()
        {
        }

        public void OnLoseFocus()
        {
        }

        // ===================== IChoppable =====================
        public bool CanBeChopped()
        {
            return State == TreeState.Standing;
        }

        public bool ApplyChop(float damage, Transform attacker)
        {
            if (State != TreeState.Standing) return false;
            currentHealth = Mathf.Max(0f, currentHealth - damage);
            if (currentHealth <= 0f)
            {
                FellTree(attacker);
                return true;   // nhát hạ gục
            }
            return false;
        }


        private void FellTree(Transform attacker)
        {
            State = TreeState.Stump; // Chuyển sang trạng thái gốc cây chuẩn theo Enum gốc của dự án

            if (standingModel != null) standingModel.SetActive(false);
            if (stumpModel != null) stumpModel.SetActive(true);

            if (fallingLogPrefab != null)
            {
                GameObject fallingLog = Instantiate(fallingLogPrefab, transform.position, transform.rotation);
                Rigidbody rb = fallingLog.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 fallDirection = (transform.position - attacker.position).normalized;
                    fallDirection.y = 0f;
                    rb.AddForce(fallDirection * fallForce, ForceMode.Impulse);
                }
                Destroy(fallingLog, fallenLogLifetime);
            }

            SpawnLoot();
        }

        /// <summary>
        /// FIX Block 1 (2 lỗi trong bản cũ):
        /// 1) Log giờ là WORLD OBJECT thật (Building.WorldLog, vác bằng PlayerLogCarry) thay vì ItemPickup giả.
        /// 2) Stick vẫn là ItemPickup nhưng nay ĐƯỢC gọi .Configure(stickItem, amount) đúng cách — bản cũ chỉ
        ///    Instantiate(lootPickupPrefab) suông, không bao giờ set item nên pickup luôn rỗng (item == null),
        ///    khiến GetPrompt()/CanInteract() hỏng. Cũng tận dụng luôn field isSmallTree trước đây khai báo
        ///    nhưng chưa dùng tới ở đâu cả.
        /// </summary>
        private void SpawnLoot()
        {
            if (isSmallTree)
            {
                // Cây nhỏ / bụi cây theo GDD: không rơi log, chỉ bù thêm sticks.
                SpawnSticks(stickAmount + 2);
            }
            else
            {
                SpawnLogs();
                SpawnSticks(stickAmount);
            }
        }

        private void SpawnLogs()
        {
            if (logPrefab == null) return;

            for (int i = 0; i < logAmount; i++)
            {
                // Rải log dọc theo thân cây đổ, lệch nhẹ 2 bên cho tự nhiên thay vì chồng lên nhau.
                Vector3 alongTrunk = transform.forward * (i * 2.2f);
                Vector3 sideOffset = transform.right * Random.Range(-0.4f, 0.4f);
                Vector3 spawnPos = transform.position + alongTrunk + sideOffset + Vector3.up * 0.3f;

                var go = Instantiate(logPrefab, spawnPos, Quaternion.LookRotation(transform.right));

                var worldLog = go.GetComponent<Building.WorldLog>();
                if (worldLog != null) worldLog.SetCutType(Building.LogCutType.Full);
                else Debug.LogWarning("[TreeCutting] logPrefab thiếu component WorldLog — kiểm tra lại prefab.");

                var rb = go.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 scatter = (Random.insideUnitSphere + Vector3.up) * logScatterForce;
                    rb.AddForce(scatter, ForceMode.Impulse);
                }
            }
        }

        private void SpawnSticks(int amount)
        {
            if (stickItem == null || amount <= 0 || lootPickupPrefab == null) return;

            Vector3 spawnOffset = new Vector3(Random.Range(-0.5f, 0.5f), 0.2f, Random.Range(-0.5f, 0.5f));
            var go = Instantiate(lootPickupPrefab, transform.position + spawnOffset, Quaternion.identity);
            var pickup = go.GetComponent<Items.ItemPickup>();
            if (pickup != null) pickup.Configure(stickItem, amount);
            else Debug.LogWarning("[TreeCutting] lootPickupPrefab thiếu component ItemPickup — kiểm tra lại prefab.");
        }

        /// <summary>
        /// Phương thức hồi sinh cây phục vụ đồng bộ cho hệ thống TreeRegrowManager gọi qua mỗi chu kỳ ngày mới.
        /// </summary>
        public void Regrow()
        {
            State = TreeState.Standing;
            currentHealth = maxHealth;

            if (standingModel != null) standingModel.SetActive(true);
            if (stumpModel != null) stumpModel.SetActive(false);

            Debug.Log($"[Tree] {gameObject.name} đã được hệ thống hồi sinh thành công về trạng thái cổ thụ đứng thẳng!");
        }
    }
}