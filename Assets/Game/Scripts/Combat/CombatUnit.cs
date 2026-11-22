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
        [SerializeField, Min(0.001f)] private float separationGap = 0.05f;
        [SerializeField, Min(0f)] private float separationSpeed = 1.5f;
        [SerializeField, Min(0.02f)] private float separationInterval = 0.1f;

        // Separation never exceeds this share of the move speed, so steering toward a target wins.
        private const float MaxSeparationShareOfSpeed = 0.5f;

        private readonly List<SpriteRenderer> unitVisualRenderers =
            new List<SpriteRenderer>();
        private readonly Collider2D[] targetCandidates = new Collider2D[32];
        private SpriteRenderer weaponRenderer;
        private Rigidbody2D body;
        private CapsuleCollider2D bodyCollider;
        private CombatUnit currentTarget;
        private float nextTargetRefreshTime;
        private Vector2 separationVelocity;
        private float nextSeparationTime;
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
            if (Time.time >= nextSeparationTime)
            {
                separationVelocity = ResolveSeparationVelocity();
                nextSeparationTime = Time.time + separationInterval;
            }

            Vector2 velocity = ResolveMoveVelocity() + separationVelocity;
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
            // Move the physics body too, so collider queries see the spawn position right away.
            body.position = spawnPosition;
            body.linearVelocity = Vector2.zero;
            currentTarget = null;
            nextTargetRefreshTime = 0f;
            separationVelocity = Vector2.zero;
            nextSeparationTime = 0f;
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
            body.position = pooledPosition;
            currentTarget = null;
            separationVelocity = Vector2.zero;
            nextSeparationTime = 0f;
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

        /// <summary>Scene-view lines at the battlefield band's min/max Y, drawn by the spawners.</summary>
        internal static void DrawBandGizmo(float centreX, float minY, float maxY)
        {
            const float halfLength = 6f;
            Gizmos.color = Color.yellow;
            foreach (float y in new[] { minY, maxY })
            {
                if (!float.IsInfinity(y))
                {
                    Gizmos.DrawLine(
                        new Vector3(centreX - halfLength, y, 0f),
                        new Vector3(centreX + halfLength, y, 0f));
                }
            }
        }

        /// <summary>Collider edge-to-edge distance; negative while the bodies overlap.</summary>
        public float EdgeGapTo(CombatUnit other)
        {
            ColliderDistance2D distance = Physics2D.Distance(bodyCollider, other.bodyCollider);
            return distance.isValid ? distance.distance : float.PositiveInfinity;
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

        private Vector2 ResolveSeparationVelocity()
        {
            int candidateCount = Physics2D.OverlapCircle(
                body.position,
                separationRadius,
                ContactFilter2D.noFilter,
                targetCandidates);
            Vector2 push = Vector2.zero;

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

                float gap = EdgeGapTo(other);
                if (gap >= separationGap)
                {
                    continue;
                }

                Vector2 offset = body.position - other.body.position;
                // Units on the same spot get a stable up/down split instead of cancelling out.
                Vector2 direction = offset.sqrMagnitude > 0.00000001f
                    ? offset.normalized
                    : (GetInstanceID() > other.GetInstanceID() ? Vector2.up : Vector2.down);
                push += direction * Mathf.Clamp01((separationGap - gap) / separationGap);
            }

            return Vector2.ClampMagnitude(
                push * separationSpeed,
                MovementSpeed * MaxSeparationShareOfSpeed);
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
