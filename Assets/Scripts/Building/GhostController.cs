using UnityEngine;
using TheForest.Building.Config;

namespace TheForest.Building.Systems
{
    public class GhostController : MonoBehaviour
    {
        [SerializeField] private IndicatorConfig config;
        [SerializeField] private Material ghostValid;
        [SerializeField] private Material ghostInvalid;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private PlacementConfig placementConfig;

        private GameObject _ghost;
        private Renderer[] _renderers;
        private bool _isValid;

        // Gọi khi player cầm log lên
        public void ShowGhost(GameObject logPrefab)
        {
            HideGhost();
            _ghost = Instantiate(logPrefab);

            // Tắt collider của ghost — không va chạm
            foreach (var col in _ghost.GetComponentsInChildren<Collider>())
                col.enabled = false;

            // Lưu lại renderer để đổi màu
            _renderers = _ghost.GetComponentsInChildren<Renderer>();
            SetMaterial(ghostValid);
        }

        // Gọi khi player thả log / bỏ cầm
        public void HideGhost()
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }
        }

        private void Update()
        {
            if (_ghost == null) return;
            UpdateGhostPosition();
        }

        private void UpdateGhostPosition()
        {
            // Raycast từ camera xuống
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, config.maxRaycastDistance))
            {
                // Di chuyển ghost tới vị trí hit
                _ghost.transform.position = hit.point;
                _ghost.transform.rotation = Quaternion.FromToRotation(
                    Vector3.up, hit.normal);

                // Kiểm tra hợp lệ
                bool valid = CheckValid(hit);
                if (valid != _isValid)
                {
                    _isValid = valid;
                    SetMaterial(_isValid ? ghostValid : ghostInvalid);
                }
            }
        }

        private bool CheckValid(RaycastHit hit)
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            return angle < placementConfig.maxGroundAngle;
        }
        private void SetMaterial(Material mat)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
                r.material = mat;
        }

        public bool IsValidPlacement => _isValid;

        public Vector3 GhostPosition =>
            _ghost != null ? _ghost.transform.position : Vector3.zero;

        public Quaternion GhostRotation =>
            _ghost != null ? _ghost.transform.rotation : Quaternion.identity;
    }
}