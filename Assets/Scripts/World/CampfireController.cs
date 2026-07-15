using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TheForest.Interaction;
using TheForest.Items;
using TheForest.Multiplayer;
using TheForest.Player;
using TheForest.Persistence;

namespace TheForest.World
{
    public enum CampfireState { Unlit, Lit, Reinforced }

    /// <summary>
    /// Lửa trại theo GDD (Block 2):
    ///   Unlit  : chưa cháy. Equip Stick, nhìn xuống lửa, bấm E -> đặt 1 que (2 que tổng). Đủ que -> giữ E mồi lửa.
    ///   Lit    : đang cháy. Equip Large Rock, bấm E -> thêm đá quanh lửa (đủ rocksNeededForReinforce -> Reinforced).
    ///            Equip Firewood hoặc đang vác Log (PlayerLogCarry) -> bấm E để thêm nhiên liệu, kéo dài thời gian cháy.
    ///   Reinforced: đã gia cố đá, cháy bền hơn (bonus fuel một lần khi chuyển state).
    ///
    /// Nấu ăn: TryPlaceFood/TakeSlot/EatSlotDirectly - tối đa maxCookSlots miếng thịt/cá cùng lúc,
    /// chín sau cookSecondsToReady giây, cháy thành burnt sau burntAfterExtraSeconds giây kể từ lúc chín.
    /// Nếu lửa tắt giữa chừng khi đang nấu -> theo GDD thịt trả về trạng thái tươi (được xử lý trong Extinguish()).
    ///
    /// Dùng IHoldInteractable để "giữ E mồi lửa" hoạt động đúng nghĩa GIỮ PHÍM (xem InteractionRaycaster + IHoldInteractable).
    /// Tích hợp FireSource đã có sẵn trong dự án (cannibal sợ lửa khi đang cháy).
    /// </summary>
    [RequireComponent(typeof(FireSource))]
    [RequireComponent(typeof(NetworkObject))]
    public class CampfireController : NetworkBehaviour, IHoldInteractable, IPersistentStateParticipant
    {
        [Header("Nguyên liệu dựng lửa (2 que -> mồi bằng lighter có sẵn từ đầu game)")]
        [SerializeField] private ItemData stickItem;
        [SerializeField] private int sticksNeeded = 2;
        [SerializeField] private float igniteHoldSeconds = 1.2f;

        [Header("Gia cố (Reinforced)")]
        [SerializeField] private ItemData largeRockItem;
        [SerializeField] private int rocksNeededForReinforce = 8;

        [Header("Nhiên liệu bổ sung khi đang cháy")]
        [SerializeField] private ItemData firewoodItem;
        [SerializeField] private int maxLogsInBonfire = 6;

        [Header("Thời lượng cháy (giây)")]
        [Tooltip("Cháy cơ bản khi vừa mồi.")]
        [SerializeField] private float baseFuelSeconds = 300f;
        [Tooltip("Cộng thêm MỘT LẦN khi vừa đủ đá chuyển sang Reinforced.")]
        [SerializeField] private float reinforcedBonusSeconds = 600f;
        [Tooltip("Mỗi lần thêm firewood cộng bấy nhiêu giây (GDD: 2 firewood tăng đáng kể độ bền).")]
        [SerializeField] private float firewoodBonusSeconds = 240f;
        [Tooltip("Mỗi log nguyên thêm vào bonfire cộng bấy nhiêu giây.")]
        [SerializeField] private float logBonusSeconds = 420f;

        [Header("Nấu ăn")]
        [SerializeField] private int maxCookSlots = 6;
        [SerializeField] private float cookSecondsToReady = 10f;
        [SerializeField] private float burntAfterExtraSeconds = 20f;
        [Tooltip("Burnt meat giảm hydration bấy nhiêu điểm khi ăn (theo GDD, normal/hard).")]
        [SerializeField] private float burntMeatThirstPenalty = 10f;
        [SerializeField] private string itemResourcePath = "SotFData/Items";

        [Header("Visual")]
        [SerializeField] private GameObject stickStageVisual1;
        [SerializeField] private GameObject stickStageVisual2;
        [SerializeField] private GameObject litFlameVisual;
        [SerializeField] private GameObject reinforcedRingVisual;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip igniteSfx;
        [SerializeField] private AudioClip fireLoopSfx;

        // ===================== RUNTIME =====================
        public CampfireState State { get; private set; } = CampfireState.Unlit;
        public bool IsBurning => State != CampfireState.Unlit && _fuelRemaining > 0f;
        public float FuelSecondsRemaining => _fuelRemaining;

        private FireSource _fireSource;
        private int _sticksPlaced;
        private int _rocksPlaced;
        private int _logsAdded;
        private float _fuelRemaining;
        private float _igniteHoldTimer;
        private bool _isFocused;
        private ItemData[] _itemCache;
        private NetworkWorldObjectState _networkState;

        public event Action<CampfireState> OnStateChanged;
        public event Action OnFuelChanged;
        public event Action OnCookSlotsChanged;
        private string _persistenceId;
        public string PersistenceId => string.IsNullOrEmpty(_persistenceId)
            ? _persistenceId = PersistentStateId.For(this)
            : _persistenceId;

        [Serializable]
        private sealed class CampfirePersistenceState
        {
            public int state;
            public int sticksPlaced;
            public int rocksPlaced;
            public int logsAdded;
            public float fuelRemaining;
            public List<CookSlotPersistenceState> cookSlots = new List<CookSlotPersistenceState>();
        }

        [Serializable]
        private sealed class CookSlotPersistenceState
        {
            public string rawItemId;
            public float timer;
        }

        private class CookSlot
        {
            public ItemData rawItem;
            public ItemData cookedItem;
            public ItemData burntItem;
            public float timer;
            public bool IsEmpty => rawItem == null;
        }

        private List<CookSlot> _cookSlots;

        private void Awake()
        {
            _persistenceId = PersistentStateId.For(this);
            _networkState = GetComponent<NetworkWorldObjectState>();
            _fireSource = GetComponent<FireSource>();
            _fireSource.SetBurning(false); // đảm bảo bắt đầu Unlit dù prefab FireSource mặc định isBurning=true

            _cookSlots = new List<CookSlot>(maxCookSlots);
            for (int i = 0; i < maxCookSlots; i++) _cookSlots.Add(new CookSlot());

            ApplyVisual();
        }

        private void Update()
        {
            if (!NetworkWorldInteraction.ShouldSimulateHere(this)) return;
            if (State == CampfireState.Unlit) return;

            _fuelRemaining -= Time.deltaTime;
            if (_fuelRemaining <= 0f)
            {
                Extinguish();
                return;
            }

            TickCooking(Time.deltaTime);
        }

        // ===================== XÂY DỰNG (Unlit) =====================

        /// <summary>Đặt 1 que (equip Stick + bấm E). Đủ sticksNeeded -> chuyển sang chờ giữ E mồi lửa.</summary>
        public bool TryPlaceStick(Inventory inv)
        {
            if (State != CampfireState.Unlit || _sticksPlaced >= sticksNeeded) return false;
            if (inv == null || stickItem == null || !inv.TryConsume(stickItem, 1)) return false;

            _sticksPlaced++;
            ApplyVisual();
            PublishNetworkState();
            return true;
        }

        /// <summary>Mồi lửa: chỉ gọi khi đã tích đủ igniteHoldSeconds giữ phím (xem Interact()).</summary>
        public void Ignite()
        {
            if (State != CampfireState.Unlit || _sticksPlaced < sticksNeeded) return;

            State = CampfireState.Lit;
            _fuelRemaining = baseFuelSeconds;
            _igniteHoldTimer = 0f;

            _fireSource.SetBurning(true);
            PlaySfx(igniteSfx);
            if (audioSource != null && fireLoopSfx != null)
            {
                audioSource.clip = fireLoopSfx;
                audioSource.loop = true;
                audioSource.Play();
            }

            ApplyVisual();
            OnStateChanged?.Invoke(State);
            OnFuelChanged?.Invoke();
            PublishNetworkState();
        }

        // ===================== ĐANG CHÁY (Lit / Reinforced) =====================

        /// <summary>Thêm 1 đá lớn quanh lửa. Đủ số lượng -> Reinforced (cộng fuel 1 lần + bền hơn).</summary>
        public bool TryAddRock(Inventory inv)
        {
            if (State != CampfireState.Lit || _rocksPlaced >= rocksNeededForReinforce) return false;
            if (inv == null || largeRockItem == null || !inv.TryConsume(largeRockItem, 1)) return false;

            _rocksPlaced++;
            if (_rocksPlaced >= rocksNeededForReinforce)
            {
                State = CampfireState.Reinforced;
                _fuelRemaining += reinforcedBonusSeconds;
                OnStateChanged?.Invoke(State);
                OnFuelChanged?.Invoke();
            }
            ApplyVisual();
            PublishNetworkState();
            return true;
        }

        /// <summary>Thêm firewood (cắt từ log bằng rìu) để kéo dài thời gian cháy.</summary>
        public bool TryAddFirewood(Inventory inv)
        {
            if (State == CampfireState.Unlit) return false;
            if (inv == null || firewoodItem == null || !inv.TryConsume(firewoodItem, 1)) return false;

            _fuelRemaining += firewoodBonusSeconds * 0.5f; // GDD: 2 firewood ~ 1 đơn vị bonus đầy đủ
            OnFuelChanged?.Invoke();
            PublishNetworkState();
            return true;
        }

        /// <summary>Thêm 1 log nguyên đang vác vào lửa (bonfire), tối đa maxLogsInBonfire log.</summary>
        public bool TryAddLog(PlayerLogCarry carry)
        {
            if (State == CampfireState.Unlit) return false;
            if (carry == null || !carry.IsCarrying || _logsAdded >= maxLogsInBonfire) return false;

            var log = carry.ConsumeOne();
            if (log == null) return false;
            Destroy(log.gameObject);

            _logsAdded++;
            _fuelRemaining += logBonusSeconds;
            OnFuelChanged?.Invoke();
            PublishNetworkState();
            return true;
        }

        private void Extinguish()
        {
            State = CampfireState.Unlit;
            _sticksPlaced = 0;
            _rocksPlaced = 0;
            _logsAdded = 0;
            _fuelRemaining = 0f;

            // GDD: lửa tắt giữa chừng khi đang nấu -> thịt trả về trạng thái tươi ban đầu.
            foreach (var slot in _cookSlots)
            {
                slot.rawItem = null;
                slot.cookedItem = null;
                slot.burntItem = null;
                slot.timer = 0f;
            }
            OnCookSlotsChanged?.Invoke();

            _fireSource.SetBurning(false);
            if (audioSource != null) audioSource.Stop();
            ApplyVisual();
            OnStateChanged?.Invoke(State);
            OnFuelChanged?.Invoke();
            PublishNetworkState();
        }

        // ===================== NẤU ĂN =====================

        /// <summary>Đặt thịt/cá lên lửa (tối đa maxCookSlots miếng cùng lúc).</summary>
        public bool TryPlaceFood(ItemData raw, ItemData cooked, ItemData burnt)
        {
            if (!IsBurning || raw == null) return false;

            foreach (var slot in _cookSlots)
            {
                if (slot.IsEmpty)
                {
                    slot.rawItem = raw;
                    slot.cookedItem = cooked;
                    slot.burntItem = burnt;
                    slot.timer = 0f;
                    OnCookSlotsChanged?.Invoke();
                    PublishNetworkState();
                    return true;
                }
            }
            return false; // đủ 6 slot
        }

        public bool TryPlaceFood(ItemData raw)
        {
            if (NetworkWorldInteraction.ShouldRouteToServer(this))
            {
                TryPlaceFood(raw, GetCookedResult(raw), GetBurntResult(raw));
                PlaceFoodServerRpc(new FixedString64Bytes(NetworkWorldInteraction.ItemId(raw)));
                return true;
            }

            if (!CanCookItem(raw)) return false;
            return TryPlaceFood(raw, GetCookedResult(raw), GetBurntResult(raw));
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void PlaceFoodServerRpc(FixedString64Bytes rawItemId, RpcParams rpcParams = default)
        {
            if (!NetworkWorldInteraction.TryGetPlayerObject(rpcParams.Receive.SenderClientId, out GameObject playerObject)) return;
            Inventory inv = playerObject.GetComponent<Inventory>();
            ItemData raw = LoadItemById(rawItemId.ToString());
            if (inv != null && raw != null && EmptyCookSlotCount() > 0 && inv.TryConsume(raw, 1))
                TryPlaceFood(raw);
        }

        /// <summary>Tap E: lấy 1 slot về túi (raw/cooked/burnt tuỳ thời gian đã nấu).</summary>
        public ItemData TakeSlot(int index, out bool wasBurnt)
        {
            wasBurnt = false;
            if (index < 0 || index >= _cookSlots.Count) return null;

            var slot = _cookSlots[index];
            if (slot.IsEmpty) return null;

            ItemData result = ResolveSlotResult(slot, out wasBurnt);
            ClearSlot(slot);
            OnCookSlotsChanged?.Invoke();
            PublishNetworkState();
            return result;
        }

        /// <summary>Hold E theo GDD: ăn ngay 1 slot, hồi thẳng vào SurvivalStats (không qua túi).</summary>
        public bool EatSlotDirectly(int index, SurvivalStats stats)
        {
            if (stats == null || index < 0 || index >= _cookSlots.Count) return false;

            var slot = _cookSlots[index];
            if (slot.IsEmpty) return false;

            ItemData result = ResolveSlotResult(slot, out bool wasBurnt);
            ClearSlot(slot);
            OnCookSlotsChanged?.Invoke();
            PublishNetworkState();
            if (result == null) return false;

            stats.Eat(result.hungerRestore, result.energyRestore, result.healthRestore);
            if (wasBurnt) stats.Drink(-burntMeatThirstPenalty);
            return true;
        }

        private ItemData ResolveSlotResult(CookSlot slot, out bool wasBurnt)
        {
            wasBurnt = false;
            if (slot.timer >= cookSecondsToReady + burntAfterExtraSeconds)
            {
                wasBurnt = true;
                return slot.burntItem != null ? slot.burntItem : slot.rawItem;
            }
            if (slot.timer >= cookSecondsToReady)
                return slot.cookedItem != null ? slot.cookedItem : slot.rawItem;

            return slot.rawItem; // chưa chín -> trả lại sống
        }

        private void ClearSlot(CookSlot slot)
        {
            slot.rawItem = null;
            slot.cookedItem = null;
            slot.burntItem = null;
            slot.timer = 0f;
        }

        private void TickCooking(float dt)
        {
            foreach (var slot in _cookSlots)
            {
                if (slot.IsEmpty) continue;
                slot.timer += dt;
            }
        }

        public int EmptyCookSlotCount()
        {
            int c = 0;
            foreach (var s in _cookSlots) if (s.IsEmpty) c++;
            return c;
        }

        // ===================== IHoldInteractable =====================

        /// <summary>Chỉ muốn giữ-phím-liên-tục ở ĐÚNG lúc đang chờ mồi lửa (đủ que, chưa Lit).
        /// Ở mọi lúc khác (đặt từng que / đã cháy thêm đá-củi-log) vẫn là nhấn 1 lần bình thường.</summary>
        public bool HoldToInteract => State == CampfireState.Unlit && _sticksPlaced >= sticksNeeded;

        public string GetPrompt()
        {
            switch (State)
            {
                case CampfireState.Unlit:
                    return _sticksPlaced < sticksNeeded
                        ? $"[E] Đặt que ({_sticksPlaced}/{sticksNeeded})"
                        : "[Giữ E] Mồi lửa";
                case CampfireState.Lit:
                    return _rocksPlaced < rocksNeededForReinforce
                        ? $"[E] Lửa trại — thêm đá gia cố ({_rocksPlaced}/{rocksNeededForReinforce})"
                        : "[E] Lửa trại";
                default:
                    return "[E] Lửa trại (đã gia cố)";
            }
        }

        public bool CanInteract() => true;

        public void Interact(GameObject interactor)
        {
            if (NetworkWorldInteraction.ShouldRouteToServer(this))
            {
                var clientEquipment = interactor.GetComponent<EquipmentController>();
                var clientCarry = interactor.GetComponent<PlayerLogCarry>();
                ItemData equipped = clientEquipment != null ? clientEquipment.EquippedItem : null;
                bool wantsLog = clientCarry != null && clientCarry.IsCarrying;
                InteractLocal(interactor, equipped, wantsLog, Time.deltaTime);
                CampfireInteractServerRpc(
                    new FixedString64Bytes(NetworkWorldInteraction.ItemId(equipped)),
                    wantsLog,
                    Time.deltaTime);
                return;
            }

            InteractLocal(interactor, null, false, Time.deltaTime);
        }

        private void InteractLocal(GameObject interactor, ItemData equippedOverride, bool wantsLog, float holdDelta)
        {
            var inv = interactor.GetComponent<Inventory>();
            var equipment = interactor.GetComponent<EquipmentController>();
            var carry = interactor.GetComponent<PlayerLogCarry>();
            ItemData equippedItem = equippedOverride != null ? equippedOverride : equipment != null ? equipment.EquippedItem : null;
            bool wantsLogAction = wantsLog || (carry != null && carry.IsCarrying);

            if (State == CampfireState.Unlit)
            {
                if (_sticksPlaced < sticksNeeded)
                {
                    if (equippedItem == stickItem)
                        TryPlaceStick(inv);
                    return;
                }

                // Đủ que: Interact() được gọi LIÊN TỤC trong lúc giữ E (nhờ HoldToInteract == true ở state này).
                _igniteHoldTimer += Mathf.Max(0f, holdDelta);
                if (_igniteHoldTimer >= igniteHoldSeconds) Ignite();
                return;
            }

            // Đang cháy: ưu tiên xử lý theo item đang cầm trên tay.
            if (equippedItem == largeRockItem) { TryAddRock(inv); return; }
            if (equippedItem == firewoodItem) { TryAddFirewood(inv); return; }
            if (wantsLogAction) { TryAddLog(carry); return; }
            if (CanCookItem(equippedItem) && inv != null)
            {
                ItemData food = equippedItem;
                if (EmptyCookSlotCount() > 0 && inv.TryConsume(food, 1))
                    TryPlaceFood(food, GetCookedResult(food), GetBurntResult(food));
                return;
            }

            // Không cầm gì đặc biệt và không đang xây/gia cố -> để tầng UI (Fire Menu) tự gọi TryPlaceFood/TakeSlot.
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void CampfireInteractServerRpc(FixedString64Bytes equippedItemId, bool wantsLog, float holdDelta,
            RpcParams rpcParams = default)
        {
            if (!NetworkWorldInteraction.TryGetPlayerObject(rpcParams.Receive.SenderClientId, out GameObject playerObject)) return;
            InteractLocal(playerObject, LoadItemById(equippedItemId.ToString()), wantsLog, holdDelta);
            PublishNetworkState();
        }

        private bool CanCookItem(ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) return false;
            return item.itemId == "raw_meat" || item.itemId == "fish";
        }

        private ItemData GetCookedResult(ItemData raw)
        {
            if (raw == null) return null;
            switch (raw.itemId)
            {
                case "raw_meat": return LoadItemById("cooked_meat");
                case "fish": return LoadItemById("cooked_fish");
                default: return null;
            }
        }

        private ItemData GetBurntResult(ItemData raw)
        {
            if (raw == null) return null;
            switch (raw.itemId)
            {
                case "raw_meat": return LoadItemById("burnt_meat");
                case "fish": return LoadItemById("burnt_fish");
                default: return null;
            }
        }

        private ItemData LoadItemById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_itemCache == null) _itemCache = Resources.LoadAll<ItemData>(itemResourcePath);

            foreach (var item in _itemCache)
            {
                if (item != null && item.itemId == itemId)
                    return item;
            }

            return null;
        }

        public void OnFocus() { _isFocused = true; }

        public void OnLoseFocus()
        {
            _isFocused = false;
            // Nhả tầm nhìn giữa chừng khi đang mồi lửa -> giữ nguyên tiến trình (pause), không reset về 0,
            // vì Interact() chỉ dừng được gọi (do rời mục tiêu) chứ không có tín hiệu "nhả phím" riêng ở đây.
        }

        public string CapturePersistenceState()
        {
            var saved = new CampfirePersistenceState
            {
                state = (int)State,
                sticksPlaced = _sticksPlaced,
                rocksPlaced = _rocksPlaced,
                logsAdded = _logsAdded,
                fuelRemaining = _fuelRemaining
            };
            foreach (CookSlot slot in _cookSlots)
            {
                saved.cookSlots.Add(new CookSlotPersistenceState
                {
                    rawItemId = slot.rawItem != null ? slot.rawItem.itemId : string.Empty,
                    timer = slot.timer
                });
            }
            return JsonUtility.ToJson(saved);
        }

        public void RestorePersistenceState(string json)
        {
            CampfirePersistenceState saved = string.IsNullOrEmpty(json)
                ? null
                : JsonUtility.FromJson<CampfirePersistenceState>(json);
            if (saved == null) return;

            State = (CampfireState)Mathf.Clamp(saved.state, 0, (int)CampfireState.Reinforced);
            _sticksPlaced = Mathf.Clamp(saved.sticksPlaced, 0, sticksNeeded);
            _rocksPlaced = Mathf.Clamp(saved.rocksPlaced, 0, rocksNeededForReinforce);
            _logsAdded = Mathf.Clamp(saved.logsAdded, 0, maxLogsInBonfire);
            _fuelRemaining = Mathf.Max(0f, saved.fuelRemaining);
            if (_fuelRemaining <= 0f && State != CampfireState.Unlit) State = CampfireState.Unlit;

            if (_cookSlots == null)
            {
                _cookSlots = new List<CookSlot>(maxCookSlots);
                for (int i = 0; i < maxCookSlots; i++) _cookSlots.Add(new CookSlot());
            }
            for (int i = 0; i < _cookSlots.Count; i++)
            {
                CookSlotPersistenceState slotState = saved.cookSlots != null && i < saved.cookSlots.Count
                    ? saved.cookSlots[i]
                    : null;
                ItemData raw = slotState != null ? LoadItemById(slotState.rawItemId) : null;
                _cookSlots[i].rawItem = raw;
                _cookSlots[i].cookedItem = GetCookedResult(raw);
                _cookSlots[i].burntItem = GetBurntResult(raw);
                _cookSlots[i].timer = raw != null ? Mathf.Max(0f, slotState.timer) : 0f;
            }

            _fireSource.SetBurning(IsBurning);
            if (audioSource != null)
            {
                if (IsBurning && fireLoopSfx != null)
                {
                    audioSource.clip = fireLoopSfx;
                    audioSource.loop = true;
                    if (!audioSource.isPlaying) audioSource.Play();
                }
                else audioSource.Stop();
            }
            ApplyVisual();
            OnStateChanged?.Invoke(State);
            OnFuelChanged?.Invoke();
            OnCookSlotsChanged?.Invoke();
            PublishNetworkState();
        }

        private void ApplyVisual()
        {
            if (stickStageVisual1 != null) stickStageVisual1.SetActive(State == CampfireState.Unlit && _sticksPlaced >= 1);
            if (stickStageVisual2 != null) stickStageVisual2.SetActive(State == CampfireState.Unlit && _sticksPlaced >= 2);
            if (litFlameVisual != null) litFlameVisual.SetActive(State != CampfireState.Unlit);
            if (reinforcedRingVisual != null) reinforcedRingVisual.SetActive(State == CampfireState.Reinforced);
        }

        private void PlaySfx(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }

        private void PublishNetworkState()
        {
            if (IsServer) _networkState?.PublishNow();
        }
    }
}
