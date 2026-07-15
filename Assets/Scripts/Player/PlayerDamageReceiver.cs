using UnityEngine;
using TheForest.Interaction;

namespace TheForest.Player
{
    /// <summary>
    /// Cổng nhận damage của player. Thứ tự xử lý: Block (né/parry CHỦ ĐỘNG) -> Armor (giáp CHARGE hấp thụ,
    /// Giai đoạn 3) -> SurvivalStats (trừ máu thật). AI gọi DealDamage (IDamageable).
    /// </summary>
    [RequireComponent(typeof(SurvivalStats))]
    public class PlayerDamageReceiver : MonoBehaviour, IDamageable
    {
        [SerializeField] private SurvivalStats stats;
        [SerializeField] private PlayerBlock block;
        [SerializeField] private PlayerArmor armor;
        [SerializeField] private TheForest.UI.HitFeedbackUI hitFeedback; // flash đỏ (tùy chọn)

        private void Awake()
        {
            if (stats == null) stats = GetComponent<SurvivalStats>();
            if (block == null) block = GetComponent<PlayerBlock>();
            if (armor == null) armor = GetComponent<PlayerArmor>();
        }

        public void DealDamage(float amount, Vector3 hitDirection,
                               Transform attacker, bool isCreepyMutant)
        {
            float finalDamage = amount;
            if (block != null)
                finalDamage = block.ProcessIncoming(amount, hitDirection, attacker, isCreepyMutant);

            // GIAI ĐOẠN 3 (fidelity SotF): giáp CHARGE hấp thụ theo đòn, áp SAU block. Mỗi mảnh soak bớt
            // sát thương rồi tụt độ bền; mảnh cạn bền sẽ gãy trong ProcessIncoming. Golden/Ancient (bất
            // hoại) giảm % cố định bên trong đó.
            if (armor != null && finalDamage > 0f)
                finalDamage = armor.ProcessIncoming(finalDamage);

            if (finalDamage > 0f)
            {
                stats.ApplyDamage(finalDamage);
                if (hitFeedback != null) hitFeedback.Flash(new Color(0.7f, 0f, 0f, 0.3f)); // máu đỏ
            }
        }
    }
}
