using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TheForest.UI
{
    /// <summary>
    /// Chớp màn hình ngắn khi có phản hồi (chặt trúng, nhận damage).
    /// Gắn lên một Image full-screen (raycastTarget = OFF) trong Canvas HUD.
    /// </summary>
    public class HitFeedbackUI : MonoBehaviour
    {
        [SerializeField] private Image flashImage;
        [SerializeField] private Color chopColor = new Color(1f, 1f, 1f, 0.10f);
        [SerializeField] private Color fellColor = new Color(0.8f, 0.5f, 0.2f, 0.22f);
        [SerializeField] private float fadeSpeed = 6f;

        private Coroutine _routine;

        private void Awake()
        {
            if (flashImage == null) flashImage = GetComponent<Image>();
            if (flashImage != null) SetAlpha(0f);
        }

        public void FlashChop() => Flash(chopColor);
        public void FlashFell() => Flash(fellColor);

        public void Flash(Color color)
        {
            if (flashImage == null) return;
            flashImage.color = color;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            Color c = flashImage.color;
            while (c.a > 0.001f)
            {
                c.a = Mathf.MoveTowards(c.a, 0f, fadeSpeed * Time.deltaTime);
                flashImage.color = c;
                yield return null;
            }
            SetAlpha(0f);
        }

        private void SetAlpha(float a)
        {
            var c = flashImage.color; c.a = a; flashImage.color = c;
        }
    }
}
