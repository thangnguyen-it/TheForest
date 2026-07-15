using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.Interaction;
using TheForest.AI;
using TheForest.Items;
using TheForest.Player;

namespace TheForest.Building
{
    /// <summary>
    /// Bẫy thú. Thú nhỏ bắt được (Rabbit/Lizard/Raccoon) tự đi vào -> kẹt -> giết/đợi.
    /// Thú lớn (Deer/Boar) kích hoạt nhưng KHÔNG bị bắt. Player đứng gần -> thú né.
    /// Reset: nhìn vào bẫy + giữ E 'resetHoldTime' giây.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AnimalTrap : MonoBehaviour, IInteractable
    {
        public enum TrapMode { Land, Fish }

        [Header("Cấu hình")]
        [SerializeField] private TrapMode mode = TrapMode.Land;
        [Tooltip("Loài bắt được. Loài ngoài danh sách chỉ kích hoạt, không bị bắt.")]
        [SerializeField] private AnimalKind[] catchable =
        {
            AnimalKind.Rabbit,
            AnimalKind.Squirrel,
            AnimalKind.Raccoon,
            AnimalKind.Skunk,
            AnimalKind.Lizard
        };

        [Header("Trigger")]
        [SerializeField] private float triggerRadius = 1.2f;
        [Tooltip("Player trong bán kính này làm thú không dám vào.")]
        [SerializeField] private float playerDeterRadius = 6f;

        [Header("Trạng thái hiển thị")]
        [SerializeField] private GameObject armedVisual;
        [SerializeField] private GameObject triggeredVisual;
        [SerializeField] private GameObject caughtVisual;

        [Header("Reset")]
        [SerializeField] private float resetHoldTime = 3f;
        [Tooltip("Fish trap tốn 1 Stick để reset (gán item Stick).")]
        [SerializeField] private ItemData fishResetCost;

        [Header("Loot khi thu hoạch")]
        [SerializeField] private GameObject lootPickupPrefab;
        [Header("Bắt sống (Rabbit)")]
        [Tooltip("Item thỏ sống trả vào túi khi thu hoạch Rabbit (để cho vào Cage).")]
        [SerializeField] private ItemData livingRabbitItem;
        [Tooltip("Ưu tiên bắt sống Rabbit thay vì giết lấy thịt.")]
        [SerializeField] private bool captureAlive = true;


        private enum TrapState { Armed, Triggered, Caught }
        private TrapState _state = TrapState.Armed;
        private AnimalAI _caughtAnimal;
        private Transform _player;
        private float _holdTimer;

        private void Awake()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
            ApplyVisual();
        }

        private void Update()
        {
            if (_state != TrapState.Armed) return;

            // Player đứng gần -> thú né (không check bắt)
            if (_player != null && Vector3.Distance(transform.position, _player.position) < playerDeterRadius)
                return;

            CheckForAnimal();
        }

        private void CheckForAnimal()
        {
            var hits = Physics.OverlapSphere(transform.position, triggerRadius);
            foreach (var h in hits)
            {
                var ai = h.GetComponentInParent<AnimalAI>();
                if (ai == null || ai.State == AnimalState.Dead) continue;

                bool isCatchable = IsCatchable(ai.Kind);

                if (isCatchable)
                {
                    Catch(ai);
                }
                else
                {
                    // Thú lớn: kích hoạt nhưng không bắt được (GDD)
                    Trigger();
                }
                return;
            }
        }

        private void Catch(AnimalAI ai)
        {
            _state = TrapState.Caught;
            _caughtAnimal = ai;
            ai.OnDeath();                  // dừng AI (kẹt trong bẫy)
            ai.transform.position = transform.position; // hút vào bẫy
            ApplyVisual();
        }

        private void Trigger()
        {
            _state = TrapState.Triggered;  // bẫy bật mà rỗng (con vật thoát)
            ApplyVisual();
        }

        private bool IsCatchable(AnimalKind kind)
        {
            if (mode == TrapMode.Fish)
                return kind == AnimalKind.Salmon;

            return IsDefaultSmallCatchable(kind) || System.Array.IndexOf(catchable, kind) >= 0;
        }

        private static bool IsDefaultSmallCatchable(AnimalKind kind)
        {
            return kind == AnimalKind.Rabbit
                || kind == AnimalKind.Squirrel
                || kind == AnimalKind.Raccoon
                || kind == AnimalKind.Skunk
                || kind == AnimalKind.Lizard;
        }

        // ===================== IInteractable =====================
        public string GetPrompt()
        {
            switch (_state)
            {
                case TrapState.Caught: return "[E] Thu hoạch (giữ)";
                case TrapState.Triggered: return "[E] Đặt lại bẫy (giữ)";
                default: return string.Empty; // armed: không cần prompt
            }
        }

        public bool CanInteract() => _state != TrapState.Armed;

        public void Interact(GameObject interactor)
        {
            // Dùng kiểu "giữ E": InteractionRaycaster gọi liên tục khi giữ (nếu bạn nối auto-hold),
            // hoặc đơn giản 1 lần = hoàn tất. Ở đây hỗ trợ giữ qua HoldProgress.
            _holdTimer += Time.deltaTime;
            if (_holdTimer < resetHoldTime) return;
            _holdTimer = 0f;

            if (_state == TrapState.Caught) Harvest(interactor);
            else if (_state == TrapState.Triggered) ResetTrap(interactor);
        }

        public void OnFocus() { }
        public void OnLoseFocus() { _holdTimer = 0f; }

        private void Harvest(GameObject interactor)
        {
            if (_caughtAnimal != null)
            {
                var inv = interactor.GetComponent<Inventory>();
                bool isRabbit = _caughtAnimal.Kind == AnimalKind.Rabbit;

                if (captureAlive && isRabbit && livingRabbitItem != null && inv != null)
                {
                    int leftover = inv.Add(livingRabbitItem, 1); // thỏ sống vào túi
                    if (leftover > 0) { Debug.Log("[Trap] Túi đầy, không bắt sống được."); return; }
                    Destroy(_caughtAnimal.gameObject); // gỡ con vật khỏi bẫy (không drop thịt)
                    _caughtAnimal = null;
                }
                else
                {
                    var hp = _caughtAnimal.GetComponent<AnimalHealth>();
                    if (hp != null) hp.KillByTrap();
                    _caughtAnimal = null;
                }
            }
            ResetTrap(interactor);
        }


        private void ResetTrap(GameObject interactor)
        {
            // Fish trap tốn 1 Stick
            if (mode == TrapMode.Fish && fishResetCost != null)
            {
                var inv = interactor.GetComponent<Inventory>();
                if (inv == null || !inv.TryConsume(fishResetCost, 1))
                {
                    Debug.Log("[Trap] Cần 1 Stick để đặt lại bẫy cá.");
                    return;
                }
            }
            _state = TrapState.Armed;
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (armedVisual != null) armedVisual.SetActive(_state == TrapState.Armed);
            if (triggeredVisual != null) triggeredVisual.SetActive(_state == TrapState.Triggered);
            if (caughtVisual != null) caughtVisual.SetActive(_state == TrapState.Caught);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green; Gizmos.DrawWireSphere(transform.position, triggerRadius);
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, playerDeterRadius);
        }
    }
}
