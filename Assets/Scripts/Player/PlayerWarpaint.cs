using System;
using UnityEngine;

namespace TheForest.Player
{
    /// <summary>
    /// Sơn đỏ trên người player — CHỈ CÒN HIỆU ỨNG HÌNH ẢNH (cosmetic).
    ///
    /// FIX fidelity (#2): cơ chế "sơn đỏ khiến cannibal quỳ lạy/bỏ qua" là của The Forest (2014), KHÔNG
    /// có trong Sons of the Forest — đã GỠ khỏi CannibalAI. Component này giữ lại chỉ để đổi màu vật liệu
    /// (bôi sơn/rửa trôi), KHÔNG còn tác động tới AI. IsPainted/PaintStrength vẫn public cho UI nếu cần.
    /// </summary>
    public class PlayerWarpaint : MonoBehaviour
    {
        [Header("Sơn")]
        [Tooltip("Độ mạnh sơn 0..1. 1 = cannibal gần như bỏ qua hoàn toàn.")]
        [Range(0f, 1f)][SerializeField] private float paintStrength = 0f;
        [Tooltip("Có tự phai theo thời gian không (GDD chủ yếu phai khi xuống nước).")]
        [SerializeField] private bool fadeOverTime = false;
        [SerializeField] private float fadePerSec = 0.01f;

        [Header("Tham chiếu (để biết đang ở dưới nước)")]
        [SerializeField] private SurvivalStats stats; // dùng IsWet như cờ dưới nước tạm

        [Header("Hiển thị (tùy chọn)")]
        [SerializeField] private Renderer[] paintRenderers; // tay/người để đổi material đỏ
        [SerializeField] private string paintColorProperty = "_BaseColor";
        [SerializeField] private Color paintColor = new Color(0.6f, 0.05f, 0.05f);

        public bool IsPainted => paintStrength > 0.05f;
        public float PaintStrength => paintStrength;

        public event Action<float> OnPaintChanged;

        private void Awake()
        {
            if (stats == null) stats = GetComponent<SurvivalStats>();
            ApplyVisual();
        }

        private void Update()
        {
            // Rửa trôi khi xuống nước (GDD)
            if (stats != null && stats.IsWet && paintStrength > 0f)
            {
                SetPaint(paintStrength - fadePerSec * 4f * Time.deltaTime); // phai nhanh khi ướt
            }
            else if (fadeOverTime && paintStrength > 0f)
            {
                SetPaint(paintStrength - fadePerSec * Time.deltaTime);
            }
        }

        /// <summary>Bôi sơn (gọi từ item sơn đỏ). amount cộng dồn, kẹp 0..1.</summary>
        public void ApplyPaint(float amount)
        {
            SetPaint(paintStrength + amount);
        }

        public void WashOff() => SetPaint(0f);

        private void SetPaint(float value)
        {
            float v = Mathf.Clamp01(value);
            if (Mathf.Approximately(v, paintStrength)) return;
            paintStrength = v;
            ApplyVisual();
            OnPaintChanged?.Invoke(paintStrength);
        }

        private void ApplyVisual()
        {
            if (paintRenderers == null) return;
            foreach (var r in paintRenderers)
            {
                if (r == null) continue;
                var mat = r.material; // instance
                if (mat.HasProperty(paintColorProperty))
                {
                    Color baseCol = mat.GetColor(paintColorProperty);
                    mat.SetColor(paintColorProperty, Color.Lerp(baseCol, paintColor, paintStrength));
                }
            }
        }
    }
}
