using UnityEngine;

namespace TheForest.World
{
    /// <summary>Danh sách các DifficultySettings để menu liệt kê & chọn.</summary>
    [CreateAssetMenu(fileName = "DifficultyDatabase", menuName = "The Forest/Difficulty Database")]
    public class DifficultyDatabase : ScriptableObject
    {
        public DifficultySettings[] difficulties;

        public DifficultySettings GetByMode(DifficultyMode mode)
        {
            foreach (var d in difficulties)
                if (d != null && d.mode == mode) return d;
            return difficulties != null && difficulties.Length > 0 ? difficulties[0] : null;
        }
    }
}
