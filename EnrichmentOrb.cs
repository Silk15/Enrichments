using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using ThunderRoad;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace Enrichments
{
    public class EnrichmentOrb : ThunderBehaviour
    {
        private static Queue<EnrichmentOrb> orbQueue = new();
        public static GameObject enrichmentRoot;
        private static GameObject enrichmentPrefab;
        public static bool poolsLoaded;

        public ParticleSystem particleSystem;
        public SpriteRenderer spriteRenderer;
        public AudioSource grabAudioSource;
        public AudioSource ungrabAudioSource;
        public Rigidbody rigidbody;
        public Handle handle;
        public EnrichmentData enrichmentData;
        private bool isRunning;
        private bool isActionEnabled;
        public string enableLoopEffectId = "SkillBuyLoop";
        public string disableEffectId = "RefundSkill";
        public string enableEffectId = "BuySkill";
        private EffectData enableLoopEffectData;
        private EffectData enableEffectData;
        private EffectData disableEffectData;
        public EffectInstance enableEffectInstance;
        public EnrichmentMessage enrichmentMessage;
        public UIEnrichmentCore uiEnrichmentCore;

        public event OnSpawn onSpawn;
        public event OnDespawn onDespawn;
        public delegate void OnSpawn(EnrichmentOrb enrichmentOrb, EventTime eventTime);
        public delegate void OnDespawn(EnrichmentOrb enrichmentOrb);

        public bool Active { get; private set; }

        public static IEnumerator TryGeneratePools(int count)
        {
            if (poolsLoaded) yield break;
            Stopwatch stopwatch = Stopwatch.StartNew();
            yield return Catalog.LoadAssetCoroutine<GameObject>("Silk.Prefab.Enrichments.Orb", prefab => enrichmentPrefab = prefab, "Silk.Prefab.Enrichments.Orb");
            if (enrichmentPrefab == null)
            {
                Debug.LogError("[Enrichments] Failed to load enrichment prefab, please ensure this mod is installed correctly!");
                yield break;
            }

            enrichmentRoot = new GameObject("Enrichment Root");
            DontDestroyOnLoad(enrichmentRoot);

            for (int i = 0; i < count; i++)
            {
                var prefab = Instantiate(enrichmentPrefab, enrichmentRoot.transform);
                if (!prefab.TryGetComponent(out EnrichmentOrb enrichmentOrb))
                {
                    Debug.Log($"[Enrichments] Failed to load prefab, gameObject ({enrichmentOrb.name}) does not contain an [{nameof(EnrichmentOrb)}] component.");
                    yield break;
                }

                enrichmentOrb.enableEffectData = Catalog.GetData<EffectData>(enrichmentOrb.enableEffectId);
                enrichmentOrb.disableEffectData = Catalog.GetData<EffectData>(enrichmentOrb.disableEffectId);
                enrichmentOrb.enableLoopEffectData = Catalog.GetData<EffectData>(enrichmentOrb.enableLoopEffectId);
                enrichmentOrb.rigidbody.Sleep();
                EnrichmentMessage.Create(enrichmentOrb, message =>
                {
                    enrichmentOrb.enrichmentMessage = message;
                    enrichmentOrb.enrichmentMessage.gameObject.SetActive(false);
                    message.transform.SetParent(enrichmentOrb.transform);
                });
                orbQueue.Enqueue(enrichmentOrb);
                enrichmentOrb.gameObject.SetActive(false);
                yield return Yielders.EndOfFrame;
            }

            poolsLoaded = true;
            GeneratePools(count, stopwatch);
        }

        private static void GeneratePools(int count, Stopwatch stopwatch) => Debug.Log($"[Enrichments] Pooled {count} enrichment orbs in {stopwatch.Elapsed.TotalMilliseconds} ms");

        private static EnrichmentOrb Get(Vector3 position, Quaternion rotation)
        {
            EnrichmentOrb orb = null;
            if (!poolsLoaded) Debug.LogError("[Enrichments] Pool has not been loaded, this may be due to a null or corrupted prefab, please ensure this mod is installed correctly!");
            if (orbQueue.Count > 0) orb = orbQueue.Dequeue();
            else
            {
                Debug.LogWarning("[Enrichments] No orbs left in the pool, new objects will be instantiated! This may negatively impact frames. Please consider using less enrichment mods at once if this bothers you.");
                orb = Instantiate(enrichmentPrefab).GetComponent<EnrichmentOrb>();
            }

            orb.transform.SetParent(null);
            orb.transform.position = position;
            orb.transform.rotation = rotation;
            orb.gameObject.SetActive(true);
            orb.rigidbody.WakeUp();
            orb.enrichmentMessage.gameObject.SetActive(true);
            orb.particleSystem.transform.localScale = Vector3.zero;
            orb.particleSystem.Play();
            return orb;
        }

        public static void Get(EnrichmentData enrichmentData, UIEnrichmentCore uiEnrichmentCore, Vector3 position, Quaternion rotation, Action<EnrichmentOrb> onSpawn)
        {
            EnrichmentOrb enrichmentOrb = Get(position, rotation);
            enrichmentOrb.uiEnrichmentCore = uiEnrichmentCore;
            enrichmentOrb.transform.SetPositionAndRotation(position, rotation);
            enrichmentOrb.rigidbody.velocity = Vector3.zero;
            enrichmentOrb.onSpawn?.Invoke(enrichmentOrb, EventTime.OnStart);
            enrichmentOrb.enrichmentData = enrichmentData;
            enrichmentOrb.onSpawn?.Invoke(enrichmentOrb, EventTime.OnEnd);
            enrichmentOrb.Active = true;
            enrichmentOrb.Spawn();
            enrichmentOrb.StartCoroutine(enrichmentOrb.LerpRoutine(true, null));
            onSpawn?.Invoke(enrichmentOrb);
        }

        public void Release()
        {
            StartCoroutine(LerpRoutine(false, () =>
            {
                Despawn();
                enrichmentMessage.gameObject.SetActive(false);
                onDespawn?.Invoke(this);
                transform.SetParent(enrichmentRoot.transform);
                handle.Release();
                rigidbody.Sleep();
                handle.ReleaseAllTkHandlers();
                gameObject.SetActive(false);
                orbQueue.Enqueue(this);
            }));
        }


        public IEnumerator LerpRoutine(bool active, Action onComplete)
        {
            Vector3 start = particleSystem.transform.localScale;
            Vector3 end = active ? Vector3.one : Vector3.zero;

            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                particleSystem.transform.localScale = Vector3.Lerp(start, end, t);
                yield return Yielders.EndOfFrame;
            }

            onComplete?.Invoke();
        }

        public void SetLayer(int layer) => gameObject.layer = layer;

        public void MoveTo(Vector3 position, Quaternion rotation, float maxForce = 1000)
        {
            if (handle.IsHanded()) return;

            Vector3 targetVelocity = (position - rigidbody.position) * 10f;
            rigidbody.velocity = Vector3.ClampMagnitude(Vector3.Lerp(rigidbody.velocity, targetVelocity, 0.2f), maxForce);

            Quaternion newRotation = Quaternion.RotateTowards(rigidbody.rotation, rotation, 360 * Time.fixedDeltaTime);
            rigidbody.MoveRotation(newRotation);
        }

        public void Update()
        {
            if (Player.currentCreature && spriteRenderer) spriteRenderer.transform.rotation = Quaternion.LookRotation(Player.local.head.cam.transform.forward, Vector3.up);
        }

        public void Spawn()
        {
            handle.Grabbed += OnGrabbed;
            handle.UnGrabbed += UnGrabbed;
            handle.OnHeldActionEvent += OnHeldActionEvent;
            enrichmentData.GetOrbIcon(sprite => spriteRenderer.sprite = sprite);
            enrichmentData.GetButtonIcon(EnrichmentManager.HasEnrichment(uiEnrichmentCore.heldItem, enrichmentData.id), sprite => enrichmentMessage.buttonRenderer.sprite = sprite);
            enrichmentData.GetVideo(clip => { enrichmentMessage.videoPlayer.clip = clip; });
            enrichmentMessage.titleText.text = enrichmentData.GetName();
            enrichmentMessage.descriptionText.text = enrichmentData.GetDescription();
        }

        public void Despawn()
        {
            if (handle != null)
            {
                handle.Grabbed -= OnGrabbed;
                handle.UnGrabbed -= UnGrabbed;
                handle.OnHeldActionEvent -= OnHeldActionEvent;
                enrichmentMessage.Hide();
                enrichmentMessage.transform.SetParent(null);
            }
        }

        private void OnGrabbed(RagdollHand ragdollHand, Handle handle, EventTime eventTime)
        {
            if (eventTime == EventTime.OnEnd)
            {
                enrichmentMessage.Enable();
                grabAudioSource.Play();
            }
        }

        private void UnGrabbed(RagdollHand ragdollHand, Handle handle1, EventTime eventTime)
        {
            if (eventTime == EventTime.OnEnd)
            {
                ungrabAudioSource.Play();
                enrichmentMessage.Hide();
            }
        }

        private void OnHeldActionEvent(RagdollHand ragdollHand, Interactable.Action action)
        {
            if (action != Interactable.Action.UseStart || !uiEnrichmentCore.heldItem) return;
            bool has = EnrichmentManager.HasEnrichment(uiEnrichmentCore.heldItem, enrichmentData.id);
            enrichmentData.GetButtonIcon(has, sprite => enrichmentMessage.buttonRenderer.sprite = sprite);
            if ((Player.characterData.inventory.GetCurrencyValue(Currency.CrystalShard) < enrichmentData.cost || EnrichmentManager.IsAtMaxEnrichments(uiEnrichmentCore.heldItem) || !enrichmentData.IsAllowedOnItem(uiEnrichmentCore.heldItem)) && !has)
            {
                Error();
                return;
            }

            if (has && !enrichmentData.allowRefund)
            {
                Error();
                return;
            }

            BuyOrRefund(Player.local.GetHand(ragdollHand.side), 1, hand =>
            {
                if (has) Refund();
                else Buy();
            });

            void Error() => Catalog.GetData<EffectData>("NotEnoughShards").Spawn(ragdollHand.transform).Play();

            void Buy()
            {
                StartCoroutine(SpawnShards(enrichmentData.cost, true));
                enrichmentMessage.buyEffect.Play();
                enableEffectData?.Spawn(transform).Play();
                Player.characterData.inventory.AddCurrencyValue(Currency.CrystalShard, -enrichmentData.cost);
                EnrichmentManager.AddEnrichment(uiEnrichmentCore.heldItem, enrichmentData);
                enrichmentData.GetButtonIcon(true, sprite => enrichmentMessage.buttonRenderer.sprite = sprite);
            }

            void Refund()
            {
                StartCoroutine(SpawnShards(enrichmentData.cost, false));
                disableEffectData?.Spawn(transform).Play();
                Player.characterData.inventory.AddCurrencyValue(Currency.CrystalShard, enrichmentData.cost);
                EnrichmentManager.RemoveEnrichment(uiEnrichmentCore.heldItem, enrichmentData.id);
                enrichmentData.GetButtonIcon(false, sprite => enrichmentMessage.buttonRenderer.sprite = sprite);
            }
        }

        public void BuyOrRefund(PlayerHand playerHand, float time, Action<PlayerHand> onComplete)
        {
            if (isRunning) return;
            StartCoroutine(EnableRoutine(playerHand, time, onComplete));
        }

        public IEnumerator SpawnShards(int count, bool bought)
        {
            ItemData shardItemData = Catalog.GetData<ItemData>(enrichmentData.shardId);
            Item[] items = new Item[count];
            for (int i = 0; i < count; ++i)
            {
                int j = i;
                shardItemData.SpawnAsync(item =>
                {
                    items[j] = item;
                    item.physicBody.useGravity = false;
                    foreach (ThunderBehaviour handle in item.handles) handle.gameObject.SetActive(false);
                    item.GetComponent<SkillTreeShard>().genericAttractionTarget = bought ? uiEnrichmentCore.transform : Player.currentCreature.ragdoll.targetPart.transform;
                }, !bought ? uiEnrichmentCore.transform.position : Player.currentCreature.ragdoll.targetPart.transform.position, Quaternion.identity);
                yield return Yielders.ForSeconds(0.25f);
            }
        }

        private IEnumerator EnableRoutine(PlayerHand playerHand, float time, Action<PlayerHand> onComplete)
        {
            isRunning = true;
            enableEffectInstance = enableLoopEffectData?.Spawn(transform);
            enableEffectInstance?.Play();
            float charge = 0f;
            while (playerHand.ragdollHand.grabbedHandle != null)
            {
                playerHand.controlHand.HapticLoop(this, charge, 0.01f);
                enableEffectInstance?.SetIntensity(charge);
                if (playerHand.controlHand.usePressed)
                {
                    if (time <= 0.0001f) time = 0.0001f;
                    charge += Time.deltaTime / time;
                    charge = Mathf.Clamp01(charge);
                    if (charge >= 1f)
                    {
                        enableEffectInstance?.End();
                        onComplete?.Invoke(playerHand);
                        playerHand.ragdollHand.HapticTick(10);
                        playerHand.controlHand.StopHapticLoop(this);
                        isRunning = false;
                        yield break;
                    }
                }
                else
                {
                    charge -= Time.deltaTime / (time * 2f);
                    charge = Mathf.Clamp01(charge);
                    if (Mathf.Approximately(charge, 0f))
                    {
                        enableEffectInstance?.End();
                        playerHand.controlHand.StopHapticLoop(this);
                        isRunning = false;
                        yield break;
                    }
                }

                yield return Yielders.EndOfFrame;
            }

            enableEffectInstance?.End();
            playerHand.controlHand.StopHapticLoop(this);
            isRunning = false;
        }
    }
}