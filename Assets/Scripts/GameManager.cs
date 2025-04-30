using UnityEngine;
using TMPro;

namespace WrathOfHerndon
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI notebookCountText;

        private int notebooksCollected = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            UpdateNotebookUI();
        }

        public void AddNotebook()
        {
            notebooksCollected++;
            UpdateNotebookUI();
        }

        private void UpdateNotebookUI()
        {
            if (notebookCountText != null)
                notebookCountText.text = $"Notebooks: {notebooksCollected}";
        }
    }
}
