using UnityEngine;

namespace Companion.Events
{
    /// <summary>
    /// Sự kiện tiếng động phát ra trong thế giới. Người nghe tự quyết có nghe được không
    /// (dựa hearingRadius riêng), không phải nguồn phát quyết định.
    /// </summary>
    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Loudness;   // 1 = chặt cây; >1 to hơn; <1 nhỏ
        public readonly Transform Source; // có thể null

        public NoiseEvent(Vector3 position, float loudness, Transform source = null)
        {
            Position = position;
            Loudness = loudness;
            Source = source;
        }
    }

    // Tái dùng EventChannelSO<T> có sẵn trong BaseEventChannelSO.cs
    [CreateAssetMenu(menuName = "Companion/Events/Noise", fileName = "Ch_Noise")]
    public class NoiseEventChannelSO : EventChannelSO<NoiseEvent> { }
}
