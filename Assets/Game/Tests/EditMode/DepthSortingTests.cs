using BagManagementBattler.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BagManagementBattler.Tests
{
    // Batch tests run with -nographics, so this checks the sorting inputs on the real assets
    // instead of reading rendered pixels.
    public sealed class DepthSortingTests
    {
        private const string RendererPath = "Assets/Settings/Renderer2D.asset";
        private const string PipelinePath = "Assets/Settings/UniversalRP.asset";
        private const string UnitPrefabPath = "Assets/Game/Prefabs/CombatUnit.prefab";

        [Test]
        public void SortingLayers_AreOrderedBackToFront()
        {
            string[] backToFront = { "Background", "Battlefield", "Effects", "Bag", "UI" };
            for (int index = 0; index < backToFront.Length; index++)
            {
                Assert.That(SortingLayer.IsValid(SortingLayer.NameToID(backToFront[index])), Is.True,
                    backToFront[index]);
                if (index > 0)
                {
                    Assert.That(
                        SortingLayer.GetLayerValueFromName(backToFront[index]),
                        Is.GreaterThan(SortingLayer.GetLayerValueFromName(backToFront[index - 1])));
                }
            }
        }

        [Test]
        public void LowerUnit_RendersInFront_OfAHigherUnit()
        {
            // Every unit is one Sorting Group on the same layer and order, so the 2D renderer's
            // custom axis (0, 1, 0) decides: lower Y is drawn later, i.e. in front.
            CombatUnit prefab = AssetDatabase.LoadAssetAtPath<CombatUnit>(UnitPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            SortingGroup group = prefab.GetComponent<SortingGroup>();
            Assert.That(group, Is.Not.Null);
            Assert.That(group.sortingLayerName, Is.EqualTo("Battlefield"));
            Assert.That(group.sortingOrder, Is.Zero);

            Object rendererData = AssetDatabase.LoadMainAssetAtPath(RendererPath);
            var renderer = new SerializedObject(rendererData);
            Assert.That(
                renderer.FindProperty("m_TransparencySortMode").intValue,
                Is.EqualTo((int)TransparencySortMode.CustomAxis));
            Assert.That(
                renderer.FindProperty("m_TransparencySortAxis").vector3Value,
                Is.EqualTo(new Vector3(0f, 1f, 0f)));

            // The pipeline the project renders with (set per quality level) must use that renderer.
            Assert.That(
                AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline),
                Is.EqualTo(PipelinePath));
            var pipeline = new SerializedObject(GraphicsSettings.currentRenderPipeline);
            int defaultIndex = pipeline.FindProperty("m_DefaultRendererIndex").intValue;
            Assert.That(
                pipeline.FindProperty("m_RendererDataList")
                    .GetArrayElementAtIndex(defaultIndex).objectReferenceValue,
                Is.SameAs(rendererData));
        }
    }
}
