using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.AI;

namespace TheForest.Player
{
    /// <summary>
    /// Ám sát từ phía sau: khi nhìn vào lưng một cannibal CHƯA cảnh giác trong tầm gần
    /// và đang cầm vũ khí cận chiến -> nhấn phím (E/F) để one-shot.
    /// Ưu tiên chạy TRƯỚC interaction/swing thường.
    /// </summary>
    public class StealthKillController : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private EquipmentController equipment;

        [Header("Điều kiện")]
        [SerializeField] private float range = 2f;
        [Tooltip("Góc tối đa giữa hướng player nhìn và LƯNG địch để tính 'từ phía sau'.")]
        [SerializeField] private float behindAngle = 60f;
        [SerializeField] private LayerMask enemyMask = ~0;

        [Header("SFX")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip stealthSfx;

        private CannibalAI _target;
        private CannibalHealth _targetHealth;

        public bool HasStealthTarget => _target != null;
        public string Prompt => HasStealthTarget ? "[E] Ám sát" : string.Empty;

        private void Awake()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (equipment == null) equipment = GetComponent<EquipmentController>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void Update() => DetectTarget();

        private void DetectTarget()
        {
            _target = null; _targetHealth = null;
            if (cameraTransform == null) return;

            // Cần cầm vũ khí cận chiến (không phải tay không, không ranged-only)
            if (equipment == null || !equipment.HasEquipped) return;

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, range, enemyMask, QueryTriggerInteraction.Ignore))
                return;

            var ai = hit.collider.GetComponentInParent<CannibalAI>();
            var hp = hit.collider.GetComponentInParent<CannibalHealth>();
            if (ai == null || hp == null) return;
            if (!ai.IsUnaware || !hp.CanBeStealthKilled) return;

            // Kiểm tra "từ phía sau": player đứng sau lưng địch và nhìn cùng hướng lưng
            Vector3 toEnemy = (ai.transform.position - transform.position); toEnemy.y = 0f;
            Vector3 enemyFwd = ai.Forward; enemyFwd.y = 0f;
            float angle = Vector3.Angle(enemyFwd.normalized, toEnemy.normalized);
            // angle nhỏ nghĩa là player ở PHÍA TRƯỚC mặt địch; ta cần player ở SAU:
            // player ở sau -> vector từ địch tới player ngược hướng enemyFwd
            Vector3 enemyToPlayer = (transform.position - ai.transform.position); enemyToPlayer.y = 0f;
            float backAngle = Vector3.Angle(-enemyFwd.normalized, enemyToPlayer.normalized);
            if (backAngle > behindAngle * 0.5f) return; // không ở sau lưng

            _target = ai; _targetHealth = hp;
        }

        // Input System action "StealthKill" (có thể map chung phím E nhưng ưu tiên kiểm tra trước)
        public void OnStealthKill(InputValue value)
        {
            if (!value.isPressed) return;
            TryExecute();
        }

        public bool TryExecute()
        {
            if (_target == null || _targetHealth == null) return false;
            bool ok = _targetHealth.TryStealthKill(transform);
            if (ok && audioSource != null && stealthSfx != null)
                audioSource.PlayOneShot(stealthSfx);
            _target = null; _targetHealth = null;
            return true;
        }
    }
}
