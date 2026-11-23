using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace BagManagementBattler.Combat
{
    public sealed class UnitFeedback : MonoBehaviour
    {
        private const float KnockbackDuration = 0.1f;
        private const float HitFlashDuration = 0.1f;
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        // Shared by every unit (the property block carries the per-unit flash). A serialized
        // reference keeps the shader in player builds; units built without it simply do not flash white.
        [SerializeField] private Material flashMaterial;

        private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        private MaterialPropertyBlock flashProperties;
        private Rigidbody2D body;
        private Vector2 knockbackRemaining;
        private float knockbackTimeRemaining;
        private float flashEndTime;
        private float bandMinY = float.NegativeInfinity;
        private float bandMaxY = float.PositiveInfinity;
        // Death squash: final scale relative to the visual root's scale when death starts.
        private static readonly Vector2 DeathSquash = new Vector2(1.15f, 0.75f);
        private Tween deathTween;
        private Transform deathVisualRoot;
        private Vector3 deathBaseScale;

        internal float HitFlashAmount { get; private set; }
        internal bool IsPlayingDeath => deathTween != null && deathTween.IsActive();
        public Material FlashMaterial => flashMaterial;

        private void Awake()
        {
            EnsureFlashProperties();
        }

        private void Update()
        {
            if (HitFlashAmount > 0f && Time.time >= flashEndTime)
            {
                SetFlashAmount(0f);
            }
        }

        internal void Initialize(Rigidbody2D configuredBody, float minY, float maxY)
        {
            body = configuredBody;
            bandMinY = minY;
            bandMaxY = maxY;
            knockbackRemaining = Vector2.zero;
            knockbackTimeRemaining = 0f;
            SetFlashAmount(0f);
        }

        internal void RegisterRenderer(SpriteRenderer renderer)
        {
            if (renderer == null || renderers.Contains(renderer))
            {
                return;
            }

            renderers.Add(renderer);
            if (flashMaterial != null)
            {
                renderer.sharedMaterial = flashMaterial;
            }

            EnsureFlashProperties();
            renderer.SetPropertyBlock(HitFlashAmount > 0f ? flashProperties : null);
        }

        /// <summary>Squashes the visual root slightly and fades every unit renderer out over the duration.</summary>
        internal void PlayDeath(Transform visualRoot, float duration)
        {
            StopDeath();
            deathVisualRoot = visualRoot;
            deathBaseScale = visualRoot.localScale;
            deathTween = DOTween.To(ApplyDeathProgress, 0f, 1f, duration)
                .SetEase(Ease.OutQuad)
                .SetTarget(this);
        }

        internal void Flash()
        {
            flashEndTime = Time.time + HitFlashDuration;
            SetFlashAmount(1f);
        }

        internal void BeginKnockback(Vector2 direction, float distance)
        {
            if (distance <= 0f || direction.sqrMagnitude <= 0.00000001f)
            {
                return;
            }

            knockbackRemaining += direction.normalized * distance;
            knockbackTimeRemaining = KnockbackDuration;
        }

        internal void ApplyKnockbackStep()
        {
            if (body == null ||
                knockbackTimeRemaining <= 0f ||
                knockbackRemaining.sqrMagnitude <= 0.00000001f)
            {
                knockbackRemaining = Vector2.zero;
                knockbackTimeRemaining = 0f;
                return;
            }

            float stepTime = Mathf.Min(Time.fixedDeltaTime, knockbackTimeRemaining);
            Vector2 step = knockbackRemaining * (stepTime / knockbackTimeRemaining);
            Vector2 position = body.position + step;
            position.y = Mathf.Clamp(position.y, bandMinY, bandMaxY);
            Vector2 applied = position - body.position;
            body.position = position;
            knockbackRemaining -= applied;
            if (!Mathf.Approximately(applied.y, step.y))
            {
                knockbackRemaining.y = 0f;
            }

            knockbackTimeRemaining -= stepTime;
            if (knockbackTimeRemaining <= 0f)
            {
                knockbackRemaining = Vector2.zero;
            }
        }

        internal void ResetForPool()
        {
            knockbackRemaining = Vector2.zero;
            knockbackTimeRemaining = 0f;
            bandMinY = float.NegativeInfinity;
            bandMaxY = float.PositiveInfinity;
            SetFlashAmount(0f);
            StopDeath();
        }

        private void OnDestroy()
        {
            deathTween?.Kill();
        }

        // Kills the death tween and restores the visual root's scale and full alpha.
        private void StopDeath()
        {
            deathTween?.Kill();
            deathTween = null;
            if (deathVisualRoot != null)
            {
                ApplyDeathProgress(0f);
                deathVisualRoot = null;
            }
        }

        private void ApplyDeathProgress(float progress)
        {
            if (deathVisualRoot != null)
            {
                Vector2 squash = Vector2.Lerp(Vector2.one, DeathSquash, progress);
                deathVisualRoot.localScale = new Vector3(
                    deathBaseScale.x * squash.x, deathBaseScale.y * squash.y, deathBaseScale.z);
            }

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    Color color = renderer.color;
                    color.a = 1f - progress;
                    renderer.color = color;
                }
            }
        }

        private void SetFlashAmount(float amount)
        {
            EnsureFlashProperties();
            HitFlashAmount = amount;
            flashProperties.Clear();
            flashProperties.SetFloat(FlashAmountId, amount);
            // No block while not flashing: the material default (0) applies and sprites can batch.
            MaterialPropertyBlock block = amount > 0f ? flashProperties : null;
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(block);
                }
            }
        }

        private void EnsureFlashProperties()
        {
            if (flashProperties == null)
            {
                flashProperties = new MaterialPropertyBlock();
            }
        }
    }
}
