using System;
using System.Collections;
using System.Collections.Generic;
using ThunderRoad;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

namespace Enrichments
{
    public class EnrichmentMessage : MonoBehaviour
    {
        [Header("UI")]
        public SpriteRenderer backgroundRenderer;
        public SpriteRenderer buttonRenderer;
        public ParticleSystem[] loopingEffects;
        public ParticleSystem buyEffect;

        [Header("Video")]
        public VideoPlayer videoPlayer;

        [Header("Text")]
        public TextMeshPro titleText;
        public TextMeshPro descriptionText;

        [Header("Values")]
        public float upOffset = 0.2f;

        public float forwardOffset = 0.05f;
        public float followSpeed = 20f;
        public float scaleSpeed = 3f;
        protected float currentScale;

        [NonSerialized]
        public bool isShown;
        
        [NonSerialized]
        public EnrichmentOrb enrichmentOrb;

        private Side side;
        
        public static void Create(EnrichmentOrb enrichmentOrb, Action<EnrichmentMessage> onComplete)
        {
            Catalog.LoadAssetAsync<GameObject>("Silk.Prefab.Enrichments.Message", o =>
            {
                var obj = Instantiate(o, enrichmentOrb.transform.position, enrichmentOrb.transform.rotation);
                if (obj.TryGetComponent(out EnrichmentMessage component))
                {
                    component.enrichmentOrb = enrichmentOrb;
                    onComplete?.Invoke(component);
                    return;
                }
                Debug.Log($"[Enrichments] Failed to load enrichment message on prefab: {obj.name}, there is no EnrichmentMessage component attached!");
            }, "Silk.Prefab.Enrichments.Message");
        }

        public void Enable() => isShown = true;

        public void FixedUpdate()
        {
            if (!enrichmentOrb)
            {
                Destroy(this);
                return;
            }
            Vector3 targetPos = enrichmentOrb.transform.position + Vector3.up * upOffset + (transform.position - Player.local.head.transform.position).normalized * forwardOffset;
            Quaternion targetRot = Quaternion.LookRotation(transform.position - Player.local.head.transform.position);

            if (isShown)
            {
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.unscaledDeltaTime * followSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.unscaledDeltaTime * followSpeed);

                if (currentScale < 1.0f)
                {
                    currentScale = Mathf.Min(1.0f, currentScale + scaleSpeed * Time.deltaTime);
                    transform.localScale = Vector3.one * currentScale;
                }
            }
            else if (currentScale > 0.0f)
            {
                currentScale = Mathf.Max(0.0f, currentScale - scaleSpeed * Time.deltaTime);
                transform.localScale = Vector3.one * currentScale;
            }
        }

        public void Hide() => isShown = false;
    }
}