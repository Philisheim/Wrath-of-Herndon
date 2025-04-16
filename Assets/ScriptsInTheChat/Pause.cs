using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WrathOfHerndon
{
    public class Pause : MonoBehaviour
    {
        public GameObject menu;
        public bool isPaused;
        public int typeOfPause = 0;
        // Start is called before the first frame update
        void Start()
        {
            menu.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!isPaused)
                {
                    OpenMenu();
                }
            }
        }

        public void OpenMenu()
        {
            menu.SetActive(true);
            Time.timeScale = 0f;
            isPaused = true;
        }

        public void Resume()
        {
            menu.SetActive(false);
            Time.timeScale = 1f;
            isPaused = false;
        }

        public void Exit()
        {
            SceneManager.LoadScene(0);
            Time.timeScale = 1f;
        }
    }
}