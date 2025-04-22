using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WrathOfHerndon
{
    public class MainMenu : MonoBehaviour
    {
        public int difficulty = 1;

        // Change difficulty based off of what button is pressed
        // Difficulty is a number with 1 being low and 3 being high
        public void Difficulty()
        {
            
        }
        public void PlayGame()
        {
            
            SceneManager.LoadScene(1);
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}