using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputHandler : MonoBehaviour
{

    FirstPersonCamera firstPersonCamera;
    Player player;
    // Start is called before the first frame update
    void Start()
    {
        firstPersonCamera = GetComponent<FirstPersonCamera>();
        player = GetComponent<Player>();
    }

    // Update is called once per frame
    void Update()
    {
        HandleCameraInput();
        HandleMoveInput();
    }

    void HandleCameraInput()
    {
        firstPersonCamera.AddXAxisInput(Input.GetAxis("Mouse Y") * Time.deltaTime);
        firstPersonCamera.AddYAxisInput(Input.GetAxis("Mouse X") * Time.deltaTime);
    }

    void HandleMoveInput()
    {
        float forwardInput = Input.GetAxisRaw("Vertical");
        float rightInput = Input.GetAxisRaw("Horizontal");

        player.AddMoveInput(forwardInput, rightInput);
    }
   
}
