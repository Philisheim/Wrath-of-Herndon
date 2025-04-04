using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    CharacterController controller;
    private Vector3 moveDirection;
    [Header("Movement settings")]
    [Tooltip("Set the player's walking speed")]
    public float moveSpeed = 5;

    [Header("Stamina settings")]
    [Tooltip("Player's max and starting stamina")]
    public float stamina = 50;
    [Tooltip("Rate that stamina drains")]
    public float staminaDrainRate = 10;
    [Tooltip("Base rate that stamina regenerates")]
    public float staminaGainRate = 2;
    [Tooltip("Time delay before stamina starts regenerating after sprinting (seconds)")]
    public float staminaRegenDelay = 2f;
    [Tooltip("Additional stamina regen per second after delay")]
    public float staminaRegenIncrease = 0.5f;

    [Header("Misc/Visual")]
    public float speed;
    public float speedEffect;

    private float maxStamina;
    private float staminaRegenTimer = 0f;
    private float regenMultiplier = 1f;
    public Slider FillEffect;
    public Slider Fill;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        maxStamina = stamina;
        Fill.maxValue = maxStamina;
        FillEffect.maxValue = maxStamina;
    }

    void Update()
    {
        if (stamina == maxStamina)
        {
            FillEffect.value = 100;
            Fill.value = 100;
        }
        else
        {
            float target = Mathf.Lerp(Fill.value, stamina, speed * Time.deltaTime);
            float targetEffect = Mathf.Lerp(FillEffect.value, stamina, speedEffect * Time.deltaTime);

            Fill.value = target;
            FillEffect.value = targetEffect;
        }

        moveDirection.y = -0.5f;

        if (Input.GetKey(KeyCode.LeftShift) && stamina > 0 && controller.velocity.magnitude > 0.1f)
        {
            moveSpeed = 10;
            stamina -= staminaDrainRate * Time.deltaTime;
            staminaRegenTimer = 0f;
            regenMultiplier = 1f;
        }
        else
        {
            moveSpeed = 5;

            if (stamina < maxStamina)
            {
                staminaRegenTimer += Time.deltaTime;

                if (staminaRegenTimer >= staminaRegenDelay)
                {
                    regenMultiplier += staminaRegenIncrease * Time.deltaTime;
                    stamina += (staminaGainRate * regenMultiplier) * Time.deltaTime;
                    stamina = Mathf.Min(stamina, maxStamina);
                }
            }
        }

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

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Herndon"))
        {
            SceneManager.LoadScene(0);
        }
    }

}
