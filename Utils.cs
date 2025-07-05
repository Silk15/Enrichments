using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ThunderRoad;
using UnityEngine;
using Random = UnityEngine.Random;

public static class Utils
{
    private static Updater local;
    public static Updater Update() => local ?? (local = new GameObject().AddComponent<Updater>());
    
    public static bool Validate(Func<bool> predicate, Action onTrue = null, Action onFalse = null)
    {
        if (predicate())
        {
            onTrue?.Invoke();
            return true;
        }
        onFalse?.Invoke();
        return false;
    }

    public static void RunOnValid<T>(IEnumerable<T> collection, Func<T, bool> predicate, Action<T> onConditionMet)
    {
        foreach (var item in collection)
            if (predicate(item))
                onConditionMet(item);
    }

    public static T Retry<T>(Func<T> func, int retries, Func<Exception, bool> shouldRetry = null)
    {
        for (int i = 0; i < retries; i++)
        {
            try { return func(); }
            catch (Exception ex)
            {
                if (i == retries - 1 || (shouldRetry != null && !shouldRetry(ex))) throw;
            }
        }
        return default;
    }
    
    public static void DoWhen(Func<bool> predicate, Action action, int maxIterations = 1)
    {
        int i = 0;
        while (predicate() && i++ < maxIterations) action();
    }

    
    public static T Tap<T>(T value, Action<T> sideEffect)
    {
        sideEffect(value);
        return value;
    }
    
    public static T TryOrDefault<T>(Func<T> func, T fallback = default)
    {
        try { return func(); }
        catch { return fallback; }
    }
    
    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source) action(item);
    }

    public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int size)
    {
        if (size <= 0) throw new ArgumentException("Batch size must be positive.");
    
        var batch = new List<T>(size);
        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count == size)
            {
                yield return batch;
                batch = new List<T>(size);
            }
        }
        if (batch.Count > 0) yield return batch;
    }

    public static Dictionary<TKey, TValue> ToDictionarySafe<T, TKey, TValue>(this IEnumerable<T> source, Func<T, TKey> keySelector, Func<T, TValue> valueSelector)
    {
        var dict = new Dictionary<TKey, TValue>();
        foreach (var item in source)
        {
            var key = keySelector(item);
            if (!dict.ContainsKey(key)) dict[key] = valueSelector(item);
        }
        return dict;
    }

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T> source) where T : class  => source.Where(item => item != null)!;

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : struct => source.Where(item => item.HasValue).Select(item => item.Value);


    public static List<ThunderEntity> AddExplosionForceInRadius(Vector3 position, float radius, float force, float upwardsModifier, ForceMode forceMode, Func<ThunderEntity, bool> filter)
    {
        var alloc = ThunderEntity.InRadius(position, radius, filter);
        foreach (ThunderEntity entity in alloc) entity.AddExplosionForce(force, position, radius, upwardsModifier, forceMode);
        return alloc;
    }
    
    public static void AddForce(this Player player, Vector3 direction, float speed)
    {
        Player.local.locomotion.velocity = direction.normalized * Player.local.locomotion.velocity.magnitude;
        Player.local.locomotion.physicBody.AddForce(direction.normalized * speed, ForceMode.VelocityChange);
    }
    
    public static Vector3 GetRandomDirectionInCone(Vector3 forward, float angleDegrees)
    {
        float angleRadians = angleDegrees * Mathf.Deg2Rad;
        float z = Mathf.Cos(angleRadians * Random.value);
        float sinT = Mathf.Sqrt(1 - z * z);
        float phi = Random.Range(0, 2 * Mathf.PI);
        float x = sinT * Mathf.Cos(phi);
        float y = sinT * Mathf.Sin(phi);
        Vector3 localDirection = new Vector3(x, y, z);
        return Quaternion.FromToRotation(Vector3.forward, forward) * localDirection;
    }

    public static Vector3 GenerateOffsetVector(Vector3 origin, Vector2 minMaxX, Vector2 minMaxZ, float minDistFromOrigin, float yOffset, bool log = false)
    {
        float xOffset, zOffset;
        do
        {
            xOffset = Random.Range(minMaxX.x, minMaxX.y);
            zOffset = Random.Range(minMaxZ.x, minMaxZ.y);
        } while (Mathf.Sqrt(xOffset * xOffset + zOffset * zOffset) < minDistFromOrigin);

        var targetPosition = new Vector3(origin.x + xOffset, origin.y + yOffset, origin.z + zOffset);
        if (log) Debug.Log($"X Offset: {xOffset}, Z Offset: {zOffset}");
        return targetPosition;
    }

    public static Vector3 GenerateVectorBasedOn(Transform origin, Vector2 minMaxX, Vector2 minMaxZ, float yOffset, bool log = false)
    {
        var playerForward = origin.forward;
        var playerRight = origin.right;
        var forwardOffset = Random.Range(minMaxZ.x, minMaxZ.y);
        var lateralOffset = Random.Range(minMaxX.x, minMaxX.y);
        var offset = playerForward * forwardOffset + playerRight * lateralOffset;
        var targetPosition = origin.position + offset;
        if (log) Debug.Log($"Forward Offset: {forwardOffset}, Lateral Offset: {lateralOffset}");
        return new Vector3(targetPosition.x, origin.position.y * yOffset, targetPosition.z);
    }

    public static List<Vector3> GetListedPointsOnHemisphere(Vector3 origin, Vector3 direction, Vector2 minMaxCount, float exclusionRadius, float offsetFromOrigin)
    {
        var pointsList = new List<Vector3>();
        var pointsCount = Random.Range(Mathf.FloorToInt(minMaxCount.x), Mathf.FloorToInt(minMaxCount.y) + 1);
        for (var i = 0; i < pointsCount; i++)
        {
            Vector3 randomDirection;
            do
            {
                randomDirection = Random.onUnitSphere;
            } while (Vector3.Dot(randomDirection, direction) < Mathf.Cos(exclusionRadius * Mathf.Deg2Rad));

            randomDirection = origin + randomDirection * offsetFromOrigin;
            pointsList.Add(randomDirection);
        }

        return pointsList;
    }

    public static Vector3 GetDeflectNormal(float beamLength, Vector3 defaultNormal, Transform origin, LayerMask mask)
    {
        var raycastHitList = new List<RaycastHit>();
        for (var index = 0; index < 3; ++index)
        {
            RaycastHit hit;
            if (Raycast(Quaternion.AngleAxis(120f * index, Vector3.forward) * Vector3.up * 0.01f, origin, mask, beamLength, out hit)) raycastHitList.Add(hit);
        }

        switch (raycastHitList.Count)
        {
            case 0:
                return defaultNormal;
            case 3:
                var plane = new Plane();
                ref var local = ref plane;
                var raycastHit = raycastHitList[0];
                var point1 = raycastHit.point;
                raycastHit = raycastHitList[1];
                var point2 = raycastHit.point;
                raycastHit = raycastHitList[2];
                var point3 = raycastHit.point;
                local = new Plane(point1, point2, point3);
                if (Vector3.Dot(plane.normal, origin.forward) > 0.0) plane.Flip();
                return plane.normal;
            default:
                var vector3 = defaultNormal;
                for (var index = 0; index < raycastHitList.Count; ++index) vector3 += raycastHitList[index].normal;
                return vector3 / (raycastHitList.Count + 1);
        }
    }

    public static bool Raycast(Vector3 offset, Transform origin, LayerMask mask, float distance, out RaycastHit hit)
    {
        return Physics.Raycast(origin.position + origin.TransformPoint(offset), origin.forward, out hit, distance, mask, QueryTriggerInteraction.Ignore);
    }

    public static ConfigurableJoint CreateConfigurableJoint(Rigidbody source, Rigidbody target, float spring, float damper, float minDistance, float maxDistance, float massScale, bool enableCollision = true, ConfigurableJointMotion motion = ConfigurableJointMotion.Free)
    {
        var configurableJoint = target.gameObject.AddComponent<ConfigurableJoint>();
        configurableJoint.connectedBody = source;
        configurableJoint.enableCollision = enableCollision;
        var linearLimit = new SoftJointLimit { contactDistance = minDistance, limit = maxDistance };
        configurableJoint.linearLimit = linearLimit;
        var drive = new JointDrive { positionSpring = spring, positionDamper = damper, maximumForce = Mathf.Infinity };
        configurableJoint.xDrive = drive;
        configurableJoint.yDrive = drive;
        configurableJoint.zDrive = drive;
        configurableJoint.massScale = massScale;
        configurableJoint.angularXMotion = ConfigurableJointMotion.Free;
        configurableJoint.angularYMotion = ConfigurableJointMotion.Free;
        configurableJoint.angularZMotion = ConfigurableJointMotion.Free;

        return configurableJoint;
    }
    
    public class Updater : ThunderBehaviour
    {
        public delegate void UpdateDelegate();
        public event UpdateDelegate onManagedUpdate;
        public event UpdateDelegate onManagedLateUpdate;
        public event UpdateDelegate onManagedFixedUpdate;

        protected override void ManagedUpdate()
        {
            base.ManagedUpdate();
            onManagedUpdate?.Invoke();
        }

        protected override void ManagedLateUpdate()
        {
            base.ManagedLateUpdate();
            onManagedLateUpdate?.Invoke();
        }

        protected override void ManagedFixedUpdate()
        {
            base.ManagedFixedUpdate();
            onManagedFixedUpdate?.Invoke();
        }

        public override ManagedLoops EnabledManagedLoops => ManagedLoops.Update | ManagedLoops.LateUpdate | ManagedLoops.FixedUpdate;
    }
}

public static class Extensions
{
    public static bool TryGetMaterialData(this PhysicMaterial physicMaterial, out MaterialData material)
    {
        material = null;
        List<CatalogData> dataList = Catalog.GetDataList(Category.Material);
        int count = dataList.Count;
        for (int index = 0; index < count; ++index)
        {
            MaterialData materialData = (MaterialData)dataList[index];
            if (materialData.physicMaterialHash == Animator.StringToHash(physicMaterial.name)) material = materialData;
            if (material != null) return true;
        }
        return false;
    }
    
    public static MaterialData GetMaterialData(this PhysicMaterial physicMaterial)
    {
        List<CatalogData> dataList = Catalog.GetDataList(Category.Material);
        int count = dataList.Count;
        for (int index = 0; index < count; ++index)
        {
            MaterialData materialData = (MaterialData)dataList[index];
            if (materialData.physicMaterialHash == Animator.StringToHash(physicMaterial.name))
                return materialData;
        }
        return null;
    }
}

public static class TransformExtensions
{
    private static bool IsInColliderInternal(this Vector3 position, float allowance)
    {
        return Physics.CheckSphere(position, allowance);
    }

    private static T[] GetComponentsInternal<T>(GameObject gameObject) where T : Component
    {
        return gameObject.GetComponents<T>();
    }

    private static T GetComponentInParentInternal<T>(GameObject gameObject) where T : Component
    {
        return gameObject.GetComponentInParent<T>();
    }

    private static T[] GetComponentsInParentInternal<T>(GameObject gameObject) where T : Component
    {
        return gameObject.GetComponentsInParent<T>();
    }

    private static T GetComponentInChildrenInternal<T>(GameObject gameObject) where T : Component
    {
        return gameObject.GetComponentInChildren<T>();
    }

    private static T[] GetComponentsInChildrenInternal<T>(GameObject gameObject) where T : Component
    {
        return gameObject.GetComponentsInChildren<T>();
    }

    private static List<T> GetComponentsInImmediateChildrenInternal<T>(Transform origin) where T : Component
    {
        var components = new List<T>();
        foreach (Transform child in origin)
        {
            var component = child.GetComponent<T>();
            if (component != null) components.Add(component);
        }

        return components;
    }

    private static T GetComponentInImmediateChildrenInternal<T>(Transform origin) where T : Component
    {
        foreach (Transform child in origin)
        {
            var component = child.GetComponent<T>();
            if (component != null) return component;
        }

        return null;
    }

    private static List<T> GetComponentsInImmediateParentInternal<T>(Transform origin) where T : Component
    {
        var components = new List<T>();
        if (origin.parent != null)
            components.AddRange(origin.parent.GetComponents<T>());
        return components;
    }

    private static T GetComponentInImmediateParentInternal<T>(Transform origin) where T : Component
    {
        return origin.parent != null ? origin.parent.GetComponent<T>() : null;
    }

    private static Transform GetChildByNameRecursiveInternal(Transform parent, string nameToCheck)
    {
        foreach (Transform child in parent)
        {
            if (child.name == nameToCheck) return child;
            var found = GetChildByNameRecursiveInternal(child, nameToCheck);
            if (found != null) return found;
        }

        return null;
    }

    private static List<Transform> GetChildrenByNameRecursiveInternal(Transform parent, string nameToCheck)
    {
        var result = new List<Transform>();
        foreach (Transform child in parent)
        {
            if (child.name == nameToCheck) result.Add(child);
            result.AddRange(GetChildrenByNameRecursiveInternal(child, nameToCheck));
        }

        return result;
    }

    private static Transform GetMatchingChildInternal(Transform origin, string keyword)
    {
        foreach (Transform child in origin)
            if (child.name.Contains(keyword))
                return child;
        return null;
    }

    private static List<Transform> GetMatchingChildrenInternal(Transform origin, string keyword)
    {
        var result = new List<Transform>();
        foreach (Transform child in origin)
            if (child.name.Contains(keyword))
                result.Add(child);
        return result;
    }

    public static bool IsInCollider(this GameObject gameObject, float allowance) => IsInColliderInternal(gameObject.transform.position, allowance);

    public static bool IsInCollider(this Transform transform, float allowance) => IsInColliderInternal(transform.position, allowance);

    public static bool IsInCollider(this Component component, float allowance) => IsInColliderInternal(component.transform.position, allowance);

    public static bool TryGetComponents<T>(this GameObject go, out T[] components) where T : Component
    {
        components = GetComponentsInternal<T>(go);
        return components.Length > 0;
    }

    public static bool TryGetComponents<T>(this Component comp, out T[] components) where T : Component => comp.gameObject.TryGetComponents(out components);

    public static bool TryGetComponents<T>(this Transform tf, out T[] components) where T : Component => tf.gameObject.TryGetComponents(out components);

    public static bool TryGetComponentInParent<T>(this GameObject go, out T component) where T : Component
    {
        component = GetComponentInParentInternal<T>(go);
        return component != null;
    }

    public static bool TryGetComponentInParent<T>(this Component comp, out T component) where T : Component => comp.gameObject.TryGetComponentInParent(out component);

    public static bool TryGetComponentInParent<T>(this Transform tf, out T component) where T : Component => tf.gameObject.TryGetComponentInParent(out component);

    public static bool TryGetComponentsInParent<T>(this GameObject go, out T[] components) where T : Component
    {
        components = GetComponentsInParentInternal<T>(go);
        return components.Length > 0;
    }

    public static bool TryGetComponentsInParent<T>(this Component comp, out T[] components) where T : Component => comp.gameObject.TryGetComponentsInParent(out components);

    public static bool TryGetComponentsInParent<T>(this Transform tf, out T[] components) where T : Component => tf.gameObject.TryGetComponentsInParent(out components);

    public static bool TryGetComponentInChildren<T>(this GameObject go, out T component) where T : Component
    {
        component = GetComponentInChildrenInternal<T>(go);
        return component != null;
    }

    public static bool TryGetComponentInChildren<T>(this Component comp, out T component) where T : Component => comp.gameObject.TryGetComponentInChildren(out component);

    public static bool TryGetComponentInChildren<T>(this Transform tf, out T component) where T : Component => tf.gameObject.TryGetComponentInChildren(out component);

    public static bool TryGetComponentsInChildren<T>(this GameObject go, out T[] components) where T : Component
    {
        components = GetComponentsInChildrenInternal<T>(go);
        return components.Length > 0;
    }

    public static bool TryGetComponentsInChildren<T>(this Component comp, out T[] components) where T : Component => comp.gameObject.TryGetComponentsInChildren(out components);

    public static bool TryGetComponentsInChildren<T>(this Transform tf, out T[] components) where T : Component => tf.gameObject.TryGetComponentsInChildren(out components);

    public static List<T> GetComponentsInImmediateChildren<T>(this Transform tf) where T : Component => GetComponentsInImmediateChildrenInternal<T>(tf);

    public static List<T> GetComponentsInImmediateChildren<T>(this GameObject go) where T : Component => GetComponentsInImmediateChildrenInternal<T>(go.transform);

    public static List<T> GetComponentsInImmediateChildren<T>(this Component comp) where T : Component => GetComponentsInImmediateChildrenInternal<T>(comp.transform);

    public static T GetComponentInImmediateChildren<T>(this Transform tf) where T : Component => GetComponentInImmediateChildrenInternal<T>(tf);

    public static T GetComponentInImmediateChildren<T>(this GameObject go) where T : Component => GetComponentInImmediateChildrenInternal<T>(go.transform);

    public static T GetComponentInImmediateChildren<T>(this Component comp) where T : Component => GetComponentInImmediateChildrenInternal<T>(comp.transform);

    public static List<T> GetComponentsInImmediateParent<T>(this Transform tf) where T : Component => GetComponentsInImmediateParentInternal<T>(tf);

    public static List<T> GetComponentsInImmediateParent<T>(this GameObject go) where T : Component => GetComponentsInImmediateParentInternal<T>(go.transform);

    public static List<T> GetComponentsInImmediateParent<T>(this Component comp) where T : Component => GetComponentsInImmediateParentInternal<T>(comp.transform);

    public static T GetComponentInImmediateParent<T>(this Transform tf) where T : Component => GetComponentInImmediateParentInternal<T>(tf);

    public static T GetComponentInImmediateParent<T>(this GameObject go) where T : Component => GetComponentInImmediateParentInternal<T>(go.transform);

    public static T GetComponentInImmediateParent<T>(this Component comp) where T : Component => GetComponentInImmediateParentInternal<T>(comp.transform);

    public static Transform GetChildByNameRecursive(this Transform tf, string nameToCheck) => GetChildByNameRecursiveInternal(tf, nameToCheck);

    public static Transform GetChildByNameRecursive(this GameObject go, string nameToCheck) => GetChildByNameRecursiveInternal(go.transform, nameToCheck);

    public static Transform GetChildByNameRecursive(this Component comp, string nameToCheck) => GetChildByNameRecursiveInternal(comp.transform, nameToCheck);

    public static List<Transform> GetChildrenByNameRecursive(this Transform tf, string nameToCheck) => GetChildrenByNameRecursiveInternal(tf, nameToCheck);

    public static List<Transform> GetChildrenByNameRecursive(this GameObject go, string nameToCheck) => GetChildrenByNameRecursiveInternal(go.transform, nameToCheck);

    public static List<Transform> GetChildrenByNameRecursive(this Component comp, string nameToCheck) => GetChildrenByNameRecursiveInternal(comp.transform, nameToCheck);

    public static Transform GetMatchingChild(this Transform tf, string keyword) => GetMatchingChildInternal(tf, keyword);

    public static Transform GetMatchingChild(this GameObject go, string keyword) => GetMatchingChildInternal(go.transform, keyword);

    public static Transform GetMatchingChild(this Component comp, string keyword) => GetMatchingChildInternal(comp.transform, keyword);

    public static List<Transform> GetMatchingChildren(this Transform tf, string keyword) => GetMatchingChildrenInternal(tf, keyword);

    public static List<Transform> GetMatchingChildren(this GameObject go, string keyword) => GetMatchingChildrenInternal(go.transform, keyword);

    public static List<Transform> GetMatchingChildren(this Component comp, string keyword) => GetMatchingChildrenInternal(comp.transform, keyword);

    public static void SmoothLookAt(this Transform transform, Transform target, float duration) => GameManager.local.StartCoroutine(SmoothLookRoutine(transform, target, duration));


    private static IEnumerator SmoothLookRoutine(Transform transform, Transform target, float duration)
    {
        var timeElapsed = 0f;
        var initialRotation = transform.rotation;
        var targetRotation = Quaternion.LookRotation(target.position - transform.position);
        while (timeElapsed < duration)
        {
            transform.rotation = Quaternion.Slerp(initialRotation, targetRotation, timeElapsed / duration);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRotation;
    }
}

public static class ThunderEntityExtensions
{
    public static void Despawn(this ThunderEntity entity, float time)
    {
        entity.RunAfter(() => { entity.Despawn(); }, time);
    }
}

public static class CreatureExtensions
{
    public static RagdollPart GetClosestPart(this Ragdoll ragdoll, Vector3 position, float maxDistance, out RagdollPart ragdollPart)
    {
        ragdollPart = null;
        var closestDistance = maxDistance;

        foreach (var part in ragdoll.parts)
        {
            var distance = Vector3.Distance(part.transform.position, position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                ragdollPart = part;
            }
        }

        return ragdollPart;
    }

    public static bool Active(this Creature creature)
    {
        return !creature.isKilled && !creature.isCulled;
    }

    public static void Disarm(this Creature creature)
    {
        creature.handLeft.TryRelease();
        creature.handRight.TryRelease();
    }

    public static Creature GetClosest(this Creature creature, float maxDistance)
    {
        Creature closestCreature = null;
        var lastCreatureDist = Mathf.Infinity;
        foreach (var creature1 in Creature.allActive)
        {
            if (creature.isPlayer) continue;
            var creatureDist = Vector3.Distance(creature.ragdoll.targetPart.transform.position, creature.transform.position);
            if (creatureDist < lastCreatureDist && creatureDist <= maxDistance)
            {
                closestCreature = creature1;
                lastCreatureDist = creatureDist;
            }
        }

        return closestCreature;
    }

    public static bool IsMetal(this MaterialData materialData) 
    {
        if (!Creature.meshRaycast || materialData == null) return false;
        return materialData.isMetal;
    }
    
    public static void Shred(this Creature creature)
    {
        creature.Kill();
        for (var index = creature.ragdoll.parts.Count - 1; index >= 0; --index)
        {
            var part = creature.ragdoll.parts[index];
            if (creature.ragdoll.rootPart != part && part.sliceAllowed) part.TrySlice();
        }
    }

    public static void Slice(this RagdollPart ragdollPart)
    {
        if (ragdollPart.sliceAllowed && ragdollPart) ragdollPart.TrySlice();
    }
}

public static class ItemExtensions
{
    public static T GetOrAddCustomData<T>(this Item item) where T : ContentCustomData, new()
    {
        if (item.TryGetCustomData(out T customData)) return customData;
        var newData = new T();
        item.AddCustomData(newData);
        return newData;
    }

    
    public static UnityEngine.Vector3 ForwardVector(this Item item)
    {
        if (item.flyDirRef) return item.flyDirRef.forward;
        return item.holderPoint ? item.transform.rotation * Quaternion.Inverse(item.transform.InverseTransformRotation(item.holderPoint.rotation * Quaternion.AngleAxis(180f, Vector3.up))) * Vector3.forward : item.transform.up;
    }
    
    public static bool Has(this List<Imbue> imbues, string id)
    {
        if (imbues.IsNullOrEmpty()) return false;
        return imbues.Any(imbue => imbue.spellCastBase != null && imbue.spellCastBase.id == id);
    }

    public static Imbue Get(this List<Imbue> imbues, string id)
    {
        if (imbues.IsNullOrEmpty()) return null;
        return imbues.FirstOrDefault(imbue => imbue.spellCastBase != null && imbue.spellCastBase.id == id);
    }

    public static bool CanImbue(this ColliderGroup colliderGroup)
    {
        return colliderGroup.modifier.imbueType != ColliderGroupData.ImbueType.None && colliderGroup.allowImbueEffect && colliderGroup.imbueCustomSpellID.IsNullOrEmptyOrWhitespace();
    }

    public static List<ColliderGroup> WithImbue(this List<ColliderGroup> colliderGroups, string id)
    {
        List<ColliderGroup> result = new List<ColliderGroup>();
        for (var i = 0; i < colliderGroups.Count; ++i)
        {
            var colliderGroup = colliderGroups[i];
            if (colliderGroup.imbue.spellCastBase.id == id) result.Add(colliderGroup);
        }
        return result;
    }

    public static List<Damager> Damagers(this Item item)
    {
        return item.GetComponentsInChildren<Damager>().ToList();
    }

    public static List<Damager> OfType(this List<Damager> damagers, DamageType damageType)
    {
        var result = new List<Damager>();
        switch (damageType)
        {
            case DamageType.Blunt:
                result.AddRange(damagers.Where(d => Mathf.Approximately(d.penetrationDepth, 0) && Mathf.Approximately(d.penetrationLength, 0)));
                break;
            case DamageType.Pierce:
                result.AddRange(damagers.Where(d => d.penetrationDepth > 0 && Mathf.Approximately(d.penetrationLength, 0)));
                break;
            case DamageType.Slash:
                result.AddRange(damagers.Where(d => d.penetrationDepth > 0 && d.penetrationLength > 0));
                break;
        }

        return result;
    }

    public static void PlayHapticClip(this Item item, AnimationCurve curve, float time)
    {
        foreach (var hand in item.handlers) hand.PlayHapticClipOver(curve, time);
    }

    public static void PointItemFlyRefAtTarget(this Item item, Vector3 target, float lerpFactor, Vector3? upDir = null)
    {
        var up = upDir ?? Vector3.up;
        if (item.flyDirRef)
        {
            item.transform.rotation = Quaternion.Slerp(item.transform.rotation * item.flyDirRef.localRotation, Quaternion.LookRotation(target, up), lerpFactor) * Quaternion.Inverse(item.flyDirRef.localRotation);
        }
        else if (item.holderPoint)
        {
            item.transform.rotation = Quaternion.Slerp(item.transform.rotation * item.holderPoint.localRotation, Quaternion.LookRotation(target, up), lerpFactor) * Quaternion.Inverse(item.holderPoint.localRotation);
        }
        else
        {
            var pointDir = Quaternion.LookRotation(item.transform.up, up);
            item.transform.rotation = Quaternion.Slerp(item.transform.rotation * pointDir, Quaternion.LookRotation(target, up), lerpFactor) * Quaternion.Inverse(pointDir);
        }
    }

    public static Quaternion GetFlyDirRefLocalRotation(this Item item)
    {
        return Quaternion.Inverse(item.transform.rotation) * item.flyDirRef.rotation;
    }

    public static void IgnoreCollider(this Item item, Collider collider, bool ignore)
    {
        foreach (var group in item.colliderGroups)
        foreach (var itemCollider in group.colliders)
            Physics.IgnoreCollision(collider, itemCollider, ignore);
    }

    public static Collider FurthestCollider(this ColliderGroup colliderGroup, Vector3 point)
    {
        Collider result = null;
        foreach (var collider in colliderGroup.colliders)
            if (result == null || Vector3.Distance(collider.transform.position, point) > Vector3.Distance(result.transform.position, point))
                result = collider;
        return result;
    }

    public static Collider ClosestCollider(this ColliderGroup colliderGroup, Vector3 point)
    {
        Collider result = null;
        foreach (var collider in colliderGroup.colliders)
            if (result == null || Vector3.Distance(collider.transform.position, point) < Vector3.Distance(result.transform.position, point))
                result = collider;
        return result;
    }
}

public static class ReflectionExtensions
{
    public static object GetField(this object obj, string fieldName, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
    {
        var field = obj.GetType().GetField(fieldName, flags);
        if (field != null) return field.GetValue(obj);
        return null;
    }

    public static object SetField(this object obj, string fieldName, object value, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
    {
        var field = obj.GetType().GetField(fieldName, flags);
        if (field != null) field.SetValue(obj, value);
        return obj;
    }

    public static MethodInfo GetMethod(this object obj, string methodName, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
    {
        var method = obj.GetType().GetMethod(methodName, flags);
        if (method != null) return method;
        return null;
    }

    public static void InvokeMethod(this object obj, string methodName, BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
    {
        if (obj == null) return;
        var method = obj.GetType().GetMethod(methodName, flags);
        method?.Invoke(obj, null);
    }
}

public static class EffectInstanceExtensions
{
    public static void SetColorImmediate(this EffectInstance effectInstance, Color color)
    {
        var particleSystems = effectInstance.GetParticleSystems();
        foreach (var particleSystem in particleSystems)
        {
            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var minMaxGradient = colorOverLifetime.color;
            minMaxGradient.color = color;
            colorOverLifetime.color = color;
        }
    }

    public static void LerpIntensity(this EffectInstance effectInstance, float a, float b, float duration, bool endOnFinish)
    {
        GameManager.local.StartCoroutine(FadeIntensity(effectInstance, a, b, duration, endOnFinish));
    }

    private static IEnumerator FadeIntensity(EffectInstance effectInstance, float a, float b, float duration, bool endOnFinished)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            var intensity = Mathf.Lerp(a, b, elapsed / duration);
            effectInstance.SetIntensity(intensity);
            elapsed += Time.deltaTime;
            yield return null;
        }

        effectInstance.SetIntensity(b);
        if (endOnFinished) effectInstance.End();
    }

    public static ParticleSystem[] GetParticleSystems(this EffectInstance instance)
    {
        var particleSystems = new List<ParticleSystem>();
        if (instance != null)
            if (instance.effects != null && instance.effects.Count > 0)
                foreach (var effectParticle in instance.effects.OfType<EffectParticle>())
                    if (effectParticle?.rootParticleSystem?.gameObject != null)
                        particleSystems.AddRange(effectParticle.rootParticleSystem.gameObject.GetComponentsInChildren<ParticleSystem>());
        return particleSystems.ToArray();
    }

    public static ParticleSystem[] GetParticleSystems(this List<EffectInstance> instances)
    {
        var particleSystems = new List<ParticleSystem>();
        if (instances != null && instances.Count > 0)
            for (var index = 0; index < instances.Count; index++)
            {
                var instance = instances[index];
                if (instance != null && instance.effects != null && instance.effects.Count > 0)
                    foreach (var effectParticle in instance.effects.OfType<EffectParticle>())
                        if (effectParticle?.rootParticleSystem?.gameObject != null)
                            particleSystems.AddRange(effectParticle.rootParticleSystem.gameObject.GetComponentsInChildren<ParticleSystem>());
            }

        return particleSystems.ToArray();
    }

    public static ParticleSystem GetRootParticleSystem(this EffectInstance instance)
    {
        if (instance != null)
            if (instance.effects != null && instance.effects.Count > 0)
                foreach (var effectParticle in instance.effects.OfType<EffectParticle>())
                    if (effectParticle?.rootParticleSystem?.gameObject != null)
                        return effectParticle.rootParticleSystem;
        return null;
    }

    public static bool TryGetParticleSystem(this EffectInstance instance, string name, out ParticleSystem particleSystem)
    {
        particleSystem = instance.GetParticleSystem(name);
        return particleSystem != null;
    }

    public static ParticleSystem GetParticleSystem(this EffectInstance instance, string name)
    {
        var systems = instance.GetParticleSystems();
        foreach (var system in systems)
            if (system.name == name)
                return system;
        return null;
    }

    public static void SetMaxParticles(this List<EffectInstance> effectInstances, int max)
    {
        var particles = effectInstances.GetParticleSystems();
        if (particles != null && particles.Length > 0)
            foreach (var particle in particles)
            {
                var main = particle.main;
                main.maxParticles = 45;
            }
    }

    public static void SetMaxParticles(this EffectInstance effectInstance, int max)
    {
        var particles = effectInstance.GetParticleSystems();
        if (particles != null && particles.Length > 0)
            foreach (var particle in particles)
            {
                var main = particle.main;
                main.maxParticles = 45;
            }
    }

    public static bool isEmitting(this EffectInstance effectInstance)
    {
        var isEmitting = false;
        foreach (var particleSystem in effectInstance.GetParticleSystems())
            if (particleSystem != null && particleSystem.isEmitting)
                isEmitting = true;
        return isEmitting;
    }

    public static void SetSpeed(this EffectInstance effectInstance, float value, string name)
    {
        foreach (var particleSystem in effectInstance.GetParticleSystems())
            if (particleSystem != null && particleSystem.gameObject.name == name)
            {
                var particles = new ParticleSystem.Particle[particleSystem.main.maxParticles];
                var numParticlesAlive = particleSystem.GetParticles(particles);
                for (var i = 0; i < numParticlesAlive; i++)
                {
                    var particle = particles[i];
                    particle.velocity = particle.velocity.normalized * value;
                    particles[i] = particle;
                }

                particleSystem.SetParticles(particles, numParticlesAlive);
            }
    }

    public static void SetLifetime(this EffectInstance effectInstance, float lifetimeValue, string name)
    {
        foreach (var particleSystem in effectInstance.GetParticleSystems())
            if (particleSystem != null && particleSystem.gameObject.name == name)
            {
                var particles = new ParticleSystem.Particle[particleSystem.main.maxParticles];
                var numParticlesAlive = particleSystem.GetParticles(particles);
                for (var i = 0; i < numParticlesAlive; i++)
                {
                    var particle = particles[i];
                    particle.remainingLifetime = lifetimeValue;
                    particles[i] = particle;
                }

                particleSystem.SetParticles(particles, numParticlesAlive);
            }
    }

    public static void SetConeAngle(this EffectInstance effectInstance, float coneAngleValue, string name)
    {
        foreach (var particleSystem in effectInstance.GetParticleSystems())
            if (particleSystem != null && particleSystem.gameObject.name == name)
            {
                var shape = particleSystem.shape;
                shape.angle = coneAngleValue;
            }
    }

    public static bool IsParticleSystem(this EffectInstance effectInstance)
    {
        return effectInstance.GetRootParticleSystem() != null;
    }

    public static void ForceStop(this EffectInstance effectInstance, ParticleSystemStopBehavior stopBehavior)
    {
        foreach (var particleSystem in effectInstance.GetParticleSystems()) particleSystem.Stop(true, stopBehavior);
    }
    
    public static void ForceStop(this EffectInstance effectInstance, string systemName, ParticleSystemStopBehavior stopBehavior)
    {
         effectInstance.GetParticleSystem(systemName).Stop(true, stopBehavior);
    }

    public static void ForceStop(this List<EffectInstance> effectInstances, ParticleSystemStopBehavior stopBehavior)
    {
        foreach (var particleSystem in effectInstances.GetParticleSystems()) particleSystem.Stop(true, stopBehavior);
    }
}