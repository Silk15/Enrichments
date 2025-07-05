using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Enrichments
{
    public class ExclusionLineRenderer : MonoBehaviour
    {
        public GameObject linePrefab;
        public int segmentCount = 128;
        public float radius = 0.2f;
        public List<Transform> exclusionPoints;

        public float gapAngle = 40f;
        private List<LineRenderer> activeLines = new();
        private Coroutine fadeCoroutine;

        public void SetPoints(List<Transform> points) => exclusionPoints = points;

        public void SetColor(Color color)
        {
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

        public void Refresh()
        {
            Clear();
            float angleStep = 360f / segmentCount;
            List<float> skipAngles = new();
            foreach (var excl in exclusionPoints)
            {
                Vector3 local = transform.InverseTransformPoint(excl.position).normalized;
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

            if (currentSegment.Count > 1) CreateLine(currentSegment);
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
            fadeCoroutine = StartCoroutine(FadeLines(0f, 1f, duration));
        }

        public void Disable(float duration = 0.5f)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeLines(1f, 0f, duration));
        }

        IEnumerator FadeLines(float startAlpha, float endAlpha, float duration)
        {
            if (activeLines.Count == 0) yield break;

            float time = 0f;
            List<Color> startColors = new();
            foreach (var line in activeLines)
                startColors.Add(line.startColor);

            while (time < duration)
            {
                float t = time / duration;
                float alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                for (int i = 0; i < activeLines.Count; i++)
                {
                    var c = startColors[i];
                    c.a = alpha;
                    activeLines[i].startColor = c;
                    activeLines[i].endColor = c;
                }
                time += Time.deltaTime;
                yield return null;
            }


            for (int i = 0; i < activeLines.Count; i++)
            {
                var c = startColors[i];
                c.a = endAlpha;
                activeLines[i].startColor = c;
                activeLines[i].endColor = c;

                if (Mathf.Approximately(endAlpha, 0f))
                    Destroy(activeLines[i].gameObject);
            }
            if (Mathf.Approximately(endAlpha, 0f))
                activeLines.Clear();
        }
    }
}
