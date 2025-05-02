using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WrathOfHerndon
{
    public class Footsteps : MonoBehaviour
    {
        public AudioSource audioSource;

        // Update is called once per frame
        void Update()
        {
            if (Input.GetAxis("Vertical") != 0.0 || Input.GetAxis("Horizontal") != 0.0)
            {
                audioSource.enabled = true;
            }
            else
            {
                audioSource.enabled = false;
            }
        }
    }
}