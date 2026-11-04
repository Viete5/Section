using System;
using System.Linq;
using NUnit.Framework;
using Section.Sandbox.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Section.Sandbox.Tests
{
    public sealed class SandboxLayoutTests
    {
        Scene scene;
        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.OpenScene(SandboxSceneBuilder.ScenePath, OpenSceneMode.Single);
        }

        [Test]
        public void ShellMatchesApprovedDimensionsAndOpening()
        {
            Assert.That(SandboxLayoutValidator.Validate(scene), Is.Empty);
            var definition = AssetDatabase.LoadAssetAtPath<SandboxDefinition>(SandboxSceneBuilder.DefinitionPath);
            float[] depths = { 9.85f, 9.7f, 9.7f, 9.85f };
            for (int i = 0; i < 4; i++)
            {
                Bounds room = definition.ClearRoomBounds(i);
                Assert.That(room.size.x, Is.EqualTo(14).Within(.0001f));
                Assert.That(room.size.y, Is.EqualTo(5).Within(.0001f));
                Assert.That(room.size.z, Is.EqualTo(depths[i]).Within(.0001f));
            }
            AssertBounds("wall.left", new Vector3(-7.3f, 0, -.3f), new Vector3(-7, 5, 40.3f));
            AssertBounds("wall.right", new Vector3(7, 0, -.3f), new Vector3(7.3f, 5, 40.3f));
            AssertBounds("wall.start", new Vector3(-7, 0, -.3f), new Vector3(7, 5, 0));
            AssertBounds("wall.end", new Vector3(-7, 0, 40), new Vector3(7, 5, 40.3f));
            AssertBounds("ceiling", new Vector3(-7.3f, 5, -.3f), new Vector3(7.3f, 5.3f, 40.3f));
            for (int i = 1; i <= 3; i++)
            {
                AssertBounds("partition." + i + ".-1", new Vector3(-7, 0, i * 10 - .15f), new Vector3(-1.5f, 5, i * 10 + .15f));
                AssertBounds("partition." + i + ".1", new Vector3(1.5f, 0, i * 10 - .15f), new Vector3(7, 5, i * 10 + .15f));
            }
            AssertBounds("pit.bottom", new Vector3(2.5f, -2.8f, 13), new Vector3(6, -2.5f, 18));
            var stations = SandboxSceneBuilder.Find(scene, "stations").transform;
            Assert.That(stations.childCount, Is.EqualTo(12));
            Assert.That(stations.Cast<Transform>().Select(t => t.name), Is.EquivalentTo(SandboxSceneBuilder.StationNames));
        }

        [Test]
        public void FloorCoverageHasOnlyTheSpecifiedPitAndItsPhysicalLining()
        {
            Physics.SyncTransforms();
            for (float x = -6.75f; x < 7; x += .5f)
                for (float z = .25f; z < 40; z += .5f)
                {
                    var hits = Physics.RaycastAll(new Vector3(x, 2, z), Vector3.down, 6)
                        .Where(h => h.collider is BoxCollider && h.normal.y > .99f &&
                            (h.collider.GetComponent<SandboxGeneratedObject>()?.Key.StartsWith("floor.") == true ||
                             h.collider.GetComponent<SandboxGeneratedObject>()?.Key == "pit.bottom")).OrderBy(h => h.distance).ToArray();
                    Assert.That(hits, Is.Not.Empty, "Missing floor at " + x + ", " + z);
                    float expected = x > 2.5f && x < 6 && z > 13 && z < 18 ? -2.5f : 0;
                    Assert.That(hits[0].point.y, Is.EqualTo(expected).Within(.001f), "Floor height at " + x + ", " + z);
                }
            AssertBounds("pit.left", new Vector3(2.2f, -2.5f, 13), new Vector3(2.5f, -.3f, 18));
            AssertBounds("pit.right", new Vector3(6, -2.5f, 13), new Vector3(6.3f, -.3f, 18));
            AssertBounds("pit.front", new Vector3(2.5f, -2.5f, 12.7f), new Vector3(6, -.3f, 13));
            AssertBounds("pit.back", new Vector3(2.5f, -2.5f, 18), new Vector3(6, -.3f, 18.3f));
        }

        [Test]
        public void RebuildIsIdempotentAndPreservesUnrelatedObjectAndGuids()
        {
            var sentinel = new GameObject("UserAuthoredSentinel");
            sentinel.transform.SetParent(SandboxSceneBuilder.Find(scene, "shell").transform);
            sentinel.transform.position = new Vector3(2, 1, 4);
            EditorSceneManager.SaveScene(scene);
            string sceneGuid = AssetDatabase.AssetPathToGUID(SandboxSceneBuilder.ScenePath);
            string definitionGuid = AssetDatabase.AssetPathToGUID(SandboxSceneBuilder.DefinitionPath);
            string materialPath = SandboxSceneBuilder.AssetRoot + "/Materials/Wall.mat";
            string materialGuid = AssetDatabase.AssetPathToGUID(materialPath);
            int count = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Count();
            try
            {
                SandboxSceneBuilder.Build();
                SandboxSceneBuilder.Build();
                Assert.That(sentinel, Is.Not.Null);
                Assert.That(sentinel.transform.position, Is.EqualTo(new Vector3(2, 1, 4)));
                Assert.That(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Count(), Is.EqualTo(count));
                Assert.That(AssetDatabase.AssetPathToGUID(SandboxSceneBuilder.ScenePath), Is.EqualTo(sceneGuid));
                Assert.That(AssetDatabase.AssetPathToGUID(SandboxSceneBuilder.DefinitionPath), Is.EqualTo(definitionGuid));
                Assert.That(AssetDatabase.AssetPathToGUID(materialPath), Is.EqualTo(materialGuid));
                Assert.That(SandboxLayoutValidator.Validate(scene), Is.Empty);
            }
            finally
            {
                if (sentinel != null) UnityEngine.Object.DestroyImmediate(sentinel);
                EditorSceneManager.SaveScene(scene);
            }
        }

        [Test]
        public void RebuildRefusesToOverwriteManualGeometryEdit()
        {
            var wall = SandboxSceneBuilder.Find(scene, "partition.1.-1").transform;
            Vector3 original = wall.localPosition;
            try
            {
                wall.localPosition += Vector3.right * .1f;
                EditorSceneManager.SaveScene(scene);
                var exception = Assert.Throws<InvalidOperationException>(() => SandboxSceneBuilder.Build());
                Assert.That(exception.Message, Does.Contain("partition.1.-1"));
                Assert.That(wall.localPosition, Is.EqualTo(original + Vector3.right * .1f));
            }
            finally
            {
                wall.localPosition = original;
                EditorSceneManager.SaveScene(scene);
            }
        }

        void AssertBounds(string key, Vector3 min, Vector3 max)
        {
            Bounds actual = SandboxSceneBuilder.Find(scene, key).GetComponent<Collider>().bounds;
            Assert.That(Vector3.Distance(actual.min, min), Is.LessThan(.0001f), key + " min");
            Assert.That(Vector3.Distance(actual.max, max), Is.LessThan(.0001f), key + " max");
        }
    }
}
