using UnityEngine;

namespace TheForest.Interaction
{
    /// <summary>
    /// Vật mẫu để test InteractionRaycaster. Gắn lên một Cube có Collider.
    /// Nhìn vào -> đổi màu (focus); nhấn E -> log + đổi màu. Xóa khi không cần.
    /// </summary>
    public class TestInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string promptText = "Nhấn E để dùng";
        private Renderer _renderer;
        private Color _baseColor;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer != null) _baseColor = _renderer.material.color;
        }

        public string GetPrompt() => promptText;
        public bool CanInteract() => true;

        public void Interact(GameObject interactor)
        {
            Debug.Log($"[Test] {name} được tương tác bởi {interactor.name}");
            if (_renderer != null)
                _renderer.material.color = Random.ColorHSV(); // đổi màu để thấy rõ
        }

        public void OnFocus()
        {
            if (_renderer != null) _renderer.material.color = Color.yellow; // highlight
        }

        public void OnLoseFocus()
        {
            if (_renderer != null) _renderer.material.color = _baseColor;
        }
    }
}
