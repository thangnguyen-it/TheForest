using UnityEngine;

namespace TheForest.Interaction
{
    /// <summary>
    /// Vật có thể nhận damage từ một nhát vung/đòn đánh (cây, kẻ địch...).
    /// </summary>
    public interface IChoppable
    {
        bool CanBeChopped();

        /// <returns>true nếu nhát này khiến target bị hạ (cây đổ / địch chết).</returns>
        bool ApplyChop(float damage, Transform attacker);
    }
}