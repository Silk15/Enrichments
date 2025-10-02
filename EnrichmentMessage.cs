using System;
using System.Collections;
using ThunderRoad;
using ThunderRoad.DebugViz;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

namespace Enrichments
{
    public class EnrichmentMessage : MonoBehaviour
    {
        [Header("UI")]
        public SpriteRenderer buttonRenderer;
        public ParticleSystem buyEffect;
        public ParticleSystem loopingEffect;
        
        [Header("Buying")]
        public TextMeshPro costText;

        [Header("Video")]
        public VideoPlayer videoPlayer;
        public Renderer videoRenderer;

        [Header("Text")]
        public TextMeshPro titleText;
        public TextMeshPro descriptionText;
        
        [Header("Item Values")]
        public TextMeshPro currentText;
        public TextMeshPro maxText;

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

        [NonSerialized]
        public Coroutine flashCoroutine;
        
        private ParticleSystem[] loopParticles;
        private ParticleSystem.MinMaxGradient[] loopOriginals;

        public static void Create(EnrichmentOrb enrichmentOrb, Action<EnrichmentMessage> onComplete)
        {
            Catalog.LoadAssetAsync<GameObject>("Silk.Prefab.Enrichments.Message", o =>
            {
                var obj = Instantiate(o, EnrichmentOrb.enrichmentRoot.transform);
                if (obj.TryGetComponent(out EnrichmentMessage component))
                {
                    component.enrichmentOrb = enrichmentOrb;
                    component.InitializeVideoRendering();
                    onComplete?.Invoke(component);
                    return;
                }

                Debug.Log($"[Enrichments] Failed to load enrichment message on prefab: {obj.name}, there is no EnrichmentMessage component attached!");
            }, $"Enrichment Message");
        }

        /// <summary>
        /// We create a new render texture and material for each message so that they no longer use a shared asset, allowing multiple messages to display unique videos
        /// </summary>
        public void InitializeVideoRendering()
        {
            if (videoPlayer.targetTexture != null) return;

            RenderTexture renderTexture = new RenderTexture(256, 256, 0) { name = $"EnrichmentMessageRenderTexture" };
            videoPlayer.targetTexture = renderTexture;

            videoRenderer.material = new(videoRenderer.material);
            videoRenderer.material.mainTexture = renderTexture;
        }
        
        public void UpdateCost(EnrichmentOrb orb)
        {
            costText.text = orb.enrichmentData.cost.ToString();
        }

        public void UpdateValues(Item item)
        {
            if (item == null) return;
            if (item.TryGetCustomData(out ContentCustomEnrichment customEnrichments))
            {
                currentText.text = customEnrichments.Enrichments.Count.ToString();
                maxText.text = customEnrichments.MaxEnrichments.ToString();
            }
            else
            {
                currentText.text = "0";
                maxText.text = "4";
            }
        }

        private void Awake()
        {
            if (loopingEffect != null)
            {
                loopParticles = loopingEffect.GetComponentsInChildren<ParticleSystem>();
                loopOriginals = new ParticleSystem.MinMaxGradient[loopParticles.Length];
                for (int i = 0; i < loopParticles.Length; i++)
                    loopOriginals[i] = loopParticles[i].colorOverLifetime.color;
            }
        }

        public void Flash(Color color)
        {
            if (flashCoroutine != null)
                StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashRoutine(color));
        }

        private IEnumerator FlashRoutine(Color flashColor)
        {
            float duration = 0.1f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                for (int i = 0; i < loopParticles.Length; i++)
                {
                    var col = loopParticles[i].colorOverLifetime;
                    col.color = new ParticleSystem.MinMaxGradient(Color.Lerp(loopOriginals[i].colorMax, flashColor, t));
                }

                yield return null;
            }

            t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                for (int i = 0; i < loopParticles.Length; i++)
                {
                    var col = loopParticles[i].colorOverLifetime;
                    col.color = new ParticleSystem.MinMaxGradient(Color.Lerp(flashColor, loopOriginals[i].colorMax, t));
                }

                yield return null;
            }

            for (int i = 0; i < loopParticles.Length; i++)
            {
                var col = loopParticles[i].colorOverLifetime;
                col.color = loopOriginals[i];
            }

            flashCoroutine = null;
        }

        public void Enable()
        {
            UpdateCost(enrichmentOrb);
            transform.SetParent(null);
            transform.position = enrichmentOrb.transform.position;
            transform.rotation = enrichmentOrb.transform.rotation;
            videoPlayer.enabled = true;
            videoPlayer.Play();
            isShown = true;
        }

        public void FixedUpdate()
        {
            if (Player.currentCreature && enrichmentOrb)
            {
                Vector3 targetPos = enrichmentOrb.transform.position + Vector3.up * upOffset + (transform.position - Player.local.head.transform.position).normalized * forwardOffset;
                Quaternion targetRot = Quaternion.LookRotation(transform.position - Player.local.head.transform.position);

                VizManager.AddOrUpdateViz(this, $"message{enrichmentOrb.enrichmentData.id}", enrichmentOrb.enrichmentData.primarySkillTree.color, VizManager.VizType.Lines, new []{transform.position, targetPos});

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
        }

        public void Hide()
        {
            VizManager.ClearViz(this, $"message{enrichmentOrb.enrichmentData.id}");
            transform.SetParent(enrichmentOrb.transform);
            videoPlayer.enabled = false;
            videoPlayer.Stop();
            isShown = false;
        }
    }
}