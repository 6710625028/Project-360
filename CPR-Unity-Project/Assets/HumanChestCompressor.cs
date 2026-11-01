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

    [Header("Body Reaction")]
    [Tooltip("ลากกระดูกหน้าอก/สันหลัง (Spine/Chest) ของโมเดลมาใส่")]
    public Transform chestBone;
    [Tooltip("ระยะยุบตัวของกระดูก (ปรับทิศทางตามแกนของโมเดล)")]
    public Vector3 compressOffset = new Vector3(0, -0.00f, 0); // ประมาณ 3 ซม.
    private Vector3 initialBonePos;

    void Start()
    {
        if (tshirtRenderer == null)
        {
            tshirtRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        // บันทึกตำแหน่งกระดูกเริ่มต้นตอนยืน/นอนปกติ
        if (chestBone != null)
        {
            initialBonePos = chestBone.localPosition;
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
        // คำนวณเกลี่ยความสมูททั้ง BlendShape และตำแหน่งกระดูก
        currentWeight = Mathf.Lerp(currentWeight, targetWeight, Time.deltaTime * smoothSpeed);
        float normalizedProgress = currentWeight / 100f; // สเกล 0.0 ถึง 1.0

        // 1. สั่งให้อกบุบผ่าน BlendShape
        if (tshirtRenderer != null)
        {
            tshirtRenderer.SetBlendShapeWeight(blendShapeIndex, currentWeight);
        }

        // 2. เลื่อนกระดูกหน้าอกยุบลงตามแรงกดจริงแบบ Real-time
        if (chestBone != null)
        {
            chestBone.localPosition = initialBonePos + (compressOffset * normalizedProgress);
        }
    }
}