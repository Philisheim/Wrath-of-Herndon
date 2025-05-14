using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

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
            if (notebooksCollected >= 3)
            {
                SceneManager.LoadScene(3);
                Cursor.lockState = CursorLockMode.Confined;
            }
        }

        private void UpdateNotebookUI()
        {
            if (notebookCountText != null)
                notebookCountText.text = $"Notebooks: {notebooksCollected}/3";
        }
    }
}
