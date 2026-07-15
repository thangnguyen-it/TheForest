using UnityEngine;

namespace TheForest.Player
{
    /// <summary>
    /// Rung camera bằng noise. Đặt component lên một transform TRUNG GIAN
    /// (Player > LookPivot > [ShakePivot] > Camera). PlayerLook xoay LookPivot;
    /// CameraShaker chỉ thêm offset cục bộ lên ShakePivot -> không xung đột.
    /// </summary>
    public class CameraShaker : MonoBehaviour
    {
        [Header("Cấu hình rung")]
        [Tooltip("Biên độ dịch vị trí tối đa (m).")]
        [SerializeField] private float maxPositionAmplitude = 0.06f;
        [Tooltip("Biên độ xoay tối đa (độ).")]
        [SerializeField] private float maxRotationAmplitude = 1.5f;
        [Tooltip("Tốc độ dao động noise.")]
        [SerializeField] private float frequency = 22f;
        [Tooltip("Tốc độ tắt dần của trauma (1 = tắt trong ~1s).")]
        [SerializeField] private float decayPerSec = 2.5f;
        [Tooltip("Mũ phi tuyến: trauma^exp. 2 = rung mạnh chỉ khi trauma cao.")]
        [SerializeField] private float traumaExponent = 2f;

        private float _trauma;          // 0..1
        private Vector3 _baseLocalPos;
        private float _seedX, _seedY, _seedRot;

        private void Awake()
        {
            _baseLocalPos = transform.localPosition;
            _seedX = Random.value * 100f;
            _seedY = Random.value * 100f;
            _seedRot = Random.value * 100f;
        }

        /// <summary>Thêm rung. amount 0..1 (cộng dồn, kẹp ở 1).</summary>
        public void Shake(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        private void LateUpdate()
        {
            // LateUpdate: chạy SAU PlayerLook để cộng offset lên trên góc nhìn cuối
            if (_trauma <= 0f)
            {
                transform.localPosition = _baseLocalPos;
                transform.localRotation = Quaternion.identity;
                return;
            }

            float shake = Mathf.Pow(_trauma, traumaExponent);
            float t = Time.time * frequency;

            float ox = (Mathf.PerlinNoise(_seedX, t) * 2f - 1f) * maxPositionAmplitude * shake;
            float oy = (Mathf.PerlinNoise(_seedY, t) * 2f - 1f) * maxPositionAmplitude * shake;
            float rz = (Mathf.PerlinNoise(_seedRot, t) * 2f - 1f) * maxRotationAmplitude * shake;

            transform.localPosition = _baseLocalPos + new Vector3(ox, oy, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, rz);

            _trauma = Mathf.Max(0f, _trauma - decayPerSec * Time.deltaTime);
        }
    }
}
