using System.Collections;
using UnityEngine;
using TheForest.Items;
using TheForest.Interaction;

namespace TheForest.Player
{
    /// <summary>
    /// Điều phối động tác vung vũ khí: cooldown theo swingSpeed, animation,
    /// và áp damage ở giữa animation (đúng thời điểm lưỡi rìu chạm).
    /// Gắn lên Player. Đọc trực tiếp dữ liệu hiển thị từ EquipmentController.
    /// Tích hợp cơ chế kiểm tra và áp dụng x2 sát thương phản đòn sau Perfect Block.
    /// Tích hợp hệ thống phát tiếng ồn bị triệt tiêu bởi Bùn (MudCamo).
    ///
    /// FIX (Phần A.4): đã xóa SetWeaponVisual(GameObject) — thân hàm trước đây hoàn toàn rỗng.
    ///
    /// GIAI ĐOẠN 3: tổng quát hoá từ "chỉ đọc AxeItemData" sang đọc MỌI MeleeWeaponItemData (Machete,
    /// Katana, Club, Chainsaw...) — vũ khí cận chiến mới giờ chỉ cần TẠO ASSET, không cần sửa file này
    /// nữa. Thêm 2 cơ chế đã kiểm chứng qua tra cứu: Chainsaw KHÔNG tốn stamina (freeStamina), và
    /// Crafted Spear/Club/Guitar có thể GÃY sau vài đòn trúng (maxSwingsBeforeBreak).
    /// </summary>
    public class WeaponSwinger : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private EquipmentController equipment;
        [SerializeField] private SurvivalStats stats;
        [SerializeField] private PlayerBlock playerBlock;

        [Header("Tàng hình (Âm thanh)")]
        [SerializeField] private TheForest.Player.PlayerMudCamo mudCamo;

        [Header("Chi phí")]
        [Tooltip("Stamina trừ mỗi nhát vung (bỏ qua nếu vũ khí có freeStamina=true, vd Chainsaw).")]
        [SerializeField] private float staminaPerSwing = 8f;
        [SerializeField] private float energyPerSwing = 1.5f;

        [Header("Tween fallback (khi không có Animator)")]
        [SerializeField] private float tweenAngle = 60f;

        [Header("Cấu hình SFX")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] swingSfx;
        [SerializeField] private AudioClip[] hitSfx;
        [SerializeField] private AudioClip weaponBreakSfx;

        [Header("Hiệu ứng trúng đòn")]
        [SerializeField] private GameObject hitVfxPrefab;
        [SerializeField] private Transform _aimSource;

        [Header("Feedback khi trúng")]
        [SerializeField] private CameraShaker cameraShaker;
        [SerializeField] private TheForest.UI.HitFeedbackUI hitFeedback;
        [Tooltip("Trauma cộng mỗi nhát chặt trúng (0..1).")]
        [SerializeField] private float chopShake = 0.25f;
        [Tooltip("Trauma khi nhát đó làm ĐỔ cây (mạnh hơn).")]
        [SerializeField] private float fellShake = 0.6f;

        [Header("FOV kick")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float fovKick = 3f;
        [SerializeField] private float fovReturnSpeed = 8f;
        private float _baseFov;

        public bool IsSwinging { get; private set; }
        private float _cooldownTimer;

        // GIAI ĐOẠN 3: theo dõi số đòn TRÚNG còn lại trước khi vũ khí hiện tại gãy.
        private ItemData _trackedWeapon;
        private int _swingsRemaining = -1; // -1 = không giới hạn

        private static readonly int SwingHash = Animator.StringToHash("Swing");
        private static readonly int SpeedHash = Animator.StringToHash("SwingSpeed");

        private void Awake()
        {
            if (equipment == null) equipment = GetComponent<EquipmentController>();
            if (stats == null) stats = GetComponent<SurvivalStats>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (_aimSource == null && Camera.main != null) _aimSource = Camera.main.transform;
            if (cameraShaker == null) cameraShaker = GetComponentInChildren<CameraShaker>();
            if (viewCamera == null && Camera.main != null) viewCamera = Camera.main;
            if (viewCamera != null) _baseFov = viewCamera.fieldOfView;
            if (playerBlock == null) playerBlock = GetComponent<PlayerBlock>();

            if (mudCamo == null) mudCamo = GetComponent<TheForest.Player.PlayerMudCamo>();
            if (mudCamo == null) mudCamo = GetComponentInParent<TheForest.Player.PlayerMudCamo>();
        }

        private void Update()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            // Đưa vũ khí về lại FOV mặc định nếu đang không áp hiệu ứng kick
            if (viewCamera != null && !IsSwinging)
            {
                viewCamera.fieldOfView = Mathf.MoveTowards(viewCamera.fieldOfView, _baseFov, fovReturnSpeed * Time.deltaTime);
            }
        }

        /// <summary>Có thể vung lúc này không (đủ điều kiện cooldown/energy/vũ khí).</summary>
        public bool CanSwing()
        {
            if (IsSwinging || _cooldownTimer > 0f) return false;
            if (equipment == null || !equipment.HasEquipped) return false;
            if (stats != null && !stats.CanExert) return false;                  // hết energy
            var melee = equipment.EquippedItem as MeleeWeaponItemData;
            bool skipStaminaCheck = melee != null && melee.freeStamina;
            if (!skipStaminaCheck && stats != null && stats.StaminaCurrent < staminaPerSwing) return false; // hết stamina
            return true;
        }

        /// <summary>
        /// Yêu cầu vung vào một target. Trả true nếu nhát vung được khởi động.
        /// Damage áp sau (giữa animation), không tức thời.
        /// </summary>
        public bool RequestSwing(IChoppable target)
        {
            if (!CanSwing()) return false;

            var melee = equipment.EquippedItem as MeleeWeaponItemData; // GIAI ĐOẠN 3: mọi vũ khí cận chiến, không chỉ Axe
            float damage = melee != null ? melee.damage : 0f;
            float speed = melee != null ? Mathf.Max(0.1f, melee.swingSpeed) : 1f;
            float hitNorm = melee != null ? melee.hitTimeNormalized : 0.45f;

            // Reset bộ đếm gãy vũ khí khi player đổi sang vũ khí khác
            if (equipment.EquippedItem != _trackedWeapon)
            {
                _trackedWeapon = equipment.EquippedItem;
                _swingsRemaining = (melee != null && melee.maxSwingsBeforeBreak > 0) ? melee.maxSwingsBeforeBreak : -1;
            }

            float swingDuration = 1f / speed;     // thời lượng 1 nhát
            _cooldownTimer = swingDuration;       // cooldown = đúng 1 nhát
            IsSwinging = true;

            // Áp dụng FOV kick nhẹ khi bắt đầu vung rìu để tạo cảm giác lực bổ mạnh
            if (viewCamera != null)
            {
                viewCamera.fieldOfView = _baseFov + fovKick;
            }

            // Trừ tài nguyên ngay khi bắt đầu vung (bỏ qua stamina nếu vũ khí freeStamina — vd Chainsaw)
            if (stats != null)
            {
                if (melee == null || !melee.freeStamina) stats.ConsumeStamina(staminaPerSwing);
                stats.ApplyChopFatigue(energyPerSwing);
            }

            // Kích hoạt chuyển động và âm thanh vung gió
            PlaySwingAnim(speed);
            PlayRandom(swingSfx);

            // Phát tiếng ồn cho quái vật nghe thấy (đã qua bộ lọc giảm âm của Bùn)
            MakeHitNoise(1f);

            StartCoroutine(SwingRoutine(target, damage, swingDuration, hitNorm));
            return true;
        }

        private IEnumerator SwingRoutine(IChoppable target, float damage,
                                         float duration, float hitNorm)
        {
            float hitTime = duration * hitNorm;

            // Chờ tới thời điểm lưỡi rìu chạm
            yield return new WaitForSeconds(hitTime);

            // Áp damage nếu target vẫn hợp lệ
            if (target != null && target.CanBeChopped())
            {
                float finalDamage = damage;

                // Kiểm tra cửa sổ phản đòn x2 damage từ Perfect Block của PlayerBlock
                if (playerBlock != null && playerBlock.CounterActive)
                {
                    finalDamage *= playerBlock.CounterMultiplier;
                    playerBlock.ConsumeCounter(); // Chỉ áp dụng x2 cho duy nhất đòn đánh trúng đầu tiên
                }

                // Hứng giá trị trả về để biết cây đã đổ hay chưa
                bool felled = target.ApplyChop(finalDamage, transform);

                // Tính toán điểm va chạm thực tế để phát VFX/SFX
                Vector3 hitPoint = transform.position + transform.forward * 1.5f;
                Vector3 hitNormal = -transform.forward;

                if (_aimSource != null && Physics.Raycast(_aimSource.position, _aimSource.forward, out RaycastHit h, 4f))
                {
                    hitPoint = h.point;
                    hitNormal = h.normal;
                }

                PlayRandom(hitSfx);
                SpawnHitVfx(hitPoint, hitNormal);

                // Camera shake + screen feedback khi nhát chém hợp lệ
                if (cameraShaker != null)
                    cameraShaker.Shake(felled ? fellShake : chopShake);

                if (hitFeedback != null)
                {
                    if (felled) hitFeedback.FlashFell();
                    else hitFeedback.FlashChop();
                }

                // GIAI ĐOẠN 3: đếm đòn trúng cho vũ khí có thể gãy (Spear/Club/Guitar...)
                TickBreakCounter();

                // GIAI ĐOẠN 3b: Stun Baton vừa gây damage (ở trên) VỪA Stun (GDD đã xác nhận, khác Stun Gun chỉ Stun).
                if (_trackedWeapon is MeleeWeaponItemData meleeStun && meleeStun.appliesStunOnHit)
                    ApplyMeleeStun(target, meleeStun.meleeStunDuration);
            }

            // Chờ hết animation
            yield return new WaitForSeconds(Mathf.Max(0f, duration - hitTime));

            IsSwinging = false;
        }

        private void TickBreakCounter()
        {
            if (_swingsRemaining < 0) return; // vũ khí không giới hạn (Axe, Katana...)
            _swingsRemaining--;
            if (_swingsRemaining <= 0) BreakCurrentWeapon();
        }

        /// <summary>
        /// Vũ khí gãy sau maxSwingsBeforeBreak đòn trúng (GDD thật: Crafted Spear/Club/Guitar).
        /// Đơn giản hoá: chỉ tự Unequip, KHÔNG xoá khỏi Inventory — muốn "biến mất khỏi túi luôn" thì
        /// gọi thêm inventory.TryConsume(_trackedWeapon, 1) ở đây khi wiring vào dự án thật.
        /// </summary>
        private void BreakCurrentWeapon()
        {
            if (weaponBreakSfx != null) PlayRandom(new[] { weaponBreakSfx });
            Debug.Log($"[WeaponSwinger] {_trackedWeapon?.displayName} đã gãy sau nhiều lần sử dụng.");
            equipment.Unequip();
            _trackedWeapon = null;
            _swingsRemaining = -1;
        }

        /// <summary>Stun Baton: gọi CannibalAI.Stun()/MutantAI.Stun() sau khi ApplyChop đã trừ máu — target luôn là MonoBehaviour trong dự án này dù IChoppable không tự bắt buộc điều đó.</summary>
        private void ApplyMeleeStun(IChoppable target, float duration)
        {
            var mb = target as MonoBehaviour;
            if (mb == null) return;

            var cannibal = mb.GetComponent<TheForest.AI.CannibalAI>();
            if (cannibal != null) { cannibal.Stun(duration); return; }

            var mutant = mb.GetComponent<TheForest.AI.MutantAI>();
            if (mutant != null) mutant.Stun(duration);
        }

        private void PlaySwingAnim(float speed)
        {
            if (equipment == null) return;

            Animator activeAnimator = equipment.WeaponAnimator;
            Transform activeModel = equipment.WeaponModel;

            if (activeAnimator != null)
            {
                activeAnimator.SetFloat(SpeedHash, speed);
                activeAnimator.SetTrigger(SwingHash);
            }
            else if (activeModel != null)
            {
                StopCoroutine("TweenSwing");
                StartCoroutine("TweenSwing", 1f / speed);
            }
        }

        // Tween đơn giản: bổ rìu xuống rồi về vị trí cũ (khi chưa có anim clip)
        private IEnumerator TweenSwing(float duration)
        {
            if (equipment == null) yield break;
            Transform targetModel = equipment.WeaponModel;
            if (targetModel == null) yield break;

            Quaternion start = targetModel.localRotation;
            Quaternion down = start * Quaternion.Euler(tweenAngle, 0f, 0f);

            float half = duration * 0.5f;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                if (targetModel == null) yield break;
                targetModel.localRotation = Quaternion.Slerp(start, down, t / half);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                if (targetModel == null) yield break;
                targetModel.localRotation = Quaternion.Slerp(down, start, t / half);
                yield return null;
            }
            if (targetModel != null) targetModel.localRotation = start;
        }

        private void PlayRandom(AudioClip[] clips)
        {
            if (audioSource == null || clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        private void SpawnHitVfx(Vector3 pos, Vector3 normal)
        {
            if (hitVfxPrefab == null) return;
            var vfx = Instantiate(hitVfxPrefab, pos, Quaternion.LookRotation(normal));
            Destroy(vfx, 3f);
        }

        // Helper phát ra âm thanh có áp dụng giảm ồn từ Bùn
        private void MakeHitNoise(float loudness)
        {
            if (mudCamo != null)
            {
                mudCamo.EmitPlayerNoise(loudness);
            }
            else
            {
                TheForest.AI.NoiseSystem.EmitNoise(transform.position, loudness);
            }
        }
    }
}
