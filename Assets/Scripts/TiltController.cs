using UnityEngine;

public class TiltController : MonoBehaviour
{
    public float tiltSpeed = 30f;
    public float maxTilt = 30f;

    private float currentTiltX = 0f;
    private float currentTiltZ = 0f;

    void Update()
    {
        float inputX = Input.GetAxis("Vertical");
        float inputZ = -Input.GetAxis("Horizontal");

        currentTiltX = Mathf.Clamp(currentTiltX + inputX * tiltSpeed * Time.deltaTime, -maxTilt, maxTilt);
        currentTiltZ = Mathf.Clamp(currentTiltZ + inputZ * tiltSpeed * Time.deltaTime, -maxTilt, maxTilt);

        transform.rotation = Quaternion.Euler(currentTiltX, 0, currentTiltZ);
    }
}
