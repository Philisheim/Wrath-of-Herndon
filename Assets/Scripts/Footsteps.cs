using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WrathOfHerndon
{
    public class Footsteps : MonoBehaviour
    {
        public AudioSource footsteps;
        void Update()
        {
            if (Input.GetAxis("Vertical") != 0.0 | Input.GetAxis("Horizontal") != 0.0)
            {
                footsteps.enabled = true;
            }
            else
            {
                footsteps.enabled = false;
            }
        }
        
    }
}