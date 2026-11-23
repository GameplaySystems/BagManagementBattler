using BagManagementBattler.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace BagManagementBattler.Presentation
{
    [RequireComponent(typeof(PlayerBase))]
    public sealed class PlayerBaseHealthBar : MonoBehaviour
    {
        private PlayerBase playerBase;
        private RectTransform fill;

        public float FillFraction => fill != null ? fill.localScale.x : 0f;

        private void Awake()
        {
            playerBase = GetComponent<PlayerBase>();
            CreateVisuals();
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void CreateVisuals()
        {
            var canvasObject = new GameObject("Health Bar", typeof(RectTransform), typeof(Canvas));
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.SetParent(transform, false);
            canvasRect.localPosition = new Vector3(0f, 6.2f, -0.1f);
            canvasRect.sizeDelta = new Vector2(12f, 1.2f);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerName = "Battlefield";
            canvas.sortingOrder = 10;

            RectTransform background = CreateImage(
                canvasRect,
                "Background",
                new Color(0.08f, 0.08f, 0.08f, 0.9f));
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;

            fill = CreateImage(background, "Fill", new Color(0.2f, 0.85f, 0.25f, 1f));
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(0.25f, 0.2f);
            fill.offsetMax = new Vector2(-0.25f, -0.2f);
            fill.pivot = new Vector2(0f, 0.5f);
        }

        private void Refresh()
        {
            if (fill == null || playerBase.Health == null)
            {
                return;
            }

            float fraction = playerBase.Health.Current / playerBase.Health.Max;
            fill.localScale = new Vector3(fraction, 1f, 1f);
        }

        private static RectTransform CreateImage(Transform parent, string name, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }
    }
}
