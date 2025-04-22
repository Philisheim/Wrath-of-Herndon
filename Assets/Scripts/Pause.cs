using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;


namespace WrathOfHerndon
{
    public class Pause : MonoBehaviour
    {
        public GameObject menu;
        public bool isPaused;
        public int typeOfPause = 0;
        private PostProcessVolume ppVol;

        // Start is called before the first frame update
        void Start()
        {
            menu.SetActive(false);
        }

        private void Update()
        {
            ppVol = Camera.main.gameObject.GetComponent<PostProcessVolume>();
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
            ppVol.enabled = true;
        }

        public void Resume()
        {
            menu.SetActive(false);
            Time.timeScale = 1f;
            isPaused = false;
            ppVol.enabled = false;
        }

        public void Exit()
        {
            ppVol.enabled = false;
            SceneManager.LoadScene(0);
            Time.timeScale = 1f;
        }
    }
}