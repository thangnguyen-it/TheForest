using UnityEngine;
using UnityEngine.UI;

namespace TheForest.UI
{
    /// <summary>
    /// Một thanh chỉ số tái sử dụng. Gắn lên 1 object UI có Image kiểu Filled.
    /// HUDManager gọi SetValue(current, max) mỗi khi chỉ số thay đổi.
    /// </summary>
    public class StatBar : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private Image fillImage;        // Image: Image Type = Filled, Fill Method = Horizontal
        [SerializeField] private CanvasGroup canvasGroup; // tùy chọn: để mờ/ẩn

        [Header("Đổi màu theo mức (tùy chọn)")]
        [SerializeField] private bool useColorGradient = false;
        [SerializeField] private Color fullColor = Color.green;
        [SerializeField] private Color lowColor = Color.red;
        [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.3f;

        [Header("Mượt hóa")]
        [SerializeField] private float lerpSpeed = 8f;

        private float _target = 1f;

        public void SetValue(float current, float max)
        {
            _target = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        private void Update()
        {
            if (fillImage == null) return;

            // Lerp cho mượt thay vì nhảy giật
            fillImage.fillAmount = Mathf.MoveTowards(
                fillImage.fillAmount, _target, lerpSpeed * Time.deltaTime);

            if (useColorGradient)
            {
                float t = Mathf.InverseLerp(0f, lowThreshold, fillImage.fillAmount);
                fillImage.color = Color.Lerp(lowColor, fullColor, t);
            }
        }

        public void SetVisible(bool visible)
        {
            if (canvasGroup != null) canvasGroup.alpha = visible ? 1f : 0f;
        }
    }
}
