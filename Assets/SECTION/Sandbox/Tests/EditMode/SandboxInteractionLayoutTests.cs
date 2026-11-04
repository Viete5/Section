using System.Linq;
using NUnit.Framework;
using Section.Sandbox.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Section.Sandbox.Tests
{
    public sealed class SandboxInteractionLayoutTests
    {
        Scene scene;
        [SetUp] public void SetUp(){scene=EditorSceneManager.OpenScene(SandboxSceneBuilder.ScenePath,OpenSceneMode.Single);Physics.SyncTransforms();}
        Transform Find(string id)=>SandboxSceneBuilder.Find(scene,id).transform;
        [Test]
        public void AllConventionalFixturesMatchApprovedCoordinatesAndDimensions()
        {
            Check("a1.table",new Vector3(-4,.45f,3),new Vector3(1.8f,.9f,1.4f));
            Check("a1.plate",new Vector3(-4,.06f,5.5f),new Vector3(1.2f,.12f,1.2f));
            Check("a2.plate",new Vector3(4,.06f,5.5f),new Vector3(1.4f,.12f,1.4f));
            Item("a1.cube",new Vector3(-4,1.2f,3),Vector3.one*.6f,2);
            Item("a2.light",new Vector3(3.3f,.25f,3),Vector3.one*.5f,1);
            Item("a2.heavy",new Vector3(4.7f,.35f,3),Vector3.one*.7f,5);
            Item("a3.block",new Vector3(-5.1f,.275f,8.1f),new Vector3(.45f,.55f,1.4f),2);
            Assert.That(Find("a2.plate").GetComponent<LoadPlateSensor>().MinimumKilograms,Is.EqualTo(4));
            var stations=Object.FindObjectsByType<TestStation>(FindObjectsSortMode.None);
            Assert.That(stations.Where(s=>s.Id.StartsWith("A")).Select(s=>s.Id),Is.EquivalentTo(new[]{"A1","A2","A3"}));
            Assert.That(UnityEditor.AssetDatabase.GetDependencies(SandboxSceneBuilder.ScenePath),
                Does.Contain(SandboxSceneBuilder.AssetRoot+"/Runtime/Presentation/WorldText.shader"),"Runtime text shader must be a serialized build dependency.");
        }
        [Test]
        public void SocketHasTheRealSpecifiedOpenChannelAndInitialItemsDoNotPenetrateFixtures()
        {
            var socket=Find("a3.socket").GetComponent<SocketSensor>();
            Assert.That(socket.transform.position,Is.EqualTo(new Vector3(-3.025f,.9f,8.1f)));
            Assert.That(socket.CavitySize,Is.EqualTo(new Vector3(1.55f,.65f,.65f)));
            Assert.That(Find("a3.socket.floor").GetComponent<Collider>().bounds.max.y,Is.EqualTo(.575f).Within(.0001f));
            Assert.That(Find("a3.socket.ceiling").GetComponent<Collider>().bounds.min.y,Is.EqualTo(1.225f).Within(.0001f));
            Assert.That(Find("a3.socket.near").GetComponent<Collider>().bounds.max.z,Is.EqualTo(7.775f).Within(.0001f));
            Assert.That(Find("a3.socket.far").GetComponent<Collider>().bounds.min.z,Is.EqualTo(8.425f).Within(.0001f));
            Assert.That(Find("a3.socket.back").GetComponent<Collider>().bounds.min.x,Is.EqualTo(-2.25f).Within(.0001f));
            foreach(var item in Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None))
                foreach(var other in Physics.OverlapBox(item.InitialPosition,item.Shape.size*.5f,item.InitialRotation))
                    if(other!=item.Shape)
                        Assert.That(Physics.ComputePenetration(item.Shape,item.InitialPosition,item.InitialRotation,other,
                            other.transform.position,other.transform.rotation,out _,out float depth)&&depth>.001f,Is.False,item.Id+" penetrates "+other.name);
            // An axial probe can enter the cavity; its first solid hit is the back stop.
            Assert.That(Physics.Raycast(new Vector3(-4,.9f,8.1f),Vector3.right,out var hit,2),Is.True);
            Assert.That(hit.collider,Is.SameAs(Find("a3.socket.back").GetComponent<Collider>()));
        }
        void Check(string id,Vector3 position,Vector3 size)
        {var c=Find(id).GetComponent<BoxCollider>();Assert.That(c.bounds.center,Is.EqualTo(position));Assert.That(Vector3.Distance(c.bounds.size,size),Is.LessThan(.0001f));}
        void Item(string id,Vector3 position,Vector3 size,float mass)
        {
            var item=Find(id).GetComponent<PhysicalItem>();Assert.That(item.InitialPosition,Is.EqualTo(position));
            Assert.That(item.Shape.size,Is.EqualTo(size));Assert.That(item.Body.mass,Is.EqualTo(mass));Assert.That(item.Body.isKinematic,Is.False);
            Assert.That(item.Body.useGravity&&item.Body.detectCollisions,Is.True);
        }
    }
}
