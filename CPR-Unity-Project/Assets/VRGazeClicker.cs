using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class VRGazeClicker : MonoBehaviour
{
    [Header("Dwell Settings")]
    public float dwellTime = 1.0f;
    public float minGazeDistance = 0.5f;
    public float maxGazeDistance = 30.0f;

    [Header("Reticle Feedback")]
    public Renderer reticleRenderer;
    public Color normalColor = Color.white;
    public Color focusColor = Color.green;

    private Button currentButton = null;
    private float gazeTimer = 0f;
    private Material reticleMat;
    private Camera targetCam;

    void Start()
    {
        targetCam = GetComponent<Camera>();
        if (targetCam == null) targetCam = Camera.main;

        if (reticleRenderer == null)
            reticleRenderer = transform.Find("GazeReticle")?.GetComponent<Renderer>();

        if (reticleRenderer != null)
        {
            reticleMat = reticleRenderer.material;
            reticleMat.color = normalColor;
        }

        // สั่งสร้างและตั้งค่า Box Collider ให้ทุกปุ่มในฉากอัตโนมัติ
        AutoSetupButtonColliders();
    }

    /// <summary>
    /// ค้นหา Button ทั้งหมดในฉาก และใส่ BoxCollider ให้พอดีกับขนาดปุ่มอัตโนมัติ
    /// </summary>
    private void AutoSetupButtonColliders()
    {
        Button[] allButtons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        int addedCount = 0;

        foreach (var btn in allButtons)
        {
            // ข้ามปุ่มที่ติดอยู่กับกล้อง/HUD เพื่อไม่ให้จุดเล็งยิงโดนตัวเอง
            if (btn.transform.IsChildOf(transform)) continue;

            BoxCollider col = btn.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = btn.gameObject.AddComponent<BoxCollider>();
                addedCount++;
            }

            RectTransform rt = btn.GetComponent<RectTransform>();
            if (rt != null)
            {
                // คำนวณความกว้าง ความสูง และชดเชยจุด Pivot ของปุ่มอัตโนมัติ
                float width = rt.rect.width;
                float height = rt.rect.height;
                col.size = new Vector3(width, height, 5.0f);
                col.center = new Vector3((0.5f - rt.pivot.x) * width, (0.5f - rt.pivot.y) * height, 0f);
            }
        }

        Debug.Log($"<color=cyan>[VRGazeClicker]</color> Auto-configured Colliders for {allButtons.Length} buttons (Added: {addedCount})");
    }

    void Update()
    {
        if (targetCam == null) return;

        Button hitBtn = null;

        // 1. ตรวจสอบผ่าน 3D Physics Raycast จากแนวสายตากล้องตรงๆ (แม่นยำที่สุดใน VR)
        Ray ray = new Ray(targetCam.transform.position, targetCam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxGazeDistance))
        {
            if (hit.distance >= minGazeDistance)
            {
                var btn = hit.collider.GetComponentInParent<Button>();
                if (btn != null && btn.interactable)
                {
                    hitBtn = btn;
                }
            }
        }

        // 2. Fallback: ตรวจสอบผ่าน UI Raycast กึ่งกลาง Viewport
        if (hitBtn == null && EventSystem.current != null)
        {
            Vector2 centerScreen = targetCam.ViewportToScreenPoint(new Vector3(0.5f, 0.5f, 0f));
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = centerScreen
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var r in results)
            {
                if (r.distance < minGazeDistance || (r.distance > 0 && r.distance > maxGazeDistance)) continue;

                var btn = r.gameObject.GetComponentInParent<Button>();
                if (btn != null && btn.interactable)
                {
                    hitBtn = btn;
                    break;
                }
            }
        }

        // ประมวลผลเวลาในการจ้อง (Dwell Time)
        if (hitBtn != null)
        {
            SetReticleColor(focusColor);

            if (currentButton == hitBtn)
            {
                gazeTimer += Time.deltaTime;
                if (gazeTimer >= dwellTime)
                {
                    Debug.Log($"<color=lime>[Gaze Clicked!]</color> {hitBtn.name}");
                    hitBtn.onClick.Invoke();
                    ResetGaze();
                }
            }
            else
            {
                currentButton = hitBtn;
                gazeTimer = 0f;
            }
        }
        else
        {
            if (currentButton != null) ResetGaze();
            else SetReticleColor(normalColor);
        }
    }

    private void SetReticleColor(Color col)
    {
        if (reticleMat != null)
            reticleMat.color = col;
    }

    private void ResetGaze()
    {
        currentButton = null;
        gazeTimer = 0f;
        SetReticleColor(normalColor);
    }
}