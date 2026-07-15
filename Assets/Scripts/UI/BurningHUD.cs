using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TheForest.Player;

namespace TheForest.UI
{
    /// <summary>
    /// Hiệu ứng cháy trên màn hình: overlay cam/đỏ nhấp nháy + (tùy chọn) particle lửa rìa màn.
    /// Nghe SurvivalStats.OnBurningChanged.
    /// </summary>
    public class BurningHUD : MonoBehaviour
    {
        [SerializeField] private SurvivalStats stats;
        [Tooltip("Image full-screen overlay (raycast OFF).")]
        [SerializeField] private Image fireOverlay;
        [SerializeField] private Color fireColor = new Color(1f, 0.35f, 0.05f, 0.25f);
        [SerializeField] private float pulseSpeed = 6f;
        [Tooltip("Particle lửa viền màn (tùy chọn).")]
        [SerializeField] private GameObject fireParticles;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip burningLoop;

        private Coroutine _pulse;

        private void Awake()
        {
            if (stats == null) stats = FindFirstObjectByType<SurvivalStats>();
            SetOverlayAlpha(0f);
            if (fireParticles != null) fireParticles.SetActive(false);
        }

        private void OnEnable()
        {
            if (stats != null) stats.OnBurningChanged += HandleBurning;
        }
        private void OnDisable()
        {
            if (stats != null) stats.OnBurningChanged -= HandleBurning;
        }

        private void HandleBurning(bool burning)
        {
            if (fireParticles != null) fireParticles.SetActive(burning);

            if (burning)
            {
                if (audioSource != null && burningLoop != null)
                { audioSource.clip = burningLoop; audioSource.loop = true; audioSource.Play(); }
                if (_pulse == null) _pulse = StartCoroutine(Pulse());
            }
            else
            {
                if (audioSource != null) audioSource.Stop();
                if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }
                SetOverlayAlpha(0f);
            }
        }

        private IEnumerator Pulse()
        {
            while (true)
            {
                float a = (Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f) * fireColor.a;
                SetOverlayAlpha(a);
                yield return null;
            }
        }

        private void SetOverlayAlpha(float a)
        {
            if (fireOverlay == null) return;
            var c = fireColor; c.a = a; fireOverlay.color = c;
        }
    }
}
