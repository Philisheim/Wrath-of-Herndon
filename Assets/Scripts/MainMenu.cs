using UnityEngine;
using UnityEngine.SceneManagement;

namespace WrathOfHerndon
{
    public class MainMenu : MonoBehaviour
    {
        // Made static since we want to read it from any other classes/scenes
        public static int difficulty = 1;

        public void SetDifficulty(int level)
        {
            difficulty = Mathf.Clamp(level, 1, 3);
        }

        public void PlayGame()
        {
            
            PlayerPrefs.SetInt("GameDifficulty", difficulty);
            PlayerPrefs.Save();

            print(difficulty);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
