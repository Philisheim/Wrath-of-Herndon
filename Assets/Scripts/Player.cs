using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Player : MonoBehaviour
{
    CharacterController controller;
    private Vector3 moveDirection;
    [Tooltip("Set the player's walking speed")]
    public float moveSpeed = 5;
    [Tooltip("Player's max and starting stamina")]
    public float stamina = 50;
    [Tooltip("Rate that stamina drains")]
    public float staminaDrainRate = 10;
    [Tooltip("Rate that stamina regenerates")]
    public float staminaGainRate = 2;
    public TMP_Text staminaDisplay;
    private float maxStamina;
    // Start is called before the first frame update
    void Start()
    {
        controller = GetComponent<CharacterController>();
        maxStamina = stamina;
    }

    // Update is called once per frame
    void Update()
    {
        moveDirection.Normalize();
        moveDirection.y = -0.5f;
        if(Input.GetKey((KeyCode.LeftShift)) && stamina > 0)
        {
            moveSpeed = 10;
            stamina -= staminaDrainRate * Time.deltaTime; 
        }
        else
        {
            moveSpeed = 5;
            if(stamina < maxStamina)
            {
                stamina += staminaGainRate * Time.deltaTime;
            }
        }
        staminaDisplay.text = Mathf.RoundToInt(stamina).ToString();
        controller.Move(moveDirection * moveSpeed * Time.deltaTime);
    }

    public void AddMoveInput(float forwardInput, float rightInput)
    {
        Vector3 forward = Camera.main.transform.forward;
        Vector3 right = Camera.main.transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        moveDirection = (forwardInput * forward) + (rightInput * right);
    }

}
