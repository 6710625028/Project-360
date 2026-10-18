using UnityEngine;
using UnityEngine.InputSystem;

public class VRHeadSimulator : MonoBehaviour
{
    [Header("Sensitivity")]
    public float sensitivity = 0.15f;
    private float rotX = 0f;
    private float rotY = 0f;

    void Start()
    {
        Vector3 currentAngles = transform.localEulerAngles;
        rotX = currentAngles.y;
        rotY = currentAngles.x;
    }

    void Update()
    {
        // ใช้งานผ่าน New Input System ของ Unity 6
        var mouse = Mouse.current;
        if (mouse == null) return;

        // คลิกขวาค้างเพื่อจำลองการหันศีรษะรอบตัว
        if (mouse.rightButton.isPressed)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            rotX += mouseDelta.x * sensitivity;
            rotY -= mouseDelta.y * sensitivity;
            rotY = Mathf.Clamp(rotY, -80f, 80f);

            transform.localRotation = Quaternion.Euler(rotY, rotX, 0f);
        }
    }
}