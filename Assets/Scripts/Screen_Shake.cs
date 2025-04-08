using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Screen_Shake : MonoBehaviour
{
    public bool shake = false;
    public AnimationCurve curve;
    public float duration = 1.5f;

    // Update is called once per frame
    public void Shake()
    {
        if(shake)
        {
            shake = false;
            StartCoroutine(Shaking());
        }
    }

    IEnumerator Shaking()
    {
        // Save the initial local position relative to the player.
        Vector3 startLocalPosition = transform.localPosition;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float strength = curve.Evaluate(elapsedTime / duration);
            // Apply the shake effect using localPosition.
            transform.localPosition = startLocalPosition + Random.insideUnitSphere * strength;
            yield return null;
        }
        // Restore the original local position.
        transform.localPosition = startLocalPosition;
    }

}
