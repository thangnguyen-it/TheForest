using UnityEngine;
using TheForest.Player;

namespace TheForest.Player
{
    /// <summary>
    /// Điều khiển tư thế model vũ khí động trên tay: Idle <-> Block theo từng vũ khí.
    /// Khắc phục hoàn toàn lỗi tích tụ góc xoay Exponential Rotation bằng bộ lọc Offset nội suy.
    /// </summary>
    public class WeaponPoseController : MonoBehaviour
    {
        [Header("Tham chiếu cấu phần")]
        [SerializeField] private PlayerBlock playerBlock;
        [SerializeField] private WeaponSwinger swinger;
        [SerializeField] private EquipmentController equipment;

        [Header("Chế độ xử lý")]
        [Tooltip("Bật nếu mô hình vũ khí có Animator cấu hình biến bool 'IsBlocking'.")]
        [SerializeField] private bool useAnimator = false;

        [Header("Tween Mặc định (Dự phòng khi tay không hoặc vũ khí trống dữ liệu)")]
        [SerializeField] private Vector3 blockPositionOffset = new Vector3(-0.12f, 0.08f, -0.05f);
        [SerializeField] private Vector3 blockRotationOffset = new Vector3(-20f, 25f, 35f);

        [Header("Động lực học chuyển động")]
        [SerializeField] private float poseLerpSpeed = 12f;
        [SerializeField] private float impactKick = 12f;
        [SerializeField] private float impactReturnSpeed = 10f;

        private Transform _model;
        private Animator _animator;
        private Vector3 _idleLocalPos;
        private Quaternion _idleLocalRot;
        private bool _hasModel;
        private float _impactAmount;

        // Lưu trữ cục bộ Offset trích xuất từ ScriptableObject của vũ khí đang cầm
        private Vector3 _curPosOffset;
        private Vector3 _curRotOffset;

        private static readonly int IsBlockingHash = Animator.StringToHash("IsBlocking");
        private static readonly int BlockImpactHash = Animator.StringToHash("BlockImpact");

        private void Awake()
        {
            if (playerBlock == null) playerBlock = GetComponent<PlayerBlock>();
            if (swinger == null) swinger = GetComponent<WeaponSwinger>();
            if (equipment == null) equipment = GetComponent<EquipmentController>();
        }

        private void OnEnable()
        {
            if (playerBlock != null)
            {
                playerBlock.OnPerfectBlock += HandlePerfectBlock;
                playerBlock.OnNormalBlock += HandleNormalBlock;
            }
        }

        private void OnDisable()
        {
            if (playerBlock != null)
            {
                playerBlock.OnPerfectBlock -= HandlePerfectBlock;
                playerBlock.OnNormalBlock -= HandleNormalBlock;
            }
        }

        public void SetModel(GameObject model)
        {
            if (model == null)
            {
                _model = null;
                _animator = null;
                _hasModel = false;
                return;
            }

            _model = model.transform;
            _animator = model.GetComponentInChildren<Animator>();
            _idleLocalPos = _model.localPosition;
            _idleLocalRot = _model.localRotation;
            _hasModel = true;

            // Đồng bộ dữ liệu tư thế đặc trưng của vũ khí vừa được rút ra khỏi túi đồ
            var item = equipment != null ? equipment.EquippedItem : null;
            if (item != null)
            {
                _curPosOffset = item.blockPositionOffset;
                _curRotOffset = item.blockRotationOffset;
            }
            else
            {
                _curPosOffset = blockPositionOffset;
                _curRotOffset = blockRotationOffset;
            }
        }

        private void Update()
        {
            if (!_hasModel || _model == null) return;

            bool blocking = playerBlock != null && playerBlock.IsBlocking;
            bool swinging = swinger != null && swinger.IsSwinging;

            if (useAnimator && _animator != null)
            {
                _animator.SetBool(IsBlockingHash, blocking && !swinging);
                ApplyAnimatorImpact();
                return;
            }

            // Nếu người chơi đang vung đòn tấn công, nhường hoàn toàn quyền kiểm soát vị trí cho WeaponSwinger
            if (swinging)
            {
                DecayImpact();
                return;
            }

            Vector3 targetPos = blocking ? _idleLocalPos + _curPosOffset : _idleLocalPos;
            Quaternion targetRot = blocking ? _idleLocalRot * Quaternion.Euler(_curRotOffset) : _idleLocalRot;

            // Nội suy tịnh tiến vị trí che chắn mặt
            _model.localPosition = Vector3.Lerp(_model.localPosition, targetPos, poseLerpSpeed * Time.deltaTime);

            // Tách biệt lực giật (Impact Offset) cộng dồn trực tiếp vào điểm đích xoay để triệt tiêu lỗi quay vòng vật thể
            Quaternion impactRotation = Quaternion.Euler(_impactAmount, 0f, 0f);
            _model.localRotation = Quaternion.Slerp(_model.localRotation, targetRot * impactRotation, poseLerpSpeed * Time.deltaTime);

            DecayImpact();
        }

        private void ApplyAnimatorImpact()
        {
            if (_impactAmount > 0.01f && _model != null)
            {
                _model.localRotation *= Quaternion.Euler(_impactAmount, 0f, 0f);
                DecayImpact();
            }
        }

        private void DecayImpact()
        {
            _impactAmount = Mathf.Lerp(_impactAmount, 0f, impactReturnSpeed * Time.deltaTime);
        }

        private void HandlePerfectBlock()
        {
            _impactAmount = impactKick * 1.5f;
            if (useAnimator && _animator != null) _animator.SetTrigger(BlockImpactHash);
        }

        private void HandleNormalBlock()
        {
            _impactAmount = impactKick;
            if (useAnimator && _animator != null) _animator.SetTrigger(BlockImpactHash);
        }
    }
}