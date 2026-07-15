using UnityEngine;
using TheForest.Interaction;
using TheForest.Items;
using TheForest.Player;

namespace TheForest.Building
{
    /// <summary>
    /// Chuồng thỏ: cho thỏ sống vào -> nhân giống theo thời gian -> thu hoạch thịt/lông.
    /// Nhìn vào + E: nếu cầm thỏ sống & còn chỗ -> bỏ vào; nếu đủ điều kiện -> thu hoạch.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RabbitCage : MonoBehaviour, IInteractable
    {
        [Header("Sức chứa & nhân giống")]
        [SerializeField] private int maxRabbits = 6;
        [SerializeField] private int rabbits = 0;
        [Tooltip("Cần tối thiểu 2 con để nhân giống.")]
        [SerializeField] private int minBreedCount = 2;
        [Tooltip("Thời gian (giây) để +1 thỏ khi đủ điều kiện.")]
        [SerializeField] private float breedInterval = 60f;

        [Header("Sản lượng định kỳ")]
        [Tooltip("Mỗi chu kỳ thu hoạch cho bao nhiêu thịt/lông theo số thỏ.")]
        [SerializeField] private float harvestInterval = 90f;
        [SerializeField] private ItemData meatItem;   // raw meat
        [SerializeField] private ItemData furItem;    // rabbit fur

        [Header("Item & hiển thị")]
        [SerializeField] private ItemData livingRabbitItem; // để nhận biết khi cầm
        [SerializeField] private GameObject[] rabbitVisuals; // bật dần theo số con

        private float _breedTimer;
        private float _harvestTimer;
        private int _pendingMeat;
        private int _pendingFur;

        private void Update()
        {
            // Nhân giống
            if (rabbits >= minBreedCount && rabbits < maxRabbits)
            {
                _breedTimer += Time.deltaTime;
                if (_breedTimer >= breedInterval)
                {
                    _breedTimer = 0f;
                    rabbits++;
                    ApplyVisual();
                }
            }
            else _breedTimer = 0f;

            // Tích sản lượng
            if (rabbits > 0)
            {
                _harvestTimer += Time.deltaTime;
                if (_harvestTimer >= harvestInterval)
                {
                    _harvestTimer = 0f;
                    _pendingMeat += Mathf.Max(1, rabbits / 2);
                    _pendingFur += Mathf.Max(1, rabbits / 3);
                }
            }
        }

        // ===================== IInteractable =====================
        public string GetPrompt()
        {
            if (_pendingMeat > 0 || _pendingFur > 0) return "[E] Thu hoạch chuồng thỏ";
            return $"[E] Thả thỏ vào chuồng ({rabbits}/{maxRabbits})";
        }

        public bool CanInteract() => true;

        public void Interact(GameObject interactor)
        {
            var inv = interactor.GetComponent<Inventory>();
            if (inv == null) return;

            // Ưu tiên thu hoạch nếu có sản lượng
            if (_pendingMeat > 0 || _pendingFur > 0)
            {
                if (_pendingMeat > 0 && meatItem != null) inv.Add(meatItem, _pendingMeat);
                if (_pendingFur > 0 && furItem != null) inv.Add(furItem, _pendingFur);
                _pendingMeat = 0; _pendingFur = 0;
                return;
            }

            // Thả thỏ sống vào chuồng (nếu cầm thỏ sống & còn chỗ)
            if (livingRabbitItem != null && inv.Has(livingRabbitItem, 1) && rabbits < maxRabbits)
            {
                if (inv.TryConsume(livingRabbitItem, 1))
                {
                    rabbits++;
                    ApplyVisual();
                }
            }
            else if (rabbits >= maxRabbits)
            {
                Debug.Log("[Cage] Chuồng đầy.");
            }
            else
            {
                Debug.Log("[Cage] Cần thỏ sống để thả vào.");
            }
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }

        private void ApplyVisual()
        {
            if (rabbitVisuals == null) return;
            for (int i = 0; i < rabbitVisuals.Length; i++)
                if (rabbitVisuals[i] != null) rabbitVisuals[i].SetActive(i < rabbits);
        }
    }
}
