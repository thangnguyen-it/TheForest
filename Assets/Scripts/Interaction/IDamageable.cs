using UnityEngine;

namespace TheForest.Interaction
{
    /// <summary>
    /// Nhận sát thương có ngữ cảnh (hướng, kẻ tấn công) để xử lý block/né.
    /// Player cài interface này; AI gọi DealDamage thay vì trừ máu trực tiếp.
    /// </summary>
    public interface IDamageable
    {
        /// <param name="amount">Sát thương gốc.</param>
        /// <param name="hitDirection">Hướng đòn tới (từ attacker tới nạn nhân).</param>
        /// <param name="attacker">Kẻ tấn công (có thể null).</param>
        /// <param name="isCreepyMutant">Đòn từ Creepy Mutant (perfect block không đẩy lùi).</param>
        void DealDamage(float amount, Vector3 hitDirection, Transform attacker, bool isCreepyMutant);
    }
}
