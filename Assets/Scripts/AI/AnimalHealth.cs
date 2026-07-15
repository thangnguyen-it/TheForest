using System;
using UnityEngine;
using TheForest.Interaction;
using TheForest.Items;
using TheForest.Player;

namespace TheForest.AI
{
    /// <summary>
    /// Máu động vật + nhận đòn (IChoppable: dùng lại hệ rìu/vũ khí, giáo ném).
    /// Chết -> rơi loot (thịt/da/đầu) + spawn corpse Animal (hút Starving/Skinny).
    /// Nhiều con chết 1 đòn (Rabbit/Fish/Lizard) -> đặt maxHealth thấp.
    /// </summary>
    [RequireComponent(typeof(AnimalAI))]
    public class AnimalHealth : MonoBehaviour, IChoppable
    {
        [Header("Máu")]
        [SerializeField] private float maxHealth = 20f;
        [SerializeField] private float health = 20f;

        [Header("Loot")]
        [SerializeField] private bool autoConfigureLootFromKind = true;
        [SerializeField] private string itemResourcePath = "SotFData/Items";
        [SerializeField] private ItemData meatItem;   // fresh meat
        [SerializeField] private int meatAmount = 1;
        [SerializeField] private ItemData hideItem;   // da (skin)
        [SerializeField] private int hideAmount = 0;
        [SerializeField] private ItemData headItem;   // đầu (trophy) - tùy chọn
        [SerializeField] private GameObject lootPickupPrefab;

        [Header("Xác")]
        [SerializeField] private GameObject animalCorpsePrefab; // CannibalCorpse Kind=Animal

        private AnimalAI _ai;
        public bool IsDead { get; private set; }
        public event Action OnDeath;

        private void Awake()
        {
            _ai = GetComponent<AnimalAI>();
            ConfigureLootFromKind();
            health = maxHealth;
        }

        // ===================== IChoppable =====================
        public bool CanBeChopped() => !IsDead;

        public bool ApplyChop(float damage, Transform attacker)
        {
            if (IsDead) return false;
            health = Mathf.Max(0f, health - damage);

            // Bị đánh -> hoảng chạy (trừ Boar có thể phản công)
            _ai.OnHurt(attacker);

            if (health <= 0f) { Die(attacker); return true; }
            return false;
        }

        private void Die(Transform killer)
        {
            IsDead = true;
            OnDeath?.Invoke();
            _ai.OnDeath();

            DropLoot();

            if (animalCorpsePrefab != null)
                Instantiate(animalCorpsePrefab, transform.position, transform.rotation);

            Destroy(gameObject, 0.2f);
        }

        private void DropLoot()
        {
            SpawnLoot(meatItem, meatAmount);
            SpawnLoot(hideItem, hideAmount);
            SpawnLoot(headItem, 1);
        }

        private void SpawnLoot(ItemData item, int amount)
        {
            if (item == null || amount <= 0 || lootPickupPrefab == null) return;
            Vector3 pos = transform.position + Vector3.up * 0.3f
                + new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), 0f, UnityEngine.Random.Range(-0.6f, 0.6f));
            var go = Instantiate(lootPickupPrefab, pos, Quaternion.identity);
            var pickup = go.GetComponent<ItemPickup>();
            if (pickup != null) pickup.Configure(item, amount);
        }

        // Cho bẫy giết trực tiếp
        public void KillByTrap()
        {
            if (IsDead) return;
            health = 0f;
            Die(null);
        }

        private void ConfigureLootFromKind()
        {
            if (!autoConfigureLootFromKind || _ai == null) return;

            switch (_ai.Kind)
            {
                case AnimalKind.Deer:
                    SetLoot("raw_meat", 2, "animal_hide", 1);
                    break;
                case AnimalKind.Moose:
                    SetLoot("raw_meat", 4, "animal_hide", 2);
                    break;
                case AnimalKind.Rabbit:
                case AnimalKind.Squirrel:
                case AnimalKind.Skunk:
                case AnimalKind.Raccoon:
                    SetLoot("raw_meat", 1, null, 0);
                    break;
                case AnimalKind.Duck:
                case AnimalKind.Seagull:
                case AnimalKind.Bird:
                case AnimalKind.BlueBird:
                    SetLoot("raw_meat", 1, "feather", 2);
                    break;
                case AnimalKind.Salmon:
                    SetLoot("fish", 1, null, 0);
                    break;
                case AnimalKind.SeaTurtle:
                    SetLoot("raw_meat", 1, "turtle_shell", 1);
                    break;
                case AnimalKind.FreshwaterTurtle:
                case AnimalKind.Turtle:
                    SetLoot("raw_meat", 1, null, 0);
                    break;
                case AnimalKind.Shark:
                case AnimalKind.Orca:
                    SetLoot("raw_meat", 4, null, 0);
                    break;
                default:
                    break;
            }
        }

        private void SetLoot(string meatId, int meatCount, string hideId, int hideCount)
        {
            if (meatItem == null) meatItem = LoadItem(meatId);
            if (meatAmount <= 0) meatAmount = meatCount;
            else meatAmount = Mathf.Max(meatAmount, meatCount);

            if (hideItem == null) hideItem = LoadItem(hideId);
            if (hideAmount <= 0) hideAmount = hideCount;
            else hideAmount = Mathf.Max(hideAmount, hideCount);
        }

        private ItemData LoadItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var items = Resources.LoadAll<ItemData>(itemResourcePath);
            foreach (var item in items)
            {
                if (item != null && item.itemId == id)
                    return item;
            }
            return null;
        }
    }
}
