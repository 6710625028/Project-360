using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// Viewer หลักสำหรับ PICO: โหลด equirectangular เต็มใบไปแปะด้านใน Sphere
public class Pano360SphereViewer : MonoBehaviour
{
    [Header("Server")]
    [Tooltip("URL ของ mock server เช่น http://192.168.1.50:5000")]
    public string serverBaseUrl = "http://192.168.1.50:5000";

    [Header("Target")]
    [Tooltip("Renderer ของ Sphere ที่ครอบกล้องอยู่และพลิก normal เข้าด้านในแล้ว")]
    public Renderer targetRenderer;

    [Header("Refresh")]
    [Tooltip("ความถี่ในการดึงภาพเต็มใบมาอัปเดต (วินาที)")]
    [Range(0.2f, 5f)]
    public float refreshInterval = 1.0f;

    [Tooltip("ดึงภาพทันทีตอนเริ่มเกม")]
    public bool fetchOnStart = true;

    private Coroutine refreshRoutine;
    private bool requestInFlight;
    private Texture2D downloadedTexture;

    private void Start()
    {
        if (fetchOnStart)
        {
            StartCoroutine(FetchFullPano());
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying && refreshRoutine == null)
        {
            refreshRoutine = StartCoroutine(RefreshLoop());
        }
    }

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(refreshInterval);
            yield return FetchFullPano();
        }
    }

    private IEnumerator FetchFullPano()
    {
        if (requestInFlight)
        {
            yield break;
        }

        requestInFlight = true;
        string url = $"{serverBaseUrl}/pano/full";

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                ApplyDownloadedTexture(DownloadHandlerTexture.GetContent(request));
            }
            else
            {
                Debug.LogWarning($"[Pano360SphereViewer] Download failed: {request.error} (url={url})");
            }
        }

        requestInFlight = false;
    }

    private void ApplyDownloadedTexture(Texture2D texture)
    {
        if (targetRenderer == null)
        {
            Destroy(texture);
            return;
        }

        if (downloadedTexture != null)
        {
            Destroy(downloadedTexture);
        }

        downloadedTexture = texture;
        targetRenderer.material.mainTexture = texture;
    }

    // เรียกจากปุ่ม UI เพื่อโหลดภาพใหม่ทันที
    public void ForceRefreshNow()
    {
        StartCoroutine(FetchFullPano());
    }

    private void OnDisable()
    {
        if (refreshRoutine != null)
        {
            StopCoroutine(refreshRoutine);
            refreshRoutine = null;
        }
    }

    private void OnDestroy()
    {
        if (downloadedTexture != null)
        {
            Destroy(downloadedTexture);
        }
    }
}
