using Companion.Events;
using UnityEngine;

namespace TheForest.AI
{
    /// <summary>
    /// Cầu nối tiếng động. GIỮ API tĩnh EmitNoise(...) cũ để tương thích ngược,
    /// nhưng publish lên NoiseEventChannelSO thay vì gọi trực tiếp từng listener.
    /// Không còn giữ danh sách CannibalAI/AnimalAI (đã sửa bug lệch index cũ).
    ///
    /// Gán channel một lần lúc khởi động (VD trong một bootstrapper) qua SetChannel().
    /// </summary>
    public static class NoiseSystem
    {
        private static NoiseEventChannelSO _channel;

        public static void SetChannel(NoiseEventChannelSO channel) => _channel = channel;

        /// <summary>loudness: 1 = bình thường (chặt cây), &gt;1 to hơn, &lt;1 nhỏ (đi bộ).</summary>
        public static void EmitNoise(Vector3 position, float loudness = 1f, Transform source = null)
        {
            if (_channel == null) return; // chưa wire channel: bỏ qua an toàn
            _channel.Raise(new NoiseEvent(position, loudness, source));
        }
    }
}
