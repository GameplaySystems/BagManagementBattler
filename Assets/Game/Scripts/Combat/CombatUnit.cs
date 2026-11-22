using System;
using System.Collections.Generic;
using BagManagementBattler.Bag;
using UnityEngine;

namespace BagManagementBattler.Combat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class CombatUnit : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform handPoint;
        [SerializeField, Min(0.1f)] private float acquisitionRange = 20f;
        [SerializeField, Min(0.02f)] private float targetRefreshInterval = 0.1f;
        [SerializeField, Min(0.01f)] private float separationRadius = 0.45f;
        [SerializeField, Min(0f)] private float separationSpeed = 1.5f;

        private readonly List<SpriteRenderer> unitVisualRenderers =
            new List<SpriteRenderer>();
        private readonly Collider2D[] targetCandidates = new Collider2D[32];
        private SpriteRenderer weaponRenderer;
        private Rigidbody2D body;
        private CapsuleCollider2D bodyCollider;
        private CombatUnit currentTarget;
        private float nextTargetRefreshTime;
        private float bandMinY = float.NegativeInfinity;
        private float bandMaxY = float.PositiveInfinity;

        public UnitDefinition UnitDefinition { get; private set; }
        public BagItemDefinition WeaponDefinition { get; private set; }
        public CombatTeam Team => UnitDefinition != null ? UnitDefinition.Team : default;
        public float MovementSpeed { get; private set; }
        public float EngagementRange { get; private set; }
        public Rigidbody2D Body => body;
        public Transform VisualRoot => visualRoot;
        public Transform HandPoint => handPoint;
        public float HalfWidth =>
            bodyCollider.size.x * 0.5f * Mathf.Abs(transform.lossyScale.x);
        public CombatUnit CurrentTarget => currentTarget;
        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            EnsureBody();
            EnsureVisualHierarchy();
        }

        private void FixedUpdate()
        {
            if (!IsInitialized)
            {
                return;
            }

            RefreshTargetIfNeeded();
            Vector2 velocity = ResolveMoveVelocity() + Vector2.up * ResolveSeparationVelocity();
            // Limit the vertical step so the next position stays inside the battlefield band.
            float currentY = body.position.y;
            float nextY = Mathf.Clamp(currentY + velocity.y * Time.fixedDeltaTime, bandMinY, bandMaxY);
            velocity.y = (nextY - currentY) / Time.fixedDeltaTime;
            body.linearVelocity = velocity;
        }

        private void OnDisable()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            currentTarget = null;
        }

        public void Initialize(
            CombatUnitLoadout loadout,
            Vector3 spawnPosition,
            float minY = float.NegativeInfinity,
            float maxY = float.PositiveInfinity)
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException("The combat unit is already initialized.");
            }

            if (float.IsNaN(minY) || float.IsNaN(maxY) || minY > maxY)
            {
                throw new ArgumentException("The battlefield band needs minY <= maxY.");
            }

            EnsureBody();
            EnsureVisualHierarchy();
            UnitDefinition = loadout.UnitDefinition;
            WeaponDefinition = loadout.WeaponDefinition;
            MovementSpeed = loadout.MovementSpeed;
            EngagementRange = loadout.EngagementRange;
            bandMinY = minY;
            bandMaxY = maxY;
            spawnPosition.y = Mathf.Clamp(spawnPosition.y, minY, maxY);

            transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
            body.linearVelocity = Vector2.zero;
            currentTarget = null;
            nextTargetRefreshTime = 0f;
            SetVisualFacing(UnitDefinition.Team == CombatTeam.Enemy ? -1f : 1f);
            ConfigureUnitVisuals();
            ConfigureWeaponVisual();
            IsInitialized = true;
        }

        public void RefreshTargetNow()
        {
            if (!IsInitialized)
            {
                currentTarget = null;
                return;
            }

            currentTarget = FindNearestTarget();
            nextTargetRefreshTime = Time.time + targetRefreshInterval;
        }

        internal void ResetForPool(Vector3 pooledPosition)
        {
            EnsureBody();
            EnsureVisualHierarchy();
            IsInitialized = false;
            body.linearVelocity = Vector2.zero;
            transform.SetPositionAndRotation(pooledPosition, Quaternion.identity);
            currentTarget = null;
            UnitDefinition = null;
            WeaponDefinition = null;
            MovementSpeed = 0f;
            EngagementRange = 0f;
            bandMinY = float.NegativeInfinity;
            bandMaxY = float.PositiveInfinity;
            SetVisualFacing(1f);
            ClearVisuals();
        }

        private void RefreshTargetIfNeeded()
        {
            if (IsValidTarget(currentTarget))
            {
                return;
            }

            currentTarget = null;
            if (Time.time >= nextTargetRefreshTime)
            {
                RefreshTargetNow();
            }
        }

        private CombatUnit FindNearestTarget()
        {
            int candidateCount = Physics2D.OverlapCircle(
                transform.position,
                acquisitionRange,
                ContactFilter2D.noFilter,
                targetCandidates);
            CombatUnit nearest = null;
            float nearestDistanceSquared = float.PositiveInfinity;

            for (int index = 0; index < candidateCount; index++)
            {
                Collider2D candidateCollider = targetCandidates[index];
                CombatUnit candidate = candidateCollider != null
                    ? candidateCollider.GetComponent<CombatUnit>()
                    : null;
                if (!IsValidTarget(candidate))
                {
                    continue;
                }

                float distanceSquared =
                    ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearest = candidate;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return nearest;
        }

        private bool IsValidTarget(CombatUnit candidate)
        {
            return candidate != null &&
                   candidate != this &&
                   candidate.IsInitialized &&
                   candidate.isActiveAndEnabled &&
                   candidate.gameObject.activeInHierarchy &&
                   candidate.Team != Team;
        }

        public float EdgeGapTo(CombatUnit other)
        {
            float centreDistance = Vector2.Distance(transform.position, other.transform.position);
            return centreDistance - HalfWidth - other.HalfWidth;
        }

        private Vector2 ResolveMoveVelocity()
        {
            if (IsValidTarget(currentTarget))
            {
                if (EdgeGapTo(currentTarget) <= EngagementRange)
                {
                    return Vector2.zero;
                }

                Vector2 delta = currentTarget.transform.position - transform.position;
                return delta.normalized * MovementSpeed;
            }

            return new Vector2(Team == CombatTeam.Friendly ? MovementSpeed : -MovementSpeed, 0f);
        }

        private float ResolveSeparationVelocity()
        {
            int candidateCount = Physics2D.OverlapCircle(
                transform.position,
                separationRadius,
                ContactFilter2D.noFilter,
                targetCandidates);
            float push = 0f;

            for (int index = 0; index < candidateCount; index++)
            {
                Collider2D candidateCollider = targetCandidates[index];
                CombatUnit other = candidateCollider != null
                    ? candidateCollider.GetComponent<CombatUnit>()
                    : null;
                if (other == null || other == this || !other.IsInitialized || other.Team != Team)
                {
                    continue;
                }

                Vector2 offset = (Vector2)transform.position - (Vector2)other.transform.position;
                float distance = offset.magnitude;
                if (distance >= separationRadius)
                {
                    continue;
                }

                // Units on the same lane get a stable up/down split instead of cancelling out.
                float direction = Mathf.Abs(offset.y) > 0.0001f
                    ? Mathf.Sign(offset.y)
                    : (GetInstanceID() > other.GetInstanceID() ? 1f : -1f);
                push += direction * (1f - distance / separationRadius);
            }

            // shortcut: vertical-only push, no crowd solver; revisit if large waves pile up.
            return push * separationSpeed;
        }

        private void SetVisualFacing(float sign)
        {
            Vector3 scale = visualRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * sign;
            visualRoot.localScale = scale;
        }

        private void EnsureBody()
        {
            if (body != null)
            {
                return;
            }

            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<CapsuleCollider2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.linearVelocity = Vector2.zero;
        }

        private void EnsureVisualHierarchy()
        {
            if (visualRoot == null)
            {
                visualRoot = transform.Find("Visual");
                if (visualRoot == null)
                {
                    visualRoot = new GameObject("Visual").transform;
                    visualRoot.SetParent(transform, false);
                }
            }

            if (handPoint == null)
            {
                handPoint = visualRoot.Find("HandPoint");
                if (handPoint == null)
                {
                    handPoint = new GameObject("HandPoint").transform;
                    handPoint.SetParent(visualRoot, false);
                }
            }
        }

        private void ConfigureUnitVisuals()
        {
            int rendererIndex = 0;
            rendererIndex = ConfigureUnitVisual(
                rendererIndex,
                UnitDefinition.BodySprite,
                UnitVisualLayout.BodyPositionInCells,
                31);
            rendererIndex = ConfigureUnitVisual(
                rendererIndex,
                UnitDefinition.HeadSprite,
                UnitVisualLayout.HeadPositionInCells,
                32);

            for (int index = rendererIndex; index < unitVisualRenderers.Count; index++)
            {
                unitVisualRenderers[index].sprite = null;
                unitVisualRenderers[index].enabled = false;
            }
        }

        private int ConfigureUnitVisual(
            int rendererIndex,
            Sprite sprite,
            Vector2 positionInCells,
            int sortingOrder)
        {
            if (sprite == null)
            {
                return rendererIndex;
            }

            SpriteRenderer renderer = GetOrCreateUnitRenderer(rendererIndex++);
            renderer.sprite = sprite;
            renderer.transform.localPosition = new Vector3(
                positionInCells.x * UnitVisualLayout.FieldLayoutCellSize,
                positionInCells.y * UnitVisualLayout.FieldLayoutCellSize,
                0f);
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale =
                Vector3.one * UnitVisualLayout.ScaleInCells * UnitVisualLayout.FieldLayoutCellSize;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = true;
            return rendererIndex;
        }

        private void ConfigureWeaponVisual()
        {
            Sprite weaponSprite = WeaponDefinition.IconSprite;
            if (weaponSprite == null)
            {
                foreach (BagItemVisualPart part in WeaponDefinition.VisualParts)
                {
                    if (part.Sprite != null)
                    {
                        weaponSprite = part.Sprite;
                        break;
                    }
                }
            }

            if (weaponSprite == null)
            {
                ClearWeaponVisual();
                return;
            }

            SpriteRenderer renderer = GetOrCreateWeaponRenderer();
            renderer.sprite = weaponSprite;
            renderer.transform.localPosition = WeaponDefinition.EquippedLocalPosition;
            renderer.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                WeaponDefinition.EquippedLocalRotationDegrees);
            Vector2 scale = WeaponDefinition.EquippedLocalScale;
            renderer.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            renderer.sortingOrder = 40;
            renderer.enabled = true;
        }

        private void ClearVisuals()
        {
            foreach (SpriteRenderer renderer in unitVisualRenderers)
            {
                renderer.sprite = null;
                renderer.enabled = false;
            }

            ClearWeaponVisual();
        }

        private void ClearWeaponVisual()
        {
            if (weaponRenderer == null)
            {
                return;
            }

            weaponRenderer.sprite = null;
            weaponRenderer.enabled = false;
        }

        private SpriteRenderer GetOrCreateUnitRenderer(int index)
        {
            if (index < unitVisualRenderers.Count)
            {
                return unitVisualRenderers[index];
            }

            GameObject visualObject = new GameObject($"Unit Visual {index}");
            visualObject.transform.SetParent(visualRoot, false);
            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            unitVisualRenderers.Add(renderer);
            return renderer;
        }

        private SpriteRenderer GetOrCreateWeaponRenderer()
        {
            if (weaponRenderer != null)
            {
                return weaponRenderer;
            }

            GameObject visualObject = new GameObject("Weapon Visual");
            visualObject.transform.SetParent(handPoint, false);
            weaponRenderer = visualObject.AddComponent<SpriteRenderer>();
            return weaponRenderer;
        }
    }
}
