using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Enrichments
{
    public class ExclusionLineRenderer : MonoBehaviour
    {
        public GameObject linePrefab;
        public int segmentCount = 128;
        public float radius = 0.2f;
        public List<Vector3> exclusionPositions = new();

        public Color color;
        public float gapAngle = 40f;
        private List<LineRenderer> activeLines = new();
        private Coroutine fadeCoroutine;
        private float currentAlpha = 0f;

        public void SetPoints(List<Vector3> positions)
        {
            exclusionPositions = positions;
            Debug.Log($"Setting {positions.Count} exclusion points on line: {gameObject.name} to, {string.Join(", ", exclusionPositions.Select(p => p.ToString()))}");
        }

        public void Refresh()
        {
            Clear();
            float angleStep = 360f / segmentCount;

            List<float> skipAngles = new();
            foreach (var pos in exclusionPositions)
            {
                Vector3 local = transform.InverseTransformPoint(pos).normalized;
                float angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;
                skipAngles.Add(angle);
            }

            List<Vector3> currentSegment = new();
            for (int i = 0; i <= segmentCount; i++)
            {
                float angle = i * angleStep;
                bool skip = false;

                foreach (float skipAngle in skipAngles)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(angle, skipAngle)) < gapAngle / 2f)
                    {
                        skip = true;
                        break;
                    }
                }

                if (skip)
                {
                    if (currentSegment.Count > 1)
                        CreateLine(currentSegment);
                    currentSegment.Clear();
                    continue;
                }

                Vector3 point = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0) * radius;
                currentSegment.Add(point);
            }

            if (currentSegment.Count > 1)
                CreateLine(currentSegment);
        }


        public void SetColor(Color color)
        {
            this.color = color;
            foreach (LineRenderer lineRenderer in activeLines)
            {
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
            }
        }

        public void Clear()
        {
            foreach (LineRenderer lineRenderer in activeLines) DestroyImmediate(lineRenderer.gameObject);
            activeLines.Clear();
        }

        void CreateLine(List<Vector3> segment)
        {
            var go = Instantiate(linePrefab, transform);
            var line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = segment.Count;
            line.SetPositions(segment.ToArray());
            activeLines.Add(line);
        }

        public void Enable(float duration = 0.5f)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeLines(1f, duration));
        }

        public void Disable(float duration = 0.5f)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeLines(0f, duration));
        }

        IEnumerator FadeLines(float targetAlpha, float duration)
        {
            if (activeLines.Count == 0)
            {
                fadeCoroutine = null;
                yield break;
            }

            float startAlpha = currentAlpha;
            float time = 0f;

            while (time < duration)
            {
                float t = time / duration;
                currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                foreach (var line in activeLines)
                {
                    if (line == null) continue;
                    SetLineAlpha(line, currentAlpha);
                }
                time += Time.deltaTime;
                yield return null;
            }

            currentAlpha = targetAlpha;

            foreach (var line in activeLines)
            {
                if (line == null) continue;
                SetLineAlpha(line, currentAlpha);
                if (Mathf.Approximately(currentAlpha, 0f))
                    Destroy(line.gameObject);
            }

            if (Mathf.Approximately(currentAlpha, 0f))
                activeLines.Clear();

            fadeCoroutine = null;
        }
        
        void SetLineAlpha(LineRenderer line, float alpha)
        {
            Color start = line.startColor;
            Color end = line.endColor;
            start.a = end.a = alpha;
            line.startColor = start;
            line.endColor = end;
        }

        void SetLineAlpha(float alpha)
        {
            foreach (var line in activeLines)
            {
                if (line == null) continue;
                SetLineAlpha(line, alpha);
            }
        }

    }
}
