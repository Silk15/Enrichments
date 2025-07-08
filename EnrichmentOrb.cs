using System;
using System.Collections;
using System.Collections.Generic;
using Enrichments;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using UnityEngine;
using UnityEngine.Serialization;

namespace Enrichments
{
    public class EnrichmentOrb : ThunderBehaviour
    {
        private static GameObject enrichmentPrefab;
        public SpriteRenderer spriteRenderer;
        public AudioSource grabAudioSource;
        public AudioSource ungrabAudioSource;
        public string orbEffectId;
        public Rigidbody rigidbody;
        public Handle handle;
        public EffectInstance orbEffectInstance;
        public EffectData orbEffectData;
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

        public static void Get(EnrichmentData enrichmentData, UIEnrichmentCore uiEnrichmentCore, Vector3 position, Quaternion rotation, Action<EnrichmentOrb> onSpawn)
        {
            GameManager.local.StartCoroutine(GetRoutine());
            IEnumerator GetRoutine()
            {
                EnrichmentOrb enrichmentOrb;
                if (enrichmentPrefab == null) yield return Catalog.LoadAssetCoroutine<GameObject>("Silk.Prefab.Enrichments.Orb", gameObject => { enrichmentPrefab = gameObject; }, "Silk.Prefab.Enrichments.Orb");
                GameObject prefab = Instantiate(enrichmentPrefab, position, rotation);
                if (prefab.TryGetComponent(out enrichmentOrb)) { enrichmentOrb.orbEffectData = Catalog.GetData<EffectData>(enrichmentData.orbEffectId); }
                else
                {
                    Debug.Log($"[Enrichments] Failed to load prefab, gameObject does not contain an [{nameof(EnrichmentOrb)}] component..");
                    yield break;
                }
                enrichmentOrb.uiEnrichmentCore = uiEnrichmentCore;
                enrichmentOrb.enableEffectData = Catalog.GetData<EffectData>(enrichmentOrb.enableEffectId);
                enrichmentOrb.disableEffectData = Catalog.GetData<EffectData>(enrichmentOrb.disableEffectId);
                enrichmentOrb.enableLoopEffectData = Catalog.GetData<EffectData>(enrichmentOrb.enableLoopEffectId);
                enrichmentOrb.transform.SetPositionAndRotation(position, rotation);
                enrichmentOrb.rigidbody.velocity = Vector3.zero;
                enrichmentOrb.onSpawn?.Invoke(enrichmentOrb, EventTime.OnStart);
                enrichmentOrb.enrichmentData = enrichmentData;
                enrichmentOrb.onSpawn?.Invoke(enrichmentOrb, EventTime.OnEnd);
                EnrichmentMessage.Create(enrichmentOrb, message =>
                {
                    enrichmentOrb.enrichmentMessage = message;
                    enrichmentOrb.Active = true;
                    enrichmentOrb.Spawn();
                });
                enrichmentOrb.StartCoroutine(enrichmentOrb.LerpRoutine(true, null));
                onSpawn?.Invoke(enrichmentOrb);
            }
        }
        
        public static EnrichmentOrb Release(EnrichmentOrb enrichmentOrb)
        {
            enrichmentOrb.onDespawn?.Invoke(enrichmentOrb);
            enrichmentOrb.StartCoroutine(enrichmentOrb.LerpRoutine(false, () =>
            {
                enrichmentOrb.orbEffectInstance.End();
                enrichmentOrb.orbEffectInstance.SetParent(null);
                enrichmentOrb.Despawn();
                enrichmentOrb.Active = false;
                enrichmentOrb.handle.Release();
                enrichmentOrb.enrichmentMessage.enrichmentOrb = null;
                Destroy(enrichmentOrb.gameObject);
            }));
            return enrichmentOrb;
        }

        public IEnumerator LerpRoutine(bool active, Action onComplete)
        {
            if (active)
            {
                yield return Yielders.ForSeconds(0.25f);
                orbEffectInstance = orbEffectData.Spawn(transform);
                orbEffectInstance.Play();
                orbEffectInstance.SetIntensity(0);
            }

            float targetValue = active ? 1 : 0;
            float elapsed = 0f;
            float total = 1f;
            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                SetIntensity(Mathf.Lerp(GetIntensity(), targetValue, elapsed / total));
                yield return Yielders.EndOfFrame;
            }

            onComplete?.Invoke();
        }

        public void SetLayer(int layer) => gameObject.layer = layer;

        public void SetIntensity(float intensity) => orbEffectInstance?.SetIntensity(intensity);

        public float GetIntensity()
        {
            if (orbEffectInstance.effects == null || orbEffectInstance.effects.Count <= 0) return 0;
            return orbEffectInstance?.effects[0]?.effectIntensity ?? 0;
        }

        public void MoveTo(Vector3 position, Quaternion rotation, float maxForce = 1000)
        {
            if (handle.IsHanded()) return;

            Vector3 targetVelocity = (position - rigidbody.position) * 10f;
            rigidbody.velocity = Vector3.ClampMagnitude(Vector3.Lerp(rigidbody.velocity, targetVelocity, 0.2f), maxForce);

            Quaternion newRotation = Quaternion.RotateTowards(rigidbody.rotation, rotation, 360 * Time.fixedDeltaTime);
            rigidbody.MoveRotation(newRotation);
        }

        public void Update() => spriteRenderer.transform.rotation = Quaternion.LookRotation(Player.local.head.cam.transform.forward, Vector3.up);

        public void Spawn()
        {
            orbEffectInstance.SetColorImmediate(enrichmentData.primarySkillTree.emissionColor);
            handle.Grabbed += OnGrabbed;
            handle.UnGrabbed += UnGrabbed;
            handle.OnHeldActionEvent += OnHeldActionEvent;
            enrichmentData.GetOrbIcon(sprite => spriteRenderer.sprite = sprite); 
            enrichmentData.GetButtonIcon(EnrichmentManager.HasEnrichment(uiEnrichmentCore.heldItem, enrichmentData.id), sprite => enrichmentMessage.buttonRenderer.sprite = sprite);
            enrichmentData.GetVideo(clip => enrichmentMessage.videoPlayer.clip = clip);
            enrichmentMessage.titleText.text = enrichmentData.GetName();
            enrichmentMessage.descriptionText.text = enrichmentData.GetDescription();
        }

        public void Despawn()
        {
            if (handle != null && orbEffectInstance != null)
            {
                handle.Grabbed -= OnGrabbed;
                handle.UnGrabbed -= UnGrabbed;
                handle.OnHeldActionEvent -= OnHeldActionEvent;
                enrichmentMessage.Hide();
                enrichmentMessage.transform.SetParent(null);
                orbEffectInstance.SetColorImmediate(Color.white);
                orbEffectInstance.SetIntensity(0f);
                orbEffectInstance.End();
                orbEffectInstance.Despawn();
                orbEffectInstance.SetParent(null);
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
            if ((Player.characterData.inventory.GetCurrencyValue(Currency.CrystalShard) < enrichmentData.cost || EnrichmentManager.IsAtMaxEnrichments(uiEnrichmentCore.heldItem) || !enrichmentData.IsAllowedOnItem(uiEnrichmentCore.heldItem)) && !has)
            {
                Error();
                return;
            }
            
            BuyOrRefund(Player.local.GetHand(ragdollHand.side), 1, hand =>
            {
                enrichmentData.GetButtonIcon(has, sprite => enrichmentMessage.buttonRenderer.sprite = sprite);
                if (has) Refund();
                else Buy();
            });

            void Error()  => Catalog.GetData<EffectData>("NotEnoughShards").Spawn(ragdollHand.transform).Play();

            void Buy()
            {
                StartCoroutine(SpawnShards(enrichmentData.cost, true));
                enrichmentMessage.buyEffect.Play();
                enableEffectData?.Spawn(transform).Play();
                Player.characterData.inventory.AddCurrencyValue(Currency.CrystalShard, -enrichmentData.cost);
                EnrichmentManager.AddEnrichment(uiEnrichmentCore.heldItem, enrichmentData);
            }

            void Refund()
            {
                StartCoroutine(SpawnShards(enrichmentData.cost, false)); 
                disableEffectData?.Spawn(transform).Play();
                Player.characterData.inventory.AddCurrencyValue(Currency.CrystalShard, enrichmentData.cost);
                EnrichmentManager.RemoveEnrichment(uiEnrichmentCore.heldItem, enrichmentData.id);
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