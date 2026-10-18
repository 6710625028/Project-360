using UnityEngine;

public class HumanChestCompressor : MonoBehaviour
{
    [Header("BlendShape Target")]
    [Tooltip("ลากชิ้นส่วน TShirt ที่มี SkinnedMeshRenderer มาใส่ที่นี่")]
    public SkinnedMeshRenderer tshirtRenderer;
    public int blendShapeIndex = 0; // ลำดับของ ChestCompress

    [Header("Motion Smoothing")]
    public float smoothSpeed = 30f;

    private float targetWeight = 0f;
    private float currentWeight = 0f;

    void Start()
    {
        if (tshirtRenderer == null)
        {
            tshirtRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }
    }

    /// <summary>
    /// รับค่ายุบตัว 0.0 - 1.0 จาก ESP32 แล้วแปลงเป็นสเกล 0 - 100 ของ BlendShape
    /// </summary>
    public void SetDepth(float normalized01)
    {
        targetWeight = Mathf.Clamp01(normalized01) * 100f;
    }

    void Update()
    {
        if (tshirtRenderer == null) return;

        currentWeight = Mathf.Lerp(currentWeight, targetWeight, Time.deltaTime * smoothSpeed);
        tshirtRenderer.SetBlendShapeWeight(blendShapeIndex, currentWeight);
    }
}