using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class VRGazeClicker : MonoBehaviour
{
    [Header("Dwell Settings")]
    public float dwellTime = 1.0f;

    [Header("Reticle Feedback")]
    public Renderer reticleRenderer; // ลาก GazeReticle มาใส่ (ถ้าไม่ใส่จะค้นหาให้เอง)
    public Color normalColor = Color.white;
    public Color focusColor = Color.green;

    private Button currentButton = null;
    private float gazeTimer = 0f;
    private Material reticleMat;

    void Start()
    {
        Debug.Log("<color=cyan>[VRGazeClicker] Online & Ready on: " + gameObject.name + "</color>");

        if (reticleRenderer == null)
        {
            reticleRenderer = transform.Find("GazeReticle")?.GetComponent<Renderer>();
        }

        if (reticleRenderer != null)
        {
            reticleMat = reticleRenderer.material;
            reticleMat.color = normalColor;
        }
    }

    void Update()
    {
        if (EventSystem.current == null) return;

        // ยิงรังสีจากจุดกึ่งกลางจอ
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        Button hitBtn = null;
        foreach (var r in results)
        {
            hitBtn = r.gameObject.GetComponentInParent<Button>();
            if (hitBtn != null && hitBtn.interactable) break;
        }

        if (hitBtn != null)
        {
            // เปลี่ยนสีจุดเป็นสีเขียวเพื่อบอกว่าเล็งโดนแล้ว
            SetReticleColor(focusColor);

            if (currentButton == hitBtn)
            {
                gazeTimer += Time.deltaTime;
                Debug.Log($"<color=yellow>[Gaze Focusing]</color> {hitBtn.name} ({gazeTimer:F1}/{dwellTime}s)");

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
            SetReticleColor(normalColor);
            if (currentButton != null) ResetGaze();
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