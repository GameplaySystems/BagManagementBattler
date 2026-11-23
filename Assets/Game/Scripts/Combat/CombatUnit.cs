using System;
using System.Collections.Generic;
using BagManagementBattler.Bag;
using UnityEngine;

namespace BagManagementBattler.Combat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class CombatUnit : MonoBehaviour, ICombatTarget
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform handPoint;
        [SerializeField, Min(0.1f)] private float acquisitionRange = 3.5f;
        [SerializeField, Min(0.02f)] private float targetRefreshInterval = 0.1f;
        // Neighbour look-ahead beyond this unit's own body (query circle = body extent + this).
        [SerializeField, Min(0.01f)] private float separationRadius = 0.45f;
        [SerializeField, Min(0.001f)] private float separationGap = 0.05f;
        [SerializeField, Min(0f)] private float separationSpeed = 1.5f;
        [SerializeField, Min(0.02f)] private float separationInterval = 0.1f;
        [SerializeField, Min(0f)] private float separationDeadZone = 0.02f;
        [SerializeField, Min(0f)] private float engagementMargin = 0.05f;
        [SerializeField, Min(0.1f)] private float velocityResponse = 10f;

        // Separation never exceeds this share of the move speed, so steering toward a target wins.
        private const float MaxSeparationShareOfSpeed = 0.5f;
        // A blocked unit whose sliding step is below this share of its speed waits instead.
        private const float WaitSlideShareOfSpeed = 0.5f;

        private readonly List<SpriteRenderer> unitVisualRenderers =
            new List<SpriteRenderer>();
        private readonly Collider2D[] targetCandidates = new Collider2D[32];
        private readonly CombatUnit[] nearbyOpponents = new CombatUnit[32];
        private readonly List<CombatUnit> areaHitTargets = new List<CombatUnit>(32);
        private int nearbyOpponentCount;
        private SpriteRenderer weaponRenderer;
        private Rigidbody2D body;
        private CapsuleCollider2D bodyCollider;
        private UnitFeedback feedback;
        private ICombatTarget currentTarget;
        private float nextTargetRefreshTime;
        private Vector2 separationVelocity;
        private Vector2 crowdBlockDirection;
        private float nextSeparationTime;
        private bool touchingRestingTeammate;
        private float bandMinY = float.NegativeInfinity;
        private float bandMaxY = float.PositiveInfinity;
        private UnitSpawner ownerSpawner;
        private UnitStateMachine stateMachine;
        private WalkState walkState;
        private WaitingState waitingState;
        private AttackState attackState;
        private DeathState deathState;
        private int successfulAttackCount;

        public UnitDefinition UnitDefinition { get; private set; }
        public BagItemDefinition WeaponDefinition { get; private set; }
        public CombatTeam Team => UnitDefinition != null ? UnitDefinition.Team : default;
        public float MovementSpeed { get; private set; }
        public float EngagementRange { get; private set; }
        public Health Health { get; private set; }
        public UnitStateMachine StateMachine => stateMachine;
        public Rigidbody2D Body => body;
        public Transform VisualRoot => visualRoot;
        public Transform HandPoint => handPoint;
        public ICombatTarget CurrentTarget => currentTarget;
        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;
        public Collider2D Collider => bodyCollider;
        /// <summary>In attack range of the current target (with hysteresis); engaged units do not steer.</summary>
        public bool IsEngaged => stateMachine?.CurrentState is AttackState;
        /// <summary>Engaged, or waiting behind a resting teammate nearer the target; resting units do not steer.</summary>
        public bool IsResting => IsEngaged || stateMachine?.CurrentState is WaitingState;
        public bool IsAlive => Health != null && !Health.IsDepleted;
        public bool IsTargetable => IsInitialized && IsAlive &&
                                    !(stateMachine?.CurrentState is DeathState);
        public bool IsInitialized { get; private set; }
        internal float HitFlashAmount => feedback != null ? feedback.HitFlashAmount : 0f;
        internal int SuccessfulAttackCount => successfulAttackCount;
        internal bool IsPlayingDeathVisual => feedback != null && feedback.IsPlayingDeath;
        internal WalkState WalkState => walkState;
        internal WaitingState WaitingState => waitingState;
        internal AttackState AttackState => attackState;
        internal bool HasValidTarget => IsValidTarget(currentTarget);
        internal float CurrentTargetGap => HasValidTarget
            ? EdgeGapTo(currentTarget)
            : float.PositiveInfinity;
        internal float EngagementReleaseRange => EngagementRange + engagementMargin;
        internal bool IsTouchingRestingTeammate => touchingRestingTeammate;
        internal float WaitSlideSpeedThreshold => MovementSpeed * WaitSlideShareOfSpeed;

        private void Awake()
        {
            EnsureBody();
            EnsureVisualHierarchy();
            EnsureStateMachine();
            EnsureFeedback();
        }

        private void FixedUpdate()
        {
            if (!IsInitialized)
            {
                return;
            }

            feedback.ApplyKnockbackStep();

            if (stateMachine.CurrentState is DeathState)
            {
                body.linearVelocity = Vector2.zero;
                stateMachine.FixedTick();
                return;
            }

            RefreshTargetIfNeeded();
            if (Time.time >= nextSeparationTime)
            {
                separationVelocity = ResolveSeparationVelocity();
                nextSeparationTime = Time.time + separationInterval;
            }

            Vector2 desired = stateMachine.FixedTick() + separationVelocity;
            if (!IsInitialized)
            {
                return;
            }
            // Ease toward the desired velocity (rate scales with move speed) to damp direction flips.
            Vector2 velocity = Vector2.MoveTowards(
                body.linearVelocity,
                desired,
                MovementSpeed * velocityResponse * Time.fixedDeltaTime);
            velocity = WithoutPushIntoOpponents(velocity);
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
            float maxY = float.PositiveInfinity,
            UnitSpawner configuredOwnerSpawner = null)
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
            EnsureStateMachine();
            EnsureFeedback();
            UnitDefinition = loadout.UnitDefinition;
            WeaponDefinition = loadout.WeaponDefinition;
            MovementSpeed = loadout.MovementSpeed;
            EngagementRange = loadout.EngagementRange;
            bandMinY = minY;
            bandMaxY = maxY;
            ownerSpawner = configuredOwnerSpawner;
            spawnPosition.y = Mathf.Clamp(spawnPosition.y, minY, maxY);

            transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
            // Move the physics body too, so collider queries see the spawn position right away.
            body.position = spawnPosition;
            body.linearVelocity = Vector2.zero;
            currentTarget = null;
            nextTargetRefreshTime = 0f;
            separationVelocity = Vector2.zero;
            crowdBlockDirection = Vector2.zero;
            touchingRestingTeammate = false;
            ClearNearbyOpponents();
            nextSeparationTime = 0f;
            feedback.Initialize(body, minY, maxY);
            successfulAttackCount = 0;
            SetVisualFacing(UnitDefinition.Team == CombatTeam.Enemy ? -1f : 1f);
            ConfigureUnitVisuals();
            ConfigureWeaponVisual();
            Health = new Health(UnitDefinition.MaxHealth);
            Health.Depleted += HandleHealthDepleted;
            IsInitialized = true;
            stateMachine.ChangeState(walkState);
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
            EnsureStateMachine();
            EnsureFeedback();
            IsInitialized = false;
            stateMachine.Reset();
            if (Health != null)
            {
                Health.Depleted -= HandleHealthDepleted;
            }
            body.linearVelocity = Vector2.zero;
            transform.SetPositionAndRotation(pooledPosition, Quaternion.identity);
            body.position = pooledPosition;
            currentTarget = null;
            separationVelocity = Vector2.zero;
            crowdBlockDirection = Vector2.zero;
            touchingRestingTeammate = false;
            ClearNearbyOpponents();
            nextSeparationTime = 0f;
            feedback.ResetForPool();
            successfulAttackCount = 0;
            UnitDefinition = null;
            WeaponDefinition = null;
            Health = null;
            ownerSpawner = null;
            MovementSpeed = 0f;
            EngagementRange = 0f;
            bandMinY = float.NegativeInfinity;
            bandMaxY = float.PositiveInfinity;
            SetVisualFacing(1f);
            ClearVisuals();
        }

        private void RefreshTargetIfNeeded()
        {
            // An attack owns its target until AttackState releases it after death or range exit.
            if (stateMachine.CurrentState is AttackState)
            {
                return;
            }

            if (!IsValidTarget(currentTarget))
            {
                currentTarget = null;
            }

            if (stateMachine.CurrentState is WalkState &&
                Time.time >= nextTargetRefreshTime)
            {
                RefreshTargetNow();
            }
        }

        private ICombatTarget FindNearestTarget()
        {
            int candidateCount = Physics2D.OverlapCircle(
                transform.position,
                acquisitionRange,
                ContactFilter2D.noFilter,
                targetCandidates);
            ICombatTarget nearest = null;
            float nearestDistanceSquared = float.PositiveInfinity;

            for (int index = 0; index < candidateCount; index++)
            {
                Collider2D candidateCollider = targetCandidates[index];
                ICombatTarget candidate = candidateCollider != null
                    ? candidateCollider.GetComponent<ICombatTarget>()
                    : null;
                if (!IsValidTarget(candidate))
                {
                    continue;
                }

                float distanceSquared =
                    (candidate.Position - body.position).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearest = candidate;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return nearest;
        }

        private bool IsValidTarget(ICombatTarget candidate)
        {
            if (candidate == null || ReferenceEquals(candidate, this))
            {
                return false;
            }

            // Unity's destroyed-object null check is not preserved by an interface reference.
            if (candidate is UnityEngine.Object unityObject && unityObject == null)
            {
                return false;
            }

            return candidate.IsTargetable && candidate.Team != Team;
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
        public float EdgeGapTo(ICombatTarget other)
        {
            ColliderDistance2D distance = Physics2D.Distance(bodyCollider, other.Collider);
            return distance.isValid ? distance.distance : float.PositiveInfinity;
        }

        public void TakeDamage(float amount)
        {
            if (!IsInitialized || Health == null)
            {
                throw new InvalidOperationException("Initialize the combat unit before damaging it.");
            }

            bool shouldFlash = amount > 0f && !Health.IsDepleted;
            Health.TakeDamage(amount);
            if (shouldFlash)
            {
                feedback.Flash();
            }
        }

        internal void BeginKnockback(Vector2 direction, float distance)
        {
            if (distance <= 0f || direction.sqrMagnitude <= 0.00000001f)
            {
                return;
            }

            feedback.BeginKnockback(direction, distance);
        }

        internal Vector2 AdvanceVelocity()
        {
            return new Vector2(Team == CombatTeam.Friendly ? MovementSpeed : -MovementSpeed, 0f);
        }

        internal Vector2 ResolveChaseSlide()
        {
            Vector2 steer = (currentTarget.Position - body.position).normalized * MovementSpeed;
            float into = Vector2.Dot(steer, crowdBlockDirection);
            return into > 0f ? steer - crowdBlockDirection * into : steer;
        }

        internal void ClearTarget()
        {
            currentTarget = null;
        }

        private void HandleHealthDepleted()
        {
            stateMachine.ChangeState(deathState);
        }

        internal void PerformAttack()
        {
            ICombatTarget primaryTarget = currentTarget;
            if (!IsAlive || !IsValidTarget(primaryTarget))
            {
                return;
            }

            areaHitTargets.Clear();
            CombatUnit primaryUnit = primaryTarget as CombatUnit;
            if (primaryUnit != null && WeaponDefinition.AreaHitRadius <= 0f)
            {
                areaHitTargets.Add(primaryUnit);
            }
            else if (primaryUnit != null)
            {
                int candidateCount = Physics2D.OverlapCircle(
                    primaryTarget.Position,
                    WeaponDefinition.AreaHitRadius,
                    ContactFilter2D.noFilter,
                    targetCandidates);
                float radiusSquared = WeaponDefinition.AreaHitRadius * WeaponDefinition.AreaHitRadius;
                for (int index = 0; index < candidateCount; index++)
                {
                    Collider2D candidateCollider = targetCandidates[index];
                    CombatUnit candidate = candidateCollider != null
                        ? candidateCollider.GetComponent<CombatUnit>()
                        : null;
                    if (IsValidTarget(candidate) &&
                        (candidate.body.position - primaryTarget.Position).sqrMagnitude <= radiusSquared &&
                        !areaHitTargets.Contains(candidate))
                    {
                        areaHitTargets.Add(candidate);
                    }
                }

                if (!areaHitTargets.Contains(primaryUnit))
                {
                    areaHitTargets.Add(primaryUnit);
                }
            }

            if (primaryUnit != null && WeaponDefinition.KnockbackDistance > 0f)
            {
                Vector2 hitDirection = primaryTarget.Position - body.position;
                if (hitDirection.sqrMagnitude <= 0.00000001f)
                {
                    hitDirection = Team == CombatTeam.Friendly ? Vector2.right : Vector2.left;
                }

                primaryUnit.BeginKnockback(hitDirection, WeaponDefinition.KnockbackDistance);
            }

            successfulAttackCount++;

            if (primaryUnit == null)
            {
                if (IsAlive && primaryTarget.IsTargetable)
                {
                    primaryTarget.TakeDamage(WeaponDefinition.Damage);
                }

                return;
            }

            foreach (CombatUnit target in areaHitTargets)
            {
                if (IsAlive && target.IsTargetable)
                {
                    target.TakeDamage(WeaponDefinition.Damage);
                }
            }
        }

        internal void StopForDeath()
        {
            currentTarget = null;
            body.linearVelocity = Vector2.zero;
            separationVelocity = Vector2.zero;
            crowdBlockDirection = Vector2.zero;
            touchingRestingTeammate = false;
            ClearNearbyOpponents();
        }

        internal void PlayDeathFeedback(float releaseDelay)
        {
            feedback.PlayDeath(visualRoot, releaseDelay);
        }

        internal void ReleaseToOwner()
        {
            if (ownerSpawner != null)
            {
                ownerSpawner.Release(this);
            }
        }

        // Checked every step against the opponents the throttled query found nearby: nothing
        // (steering, smoothing lag, teammate pushes) may close more than half the remaining gap
        // to an opponent per step; the opponent may close the other half, so the two never cross.
        private Vector2 WithoutPushIntoOpponents(Vector2 velocity)
        {
            // Removing the part toward one opponent can add speed toward another, so project
            // twice and stand still (always safe) if a limit is still broken.
            for (int pass = 0; pass < 3; pass++)
            {
                bool violated = false;
                for (int index = 0; index < nearbyOpponentCount; index++)
                {
                    CombatUnit opponent = nearbyOpponents[index];
                    if (!IsValidTarget(opponent))
                    {
                        continue;
                    }

                    ColliderDistance2D contact = Physics2D.Distance(bodyCollider, opponent.bodyCollider);
                    if (!contact.isValid)
                    {
                        continue;
                    }

                    // The gap closes along the contact normal, not the centre line (capsules); orient it toward the opponent.
                    Vector2 centreLine = opponent.body.position - body.position;
                    Vector2 toOpponent = Vector2.Dot(contact.normal, centreLine) >= 0f ? contact.normal : -contact.normal;
                    float into = Vector2.Dot(velocity, toOpponent);
                    float maxInto = Mathf.Max(contact.distance, 0f) * 0.5f / Time.fixedDeltaTime;
                    if (into > maxInto + 0.0001f)
                    {
                        violated = true;
                        if (pass < 2)
                        {
                            velocity -= toOpponent * (into - maxInto);
                        }
                    }
                }

                if (!violated)
                {
                    return velocity;
                }
            }

            return Vector2.zero;
        }

        // Strict ordering by distance to the own target, so waiting units can never hold each other in a loop.
        private bool IsCloserToTarget(CombatUnit teammate)
        {
            if (!IsValidTarget(currentTarget))
            {
                return false;
            }

            Vector2 target = currentTarget.Position;
            return (teammate.body.position - target).sqrMagnitude < (body.position - target).sqrMagnitude;
        }

        private void ClearNearbyOpponents()
        {
            Array.Clear(nearbyOpponents, 0, nearbyOpponentCount);
            nearbyOpponentCount = 0;
        }

        private Vector2 ResolveSeparationVelocity()
        {
            int candidateCount = Physics2D.OverlapCircle(
                body.position,
                bodyCollider.bounds.extents.magnitude + separationRadius,
                ContactFilter2D.noFilter,
                targetCandidates);
            Vector2 teamPush = Vector2.zero;
            Vector2 opponentPush = Vector2.zero;
            Vector2 blockSum = Vector2.zero;
            touchingRestingTeammate = false;
            ClearNearbyOpponents();

            for (int index = 0; index < candidateCount; index++)
            {
                Collider2D candidateCollider = targetCandidates[index];
                CombatUnit other = candidateCollider != null
                    ? candidateCollider.GetComponent<CombatUnit>()
                    : null;
                if (other == null || other == this || !other.IsTargetable)
                {
                    continue;
                }

                float gap = EdgeGapTo(other);
                bool teammate = other.Team == Team;
                if (!teammate)
                {
                    nearbyOpponents[nearbyOpponentCount++] = other;
                }
                else if (gap < separationGap && other.IsResting && IsCloserToTarget(other))
                {
                    touchingRestingTeammate = true;
                }

                // Moving units keep a small gap to teammates. Resting units (and every opponent pair)
                // only push apart once they truly overlap, so a packed ring around a target can rest.
                // All ignore the dead zone just below their threshold, so barely-touching units stay still.
                float threshold = (teammate && !IsResting ? separationGap : 0f) - separationDeadZone;
                if (gap >= threshold)
                {
                    continue;
                }

                Vector2 offset = body.position - other.body.position;
                // Units on the same spot get a stable up/down split instead of cancelling out.
                Vector2 direction = offset.sqrMagnitude > 0.00000001f
                    ? offset.normalized
                    : (GetInstanceID() > other.GetInstanceID() ? Vector2.up : Vector2.down);
                if (teammate)
                {
                    teamPush += direction * Mathf.Clamp01((threshold - gap) / separationGap);
                    blockSum -= direction;
                }
                else
                {
                    // Steep ramp so an opponent overlap is undone before it grows past the dead zone.
                    opponentPush += direction * Mathf.Clamp01(
                        (threshold - gap) / Mathf.Max(separationDeadZone, 0.001f));
                }
            }

            crowdBlockDirection = blockSum.normalized;

            // Team push stays weaker than steering; opponent push may reach full move speed.
            return Vector2.ClampMagnitude(
                       teamPush * separationSpeed,
                       MovementSpeed * MaxSeparationShareOfSpeed) +
                   Vector2.ClampMagnitude(opponentPush * separationSpeed, MovementSpeed);
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

        private void EnsureStateMachine()
        {
            if (stateMachine != null)
            {
                return;
            }

            stateMachine = new UnitStateMachine();
            walkState = new WalkState(this);
            waitingState = new WaitingState(this);
            attackState = new AttackState(this);
            deathState = new DeathState(this);
        }

        private void EnsureFeedback()
        {
            if (feedback == null)
            {
                feedback = GetComponent<UnitFeedback>();
                if (feedback == null)
                {
                    feedback = gameObject.AddComponent<UnitFeedback>();
                }
            }
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
            feedback.RegisterRenderer(renderer);
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
            feedback.RegisterRenderer(weaponRenderer);
            return weaponRenderer;
        }
    }
}
