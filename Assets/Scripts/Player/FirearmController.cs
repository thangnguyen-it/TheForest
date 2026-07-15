using UnityEngine;
using UnityEngine.InputSystem;
using TheForest.Items;
using TheForest.Interaction;
using TheForest.AI;

namespace TheForest.Player
{
    /// <summary>
    /// Bắn súng: chỉ hoạt động khi đang cầm FirearmItemData. Khác Bow (giữ chuột kéo căng), súng bắn
    /// NGAY khi bấm, có băng đạn hữu hạn + reload. Dùng CHUNG cho Pistol/Revolver/Shotgun/Rifle/Stun Gun.
    ///
    /// FIX fidelity (#6): súng thường (KHÔNG stun) nay HITSCAN — mỗi viên là một Physics.Raycast tức thời
    /// tới hitscanRange, áp damage theo đúng thứ tự IChoppable -> IDamageable như Arrow/Bullet. Chỉ Stun
    /// Gun còn bắn projectile (đầu taser Bullet.cs, isStun=true).
    ///
    /// FIX fidelity (#8): số viên toả (Buckshot vs Slug) đọc từ ĐẠN đang nạp (AmmoItemData.pellets/
    /// spreadAngle), không cứng trên súng — cùng khẩu Shotgun nạp Buckshot hay Slug đều đúng.
    ///
    /// ĐƠN GIẢN HOÁ CÓ CHỦ ĐÍCH: Inventory (ItemData + int count) không lưu số đạn RIÊNG từng khẩu súng khi
    /// nhặt — nên súng vừa cầm lên coi như ĐẦY BĂNG (_ammoInMag = magazineSize); các lần reload SAU mới
    /// thực sự tiêu AmmoItemData từ Inventory.
    /// </summary>
    public class FirearmController : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private EquipmentController equipment;
        [SerializeField] private Inventory inventory;
        [SerializeField] private Transform shootOrigin; // camera/nòng súng

        [Header("Đạn (kéo TẤT CẢ AmmoItemData có trong game vào đây — controller tự lọc theo caliber khớp súng đang cầm)")]
        [SerializeField] private AmmoItemData[] allAmmoTypes;
        [SerializeField] private bool loadAmmoFromResources = true;
        [SerializeField] private string ammoResourcePath = "SotFData/Items";

        [Header("Hitscan")]
        [Tooltip("Layer đạn có thể trúng (địch + môi trường). Để trống = tất cả.")]
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Animator (tuỳ chọn)")]
        [SerializeField] private Animator weaponAnimatorOverride; // để trống sẽ tự đọc equipment.WeaponAnimator

        private static readonly int FireHash = Animator.StringToHash("Fire");
        private static readonly int ReloadHash = Animator.StringToHash("Reload");

        private FirearmItemData _trackedGun;
        private int _ammoInMag;
        private float _cooldownTimer;
        private float _reloadTimer;
        private bool _reloading;

        public bool HoldingFirearm => equipment != null && equipment.EquippedItem is FirearmItemData;
        public int AmmoInMag => _ammoInMag;
        public bool IsReloading => _reloading;

        private void Awake()
        {
            if (equipment == null) equipment = GetComponent<EquipmentController>();
            if (inventory == null) inventory = GetComponent<Inventory>();
            if (shootOrigin == null && Camera.main != null) shootOrigin = Camera.main.transform;
            LoadResourceAmmo();
        }

        private void LoadResourceAmmo()
        {
            if (!loadAmmoFromResources) return;

            var resourceAmmo = Resources.LoadAll<AmmoItemData>(ammoResourcePath);
            if (resourceAmmo == null || resourceAmmo.Length == 0) return;

            var merged = new System.Collections.Generic.List<AmmoItemData>();
            if (allAmmoTypes != null)
            {
                foreach (var ammo in allAmmoTypes)
                    if (ammo != null && !merged.Contains(ammo)) merged.Add(ammo);
            }

            foreach (var ammo in resourceAmmo)
                if (ammo != null && !merged.Contains(ammo)) merged.Add(ammo);

            allAmmoTypes = merged.ToArray();
        }

        private void Update()
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            var gun = equipment != null ? equipment.EquippedItem as FirearmItemData : null;
            if (gun != _trackedGun)
            {
                _trackedGun = gun;
                _ammoInMag = gun != null ? gun.magazineSize : 0; // xem ghi chú ĐƠN GIẢN HOÁ ở đầu file
                _reloading = false;
            }

            if (_reloading)
            {
                _reloadTimer -= Time.deltaTime;
                if (_reloadTimer <= 0f) FinishReload();
            }
        }

        public void OnAttack(InputValue value)
        {
            if (!HoldingFirearm || !value.isPressed) return;
            TryFire();
        }

        public void OnReload(InputValue value)
        {
            if (!value.isPressed || !HoldingFirearm || _reloading) return;
            TryStartReload();
        }

        private void TryFire()
        {
            if (_trackedGun == null || _reloading || _cooldownTimer > 0f) return;
            if (_ammoInMag <= 0) { TryStartReload(); return; }

            var ammo = FindAmmoFor(_trackedGun);
            if (ammo == null || shootOrigin == null) return;

            _cooldownTimer = _trackedGun.fireCooldown;
            _ammoInMag--;

            var animator = weaponAnimatorOverride != null ? weaponAnimatorOverride : equipment.WeaponAnimator;
            if (animator != null) animator.SetTrigger(FireHash);

            if (_trackedGun.isStunWeapon)
            {
                FireStunProjectile(ammo);
            }
            else
            {
                int pellets = Mathf.Max(1, ammo.pellets); // Buckshot=8, Slug/Pistol/Rifle=1
                for (int i = 0; i < pellets; i++) FireHitscanPellet(ammo);
            }

            // Súng thường to hơn hẳn cung tên (cannibal nghe được xa hơn); Stun Gun êm hơn nhiều.
            NoiseSystem.EmitNoise(transform.position, _trackedGun.isStunWeapon ? 0.6f : 1.6f);
        }

        // ===================== HITSCAN (súng thường) =====================
        private void FireHitscanPellet(AmmoItemData ammo)
        {
            float spread = ammo.pellets > 1 ? ammo.spreadAngle : 0f;
            Vector3 dir = shootOrigin.forward;
            if (spread > 0f)
            {
                float h = Random.Range(-spread, spread);
                float v = Random.Range(-spread, spread);
                dir = Quaternion.Euler(v, h, 0f) * dir;
            }

            if (!Physics.Raycast(shootOrigin.position, dir, out var hit, _trackedGun.hitscanRange, hitMask, QueryTriggerInteraction.Ignore))
                return;

            // Thứ tự áp damage GIỐNG HỆT Arrow.cs/Bullet.cs: IChoppable (Cannibal/Mutant/Virginia) trước,
            // IDamageable (Player/Virginia) sau.
            var choppable = hit.collider.GetComponentInParent<IChoppable>();
            if (choppable != null && choppable.CanBeChopped())
            {
                choppable.ApplyChop(ammo.damage, transform.root);
            }
            else
            {
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                    damageable.DealDamage(ammo.damage, dir, transform.root, false);
            }

            if (ammo.impactVfxPrefab != null)
                Instantiate(ammo.impactVfxPrefab, hit.point, Quaternion.LookRotation(hit.normal));
        }

        // ===================== PROJECTILE (chỉ Stun Gun) =====================
        private void FireStunProjectile(AmmoItemData ammo)
        {
            if (ammo.stunProjectilePrefab == null) return;
            Vector3 dir = shootOrigin.forward;
            var go = Instantiate(ammo.stunProjectilePrefab, shootOrigin.position + dir * 0.4f, Quaternion.LookRotation(dir));
            var bullet = go.GetComponent<Bullet>();
            if (bullet != null)
                bullet.Launch(dir * _trackedGun.stunProjectileSpeed, 0f, true, _trackedGun.stunDuration, transform.root);
        }

        private void TryStartReload()
        {
            if (_trackedGun == null || _ammoInMag >= _trackedGun.magazineSize) return;

            // Không tự nạp nếu trong túi không còn viên nào phù hợp (kể cả Stun Cartridge).
            var ammo = FindAmmoFor(_trackedGun);
            if (ammo == null || inventory == null || inventory.GetCount(ammo) <= 0) return;

            _reloading = true;
            _reloadTimer = _trackedGun.reloadSeconds;

            var animator = weaponAnimatorOverride != null ? weaponAnimatorOverride : equipment.WeaponAnimator;
            if (animator != null) animator.SetTrigger(ReloadHash);
        }

        private void FinishReload()
        {
            _reloading = false;
            if (_trackedGun == null) return;

            var ammo = FindAmmoFor(_trackedGun);
            if (ammo == null || inventory == null) return;

            int need = _trackedGun.magazineSize - _ammoInMag;
            int have = inventory.GetCount(ammo);
            int toLoad = Mathf.Min(need, have);

            if (toLoad > 0 && inventory.TryConsume(ammo, toLoad))
                _ammoInMag += toLoad;
        }

        private AmmoItemData FindAmmoFor(FirearmItemData gun)
        {
            if (gun == null || allAmmoTypes == null) return null;
            foreach (var a in allAmmoTypes)
                if (a != null && a.caliber == gun.caliber) return a;
            return null;
        }
    }
}
