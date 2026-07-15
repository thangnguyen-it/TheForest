using System;
using Unity.Netcode;
using UnityEngine;
using TheForest.Interaction;
using TheForest.Multiplayer;
using TheForest.Player;
using TheForest.Persistence;

namespace TheForest.World
{
    public enum CookingPotState
    {
        Empty,
        DirtyWater,
        CleanWater
    }

    /// <summary>
    /// World cooking pot slice: collect water, boil dirty water near a lit fire, drink clean water in servings.
    /// Advanced stew recipes can be layered on top of this water-state machine later.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(NetworkObject))]
    public class CookingPotController : NetworkBehaviour, IInteractable, IPersistentStateParticipant
    {
        [Header("Water")]
        [SerializeField] private CookingPotState state = CookingPotState.Empty;
        [SerializeField, Range(0f, 1f)] private float fill = 0f;
        [SerializeField] private float servingSize = 0.25f;
        [SerializeField] private float thirstRestorePerServing = 35f;
        [SerializeField] private float dirtyWaterDamage = 4f;

        [Header("Boiling")]
        [SerializeField] private float boilSeconds = 18f;
        [SerializeField] private float fireSearchRadius = 2.5f;
        [SerializeField] private float waterSearchRadius = 2.2f;
        [SerializeField] private bool rainFillsCleanWater = true;

        [Header("Visual")]
        [SerializeField] private GameObject waterVisual;
        [SerializeField] private GameObject boilingVisual;

        public CookingPotState State => state;
        public float Fill => fill;
        public bool HasWater => state != CookingPotState.Empty && fill > 0f;
        public bool IsBoiling { get; private set; }

        public event Action OnPotChanged;
        private string _persistenceId;
        private NetworkWorldObjectState _networkState;
        public string PersistenceId => string.IsNullOrEmpty(_persistenceId)
            ? _persistenceId = PersistentStateId.For(this)
            : _persistenceId;

        [Serializable]
        private sealed class PotPersistenceState
        {
            public int state;
            public float fill;
            public float boilTimer;
        }

        private float _boilTimer;

        private void Awake()
        {
            _persistenceId = PersistentStateId.For(this);
            _networkState = GetComponent<NetworkWorldObjectState>();
            ApplyVisual();
        }

        private void Update()
        {
            if (!NetworkWorldInteraction.ShouldSimulateHere(this)) return;
            TickBoiling(Time.deltaTime);
        }

        public string GetPrompt()
        {
            if (state == CookingPotState.Empty)
            {
                if (CanFillFromNearbyWater()) return "[E] Fill cooking pot";
                if (CanFillFromRain()) return "[E] Collect rainwater";
                return "Cooking pot (empty)";
            }

            if (state == CookingPotState.DirtyWater)
                return IsNearLitFire() ? "[E] Drink dirty water / boiling..." : "[E] Drink dirty water";

            return $"[E] Drink clean water ({Mathf.CeilToInt(fill / servingSize)} servings)";
        }

        public bool CanInteract()
        {
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (NetworkWorldInteraction.ShouldRouteToServer(this))
            {
                InteractLocal(interactor);
                InteractServerRpc();
                return;
            }

            InteractLocal(interactor);
        }

        private void InteractLocal(GameObject interactor)
        {
            if (state == CookingPotState.Empty)
            {
                if (TryFillFromNearbyWater()) return;
                TryFillFromRain();
                return;
            }

            var stats = interactor.GetComponent<SurvivalStats>();
            if (stats == null) return;

            DrinkServingLocal(stats);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void InteractServerRpc(RpcParams rpcParams = default)
        {
            if (!NetworkWorldInteraction.TryGetPlayerObject(rpcParams.Receive.SenderClientId, out GameObject playerObject)) return;
            InteractLocal(playerObject);
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }

        public bool TryFill(bool cleanWater)
        {
            if (NetworkWorldInteraction.ShouldRouteToServer(this))
            {
                TryFillLocal(cleanWater);
                TryFillServerRpc(cleanWater);
                return true;
            }

            return TryFillLocal(cleanWater);
        }

        private bool TryFillLocal(bool cleanWater)
        {
            if (state != CookingPotState.Empty) return false;

            state = cleanWater ? CookingPotState.CleanWater : CookingPotState.DirtyWater;
            fill = 1f;
            _boilTimer = 0f;
            ApplyVisual();
            OnPotChanged?.Invoke();
            PublishNetworkState();
            return true;
        }

        public bool DrinkServing(SurvivalStats stats)
        {
            if (NetworkWorldInteraction.ShouldRouteToServer(this))
            {
                DrinkServingLocal(stats);
                DrinkServingServerRpc();
                return true;
            }

            return DrinkServingLocal(stats);
        }

        private bool DrinkServingLocal(SurvivalStats stats)
        {
            if (stats == null || !HasWater) return false;

            stats.Drink(thirstRestorePerServing);
            if (state == CookingPotState.DirtyWater && dirtyWaterDamage > 0f)
                stats.ApplyDamage(dirtyWaterDamage);

            fill = Mathf.Max(0f, fill - servingSize);
            if (fill <= 0f)
            {
                state = CookingPotState.Empty;
                _boilTimer = 0f;
            }

            ApplyVisual();
            OnPotChanged?.Invoke();
            PublishNetworkState();
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void TryFillServerRpc(bool cleanWater)
        {
            TryFillLocal(cleanWater);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void DrinkServingServerRpc(RpcParams rpcParams = default)
        {
            if (!NetworkWorldInteraction.TryGetPlayerObject(rpcParams.Receive.SenderClientId, out GameObject playerObject)) return;
            DrinkServingLocal(playerObject.GetComponent<SurvivalStats>());
        }

        private void TickBoiling(float dt)
        {
            bool boilingNow = state == CookingPotState.DirtyWater && fill > 0f && IsNearLitFire();
            IsBoiling = boilingNow;

            if (!boilingNow)
            {
                if (boilingVisual != null) boilingVisual.SetActive(false);
                return;
            }

            _boilTimer += dt;
            if (boilingVisual != null) boilingVisual.SetActive(true);

            if (_boilTimer >= boilSeconds)
            {
                state = CookingPotState.CleanWater;
                _boilTimer = 0f;
                ApplyVisual();
                OnPotChanged?.Invoke();
                PublishNetworkState();
            }
        }

        private bool TryFillFromNearbyWater()
        {
            var source = FindNearbyWaterSource();
            if (source == null || !source.TryCollectWater(out bool cleanWater)) return false;
            return TryFillLocal(cleanWater);
        }

        private bool TryFillFromRain()
        {
            if (!CanFillFromRain()) return false;
            return TryFillLocal(true);
        }

        private bool CanFillFromNearbyWater()
        {
            var source = FindNearbyWaterSource();
            return source != null && source.IsAvailable;
        }

        private bool CanFillFromRain()
        {
            return rainFillsCleanWater &&
                   WeatherSystem.Instance != null &&
                   WeatherSystem.Instance.IsRaining &&
                   state == CookingPotState.Empty;
        }

        private NaturalWaterSource FindNearbyWaterSource()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, waterSearchRadius, ~0, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                var source = hit.GetComponentInParent<NaturalWaterSource>();
                if (source != null && source.IsAvailable)
                    return source;
            }

            return null;
        }

        private bool IsNearLitFire()
        {
            var fires = FireRegistry.Fires;
            foreach (var fire in fires)
            {
                if (fire == null || !fire.IsBurning) continue;
                if (Vector3.Distance(transform.position, fire.Position) <= fireSearchRadius)
                    return true;
            }

            return false;
        }

        private void ApplyVisual()
        {
            if (waterVisual != null)
            {
                waterVisual.SetActive(HasWater);
                var s = waterVisual.transform.localScale;
                waterVisual.transform.localScale = new Vector3(s.x, Mathf.Max(0.02f, fill), s.z);
            }

            if (boilingVisual != null) boilingVisual.SetActive(IsBoiling);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.4f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, waterSearchRadius);
            Gizmos.color = new Color(1f, 0.35f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, fireSearchRadius);
        }

        public string CapturePersistenceState()
        {
            return JsonUtility.ToJson(new PotPersistenceState
            {
                state = (int)state,
                fill = fill,
                boilTimer = _boilTimer
            });
        }

        public void RestorePersistenceState(string json)
        {
            PotPersistenceState saved = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PotPersistenceState>(json);
            if (saved == null) return;
            state = (CookingPotState)Mathf.Clamp(saved.state, 0, (int)CookingPotState.CleanWater);
            fill = Mathf.Clamp01(saved.fill);
            if (fill <= 0f) state = CookingPotState.Empty;
            _boilTimer = Mathf.Clamp(saved.boilTimer, 0f, boilSeconds);
            IsBoiling = false;
            ApplyVisual();
            OnPotChanged?.Invoke();
            PublishNetworkState();
        }

        private void PublishNetworkState()
        {
            if (IsServer) _networkState?.PublishNow();
        }
    }
}
