using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using TheForest.World;

namespace TheForest.UI
{
    /// <summary>
    /// Menu chính: chọn độ khó rồi Play. Lưu lựa chọn vào DifficultyCarrier,
    /// load scene gameplay. Hiển thị mô tả từng mode.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private DifficultyDatabase database;

        [Header("UI")]
        [SerializeField] private TMP_Dropdown difficultyDropdown;   // hoặc nút riêng
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button playButton;

        [Header("Scene")]
        [Tooltip("Tên scene gameplay (phải thêm vào Build Settings).")]
        [SerializeField] private string gameplayScene = "Island";

        private void Awake()
        {
            BuildDropdown();
            if (playButton != null) playButton.onClick.AddListener(OnPlay);
            if (difficultyDropdown != null)
                difficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);
            OnDifficultyChanged(0);
        }

        private void BuildDropdown()
        {
            if (difficultyDropdown == null || database == null) return;
            difficultyDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string>();
            foreach (var d in database.difficulties)
                if (d != null) options.Add(d.mode.ToString());
            difficultyDropdown.AddOptions(options);
        }

        private void OnDifficultyChanged(int index)
        {
            if (database == null || index < 0 || index >= database.difficulties.Length) return;
            var d = database.difficulties[index];
            if (descriptionText != null) descriptionText.text = DescribeDifficulty(d);
        }

        private string DescribeDifficulty(DifficultySettings d)
        {
            if (d == null) return string.Empty;
            switch (d.mode)
            {
                case DifficultyMode.Peaceful:
                    return "Yên bình: không/ít kẻ địch. Tập trung xây dựng & sinh tồn.";
                case DifficultyMode.Normal:
                    return "Thường: trải nghiệm cân bằng.";
                case DifficultyMode.Hard:
                    return "Khó: địch hung hãn hơn (tấn công 2.5×), mutant kháng lửa, khó knockdown, tàng hình tối đa 50%.";
                case DifficultyMode.HardSurvival:
                    return "Khó Sinh tồn: như Khó + ít động vật, hồi phục chậm, quả độc chí mạng.";
                default: return d.mode.ToString();
            }
        }

        private void OnPlay()
        {
            int idx = difficultyDropdown != null ? difficultyDropdown.value : 0;
            DifficultySettings chosen = (database != null && idx < database.difficulties.Length)
                ? database.difficulties[idx] : null;

            DifficultyCarrier.Selected = chosen;
            SceneManager.LoadScene(gameplayScene);
        }

        public void OnQuit() => Application.Quit();
    }
}
