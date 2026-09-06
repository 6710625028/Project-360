using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public float mouseSensitivity = 3.0f;
    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        // ถ้าโปรแกรมรันอยู่บนอุปกรณ์จริง (แว่น Pico 4) ให้ปิดสคริปต์นี้ทิ้งทันที
        if (!Application.isEditor)
        {
            this.enabled = false;
        }
    }

    void Update()
    {
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            rotationX += Input.GetAxis("Mouse X") * mouseSensitivity;
            rotationY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            rotationY = Mathf.Clamp(rotationY, -85f, 85f);
            transform.localEulerAngles = new Vector3(rotationY, rotationX, 0f);
        }
    }
}