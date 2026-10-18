using UnityEngine;
using TMPro;

public class CPRUIDisplay : MonoBehaviour
{
    [Header("References")]
    public CPRLogicManager cprManager;

    [Header("Real-Time Labels")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI bpmText;
    [Tooltip("ลาก Text แสดงเวลานับถอยหลังมาใส่ที่นี่ (ถ้ามี)")]
    public TextMeshProUGUI timerText;
    [Tooltip("ลาก Text แสดงสัดส่วนเวลาการกด CCF มาใส่ที่นี่ (ถ้ามี)")]
    public TextMeshProUGUI ccfText;

    void OnEnable()
    {
        if (cprManager == null) return;

        cprManager.OnScoreUpdated += HandleScoreUpdated;
        cprManager.OnCompressionDetected += HandleCompressionDetected;
    }

    void OnDisable()
    {
        if (cprManager == null) return;

        cprManager.OnScoreUpdated -= HandleScoreUpdated;
        cprManager.OnCompressionDetected -= HandleCompressionDetected;
    }

    void Update()
    {
        if (cprManager == null) return;

        bool showInCompressions = cprManager.currentState == CPRLogicManager.ScenarioState.Compressions;

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(showInCompressions);
            if (showInCompressions) scoreText.text = $"Score: {cprManager.score:F1}";
        }

        if (bpmText != null)
        {
            bpmText.gameObject.SetActive(showInCompressions);
            if (showInCompressions) bpmText.text = $"BPM: {cprManager.CurrentBpm:F0}";
        }

        // Live Timer (MM:SS)
        if (timerText != null)
        {
            timerText.gameObject.SetActive(showInCompressions);
            if (showInCompressions)
            {
                int minutes = Mathf.FloorToInt(cprManager.TimeRemaining / 60f);
                int seconds = Mathf.FloorToInt(cprManager.TimeRemaining % 60f);
                timerText.text = $"Time: {minutes:00}:{seconds:00}";
            }
        }

        // Live CCF %
        if (ccfText != null)
        {
            ccfText.gameObject.SetActive(showInCompressions);
            if (showInCompressions)
            {
                ccfText.text = $"CCF: {cprManager.CurrentCCF:F0}%";
            }
        }
    }

    private void HandleScoreUpdated(float newScore)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {newScore:F1}";
    }

    private void HandleCompressionDetected(int totalCompressions)
    {
        if (bpmText != null)
            bpmText.text = $"BPM: {cprManager.CurrentBpm:F0}";
    }
}