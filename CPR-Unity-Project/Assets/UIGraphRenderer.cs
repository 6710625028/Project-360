using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class UIGraphRenderer : MaskableGraphic
{
    [Header("Waveform Settings")]
    public float lineThickness = 3.5f;
    public Color lineColor = Color.green;

    [Header("Target Guide Settings (Adult CPR: 5-6 cm out of 7 cm)")]
    public bool showTargetZone = true;
    public Color targetZoneColor = new Color(0f, 1f, 0f, 0.15f); // แถบเขียวโปร่งแสง
    public Color targetLineColor = new Color(0.2f, 0.8f, 0.2f, 0.6f); // เส้นขอบเขียว
    public Color dangerLineColor = new Color(1f, 0.2f, 0.2f, 0.7f); // เส้นแดง 6.0 cm
    public Color baselineColor = new Color(1f, 1f, 1f, 0.3f);       // เส้นพื้น 0 cm

    private readonly List<float> dataPoints = new List<float>();
    private int maxPoints = 150; // บันทึกประมาณ 4.5 วินาที

    // เกณฑ์มาตรฐาน 5.0 - 6.0 ซม. เทียบกับสเกลเต็ม 7.0 ซม.
    private const float TARGET_MIN_NORM = 5.0f / 7.0f; // ~0.714
    private const float TARGET_MAX_NORM = 6.0f / 7.0f; // ~0.857

    public void SetMaxPoints(int count)
    {
        maxPoints = count;
    }

    public void AddPoint(float normalizedValue)
    {
        dataPoints.Add(Mathf.Clamp01(normalizedValue));
        if (dataPoints.Count > maxPoints)
        {
            dataPoints.RemoveAt(0);
        }
        SetVerticesDirty();
    }

    public void ClearGraph()
    {
        dataPoints.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;

        // 1. วาดเส้น Baseline (0 cm ล่างสุด)
        DrawLineSegment(vh, 
            new Vector2(rect.xMin, rect.yMin), 
            new Vector2(rect.xMax, rect.yMin), 
            baselineColor, 2f);

        // 2. วาดพื้นที่ Target Zone (5.0 - 6.0 ซม.)
        if (showTargetZone)
        {
            float yMinTarget = rect.yMin + (TARGET_MIN_NORM * rect.height);
            float yMaxTarget = rect.yMin + (TARGET_MAX_NORM * rect.height);

            // วาดแถบสี่เหลี่ยมสีเขียวโปร่งแสง
            DrawFilledBox(vh, rect.xMin, rect.xMax, yMinTarget, yMaxTarget, targetZoneColor);

            // วาดเส้นขอบล่าง 5.0 cm (เส้นเขียว)
            DrawLineSegment(vh, new Vector2(rect.xMin, yMinTarget), new Vector2(rect.xMax, yMinTarget), targetLineColor, 1.5f);

            // วาดเส้นขอบบน 6.0 cm (เส้นเตือนสีแดง)
            DrawLineSegment(vh, new Vector2(rect.xMin, yMaxTarget), new Vector2(rect.xMax, yMaxTarget), dangerLineColor, 2f);
        }

        // 3. วาดเส้นคลื่น CPR Waveform
        if (dataPoints.Count < 2) return;

        float xStep = rect.width / (maxPoints - 1);
        for (int i = 0; i < dataPoints.Count - 1; i++)
        {
            Vector2 p1 = new Vector2(rect.xMin + (i * xStep), rect.yMin + (dataPoints[i] * rect.height));
            Vector2 p2 = new Vector2(rect.xMin + ((i + 1) * xStep), rect.yMin + (dataPoints[i + 1] * rect.height));

            DrawLineSegment(vh, p1, p2, lineColor, lineThickness);
        }
    }

    private void DrawFilledBox(VertexHelper vh, float xMin, float xMax, float yMin, float yMax, Color color)
    {
        int index = vh.currentVertCount;

        UIVertex v1 = UIVertex.simpleVert; v1.color = color; v1.position = new Vector3(xMin, yMin, 0);
        UIVertex v2 = v1; v2.position = new Vector3(xMin, yMax, 0);
        UIVertex v3 = v1; v3.position = new Vector3(xMax, yMax, 0);
        UIVertex v4 = v1; v4.position = new Vector3(xMax, yMin, 0);

        vh.AddVert(v1);
        vh.AddVert(v2);
        vh.AddVert(v3);
        vh.AddVert(v4);

        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }

    private void DrawLineSegment(VertexHelper vh, Vector2 from, Vector2 to, Color color, float thickness)
    {
        Vector2 dir = (to - from).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        int index = vh.currentVertCount;

        UIVertex v1 = UIVertex.simpleVert; v1.color = color; v1.position = from - normal;
        UIVertex v2 = UIVertex.simpleVert; v2.color = color; v2.position = from + normal;
        UIVertex v3 = UIVertex.simpleVert; v3.color = color; v3.position = to + normal;
        UIVertex v4 = UIVertex.simpleVert; v4.color = color; v4.position = to - normal;

        vh.AddVert(v1);
        vh.AddVert(v2);
        vh.AddVert(v3);
        vh.AddVert(v4);

        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }
}