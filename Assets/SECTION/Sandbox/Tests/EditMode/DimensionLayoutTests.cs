using System.Linq;
using NUnit.Framework;
using Section.Sandbox.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Section.Sandbox.Tests
{
    public sealed class DimensionLayoutTests
    {
        Scene scene;
        Transform Find(string id)=>SandboxSceneBuilder.Find(scene,id).transform;
        [SetUp] public void SetUp(){scene=EditorSceneManager.OpenScene(SandboxSceneBuilder.ScenePath);Physics.SyncTransforms();}
        [Test] public void ActualC2WindowAndBodyMatchTheMathematicalNoThreeDimensionalBypassProof()
        {
            var source=Find("geometry.C2").GetComponent<GeometrySliceView>();var h=source.PrismHalfSize;
            Assert.That(h.X*2,Is.EqualTo(1.6));Assert.That(h.Y*2,Is.EqualTo(.5));Assert.That(h.Z*2,Is.EqualTo(1.6));Assert.That(h.W*2,Is.EqualTo(.3));
            Assert.That(source.State.Position.X,Is.EqualTo(4.5));Assert.That(source.State.Position.Y,Is.EqualTo(1));Assert.That(source.State.Position.Z,Is.EqualTo(23.3));
            Assert.That(Find("c2.wall.left").GetComponent<BoxCollider>().bounds.max.x,Is.EqualTo(4).Within(1e-5));
            Assert.That(Find("c2.wall.right").GetComponent<BoxCollider>().bounds.min.x,Is.EqualTo(5).Within(1e-5));
            Assert.That(Find("c2.wall.sill").GetComponent<BoxCollider>().bounds.max.y,Is.EqualTo(.75).Within(1e-5));
            Assert.That(Find("c2.wall.header").GetComponent<BoxCollider>().bounds.min.y,Is.EqualTo(1.45).Within(1e-5));
            var ray=new Ray(new Vector3(4.5f,1.1f,24),Vector3.forward);
            foreach(var key in new[]{"c2.wall.left","c2.wall.right","c2.wall.sill","c2.wall.header"})
                Assert.That(Find(key).GetComponent<Collider>().Raycast(ray,out _,2),Is.False,"Actual open window: "+key);
            Assert.That(Find("c2.wall.header").GetComponent<Collider>().Raycast(new Ray(new Vector3(4.5f,1.6f,24),Vector3.forward),out _,2),Is.True);
        }
        [Test] public void C1C3KeepApprovedCoordinatesAndAllEightSourcesShareTheSameWorld()
        {
            var world=Object.FindFirstObjectByType<WorldSliceState>();Assert.That(world.Sources.Select(v=>v.Id),Is.EquivalentTo(new[]{"B1","C1","C2","C3","B2.P1","B2.P2","B3.U","B3.V"}));
            Assert.That(Object.FindObjectsByType<FourDimensionalBody>(FindObjectsSortMode.None).Select(b=>b.Id),Is.EquivalentTo(new[]{"C1","C2","C3"}));
            Assert.That(Object.FindObjectsByType<TestStation>(FindObjectsSortMode.None).Select(s=>s.Id),Is.EquivalentTo(new[]{"A1","A2","A3","B1","B2","B3","C1","C2","C3"}));
            foreach(var source in world.Sources)Assert.That(source.World,Is.SameAs(world));
            Assert.That(Find("c1.pedestal").GetComponent<BoxCollider>().bounds.center,Is.EqualTo(new Vector3(-4,.5f,23.4f)));
            Assert.That(Vector3.Distance(Find("c1.pedestal").GetComponent<BoxCollider>().bounds.size,new Vector3(2.2f,1,2.2f)),Is.LessThan(1e-4f));
            Assert.That(Find("c3.contact.a").position,Is.EqualTo(new Vector3(-4.8f,1.2f,27)));Assert.That(Find("c3.contact.b").position,Is.EqualTo(new Vector3(-3.2f,1.2f,27)));
            var connector=Find("geometry.C3").GetComponent<GeometrySliceView>();Assert.That(connector.State.Position.W,Is.EqualTo(.7));Assert.That(connector.PrismHalfSize.W,Is.EqualTo(1));
        }
    }
}
