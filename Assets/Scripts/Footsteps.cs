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
            if (Input.GetAxis("vertical") != 0.0 | Input.GetAxis("horizontal") != 0.0)
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