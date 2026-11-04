using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Section.Geometry4D;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
namespace Section.Sandbox.Tests
{
    public sealed class WorldShiftTests
    {
        Keyboard keyboard;Mouse mouse;readonly List<InputDevice> disabled=new List<InputDevice>();
        bool background;InputSettings.BackgroundBehavior backgroundInput;InputSettings.EditorInputBehaviorInPlayMode editorInput;
        SandboxScene sandbox;WorldSliceState world;WorldShiftDevice device;StandardManipulator standard;EquipmentSlots slots;
        GeometrySliceView Source(string id)=>world.Sources.Single(s=>s.Id==id);
        TestStation Station(string id)=>UnityEngine.Object.FindObjectsByType<TestStation>(FindObjectsSortMode.None).Single(s=>s.Id==id);
        [UnitySetUp] public IEnumerator SetUp()
        {
            background=Application.runInBackground;backgroundInput=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach(var d in InputSystem.devices.ToArray())if(d.enabled&&(d is Keyboard||d is Mouse)){InputSystem.DisableDevice(d);disabled.Add(d);}
            keyboard=InputSystem.AddDevice<Keyboard>("WorldShiftTestKeyboard");mouse=InputSystem.AddDevice<Mouse>("WorldShiftTestMouse");keyboard.MakeCurrent();mouse.MakeCurrent();
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Dev/Sandbox_01.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor scene integration test.");
#endif
            sandbox=UnityEngine.Object.FindFirstObjectByType<SandboxScene>();world=UnityEngine.Object.FindFirstObjectByType<WorldSliceState>();
            device=UnityEngine.Object.FindFirstObjectByType<WorldShiftDevice>();standard=UnityEngine.Object.FindFirstObjectByType<StandardManipulator>();slots=UnityEngine.Object.FindFirstObjectByType<EquipmentSlots>();
            yield return new WaitForSeconds(.25f);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            foreach(var d in disabled)if(d.added)InputSystem.EnableDevice(d);disabled.Clear();
            InputSystem.settings.backgroundBehavior=backgroundInput;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;Application.runInBackground=background;
            yield return null;
        }
        void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        IEnumerator Tap(Key key){Keys(key);yield return null;Keys();yield return null;}
        void PlayerFixture(Vector3 feet){sandbox.Player.RestoreSafePose(feet,Quaternion.identity);sandbox.Look.ResetPitch();Physics.SyncTransforms();}
        string State()=>"feet="+sandbox.Player.transform.position+" W="+world.W+" grounded="+sandbox.Player.Controller.isGrounded+" blocked="+world.LastBlockedReason;
        IEnumerator WalkTo(Vector3 target)
        {
            float until=Time.time+15;
            while(Time.time<until)
            {
                Vector3 delta=target-sandbox.Player.transform.position;delta.y=0;if(delta.magnitude<.09f)break;
                Vector3 local=sandbox.Player.transform.InverseTransformDirection(delta.normalized);var keys=new List<Key>();
                if(local.x>.35f)keys.Add(Key.D);if(local.x<-.35f)keys.Add(Key.A);if(local.z>.35f)keys.Add(Key.W);if(local.z<-.35f)keys.Add(Key.S);
                Keys(keys.ToArray());yield return null;
            }
            Keys();yield return null;var error=target-sandbox.Player.transform.position;error.y=0;
            Assert.That(error.magnitude,Is.LessThan(.16f),"Walk: "+State());
        }
        IEnumerator Aim(Vector3 point)
        {
            float until=Time.time+4;
            while(Time.time<until)
            {
                Vector3 direction=point-sandbox.Look.View.position;
                float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,pitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
                float dx=Mathf.DeltaAngle(sandbox.Player.transform.eulerAngles.y,yaw),dy=Mathf.DeltaAngle(Mathf.DeltaAngle(0,sandbox.Look.View.localEulerAngles.x),pitch);
                if(Mathf.Abs(dx)<.15f && Mathf.Abs(dy)<.15f)break;
                InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(Mathf.Clamp(dx,-5,5)/.12f,-Mathf.Clamp(dy,-5,5)/.12f)});yield return null;
            }
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
        }
        IEnumerator FaceForward()=>Aim(sandbox.Look.View.position+Vector3.forward*8);
        IEnumerator JumpTo(float z)
        {
            yield return FaceForward();Keys(Key.W,Key.Space);yield return null;Keys(Key.W);
            float until=Time.time+2;
            while(Time.time<until && sandbox.Player.transform.position.z<z-.04f){yield return null;}
            Keys();yield return new WaitForSeconds(.65f);
        }
        [UnityTest] public IEnumerator DeviceSwitchingAndBothDirectionsUseTheAgreedInputWithoutCrossActivation()
        {
            Assert.That(slots.SelectedSlot,Is.EqualTo(1));yield return Tap(Key.Digit3);
            Assert.That(device.IsEquipped,Is.True);Assert.That(device.EquippedModel.activeInHierarchy,Is.True);Assert.That(standard.IsEquipped,Is.False);
            double before=world.W;Keys(Key.E);yield return new WaitForSeconds(.3f);Keys();yield return null;
            Assert.That(world.W-before,Is.InRange(.25,.4));double peak=world.W;
            Keys(Key.Q);yield return new WaitForSeconds(.2f);Keys();yield return null;Assert.That(world.W,Is.LessThan(peak-.15));
            double wheelStart=world.W;InputSystem.QueueStateEvent(mouse,new MouseState{scroll=new Vector2(0,120)});yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.2f);
            Assert.That(world.W-wheelStart,Is.EqualTo(.1).Within(.005));
            yield return Tap(Key.Digit1);before=world.W;yield return Tap(Key.E);Assert.That(world.W,Is.EqualTo(before));
            Assert.That(device.EquippedModel.activeInHierarchy,Is.False);Assert.That(standard.IsEquipped,Is.True);
            yield return Tap(Key.Digit2);Assert.That(slots.SelectedSlot,Is.EqualTo(2),"M5 makes slot 2 available without changing W.");
            foreach(var source in world.Sources)Assert.That(source.SliceW,Is.EqualTo(world.W));
        }
        [UnityTest] public IEnumerator EveryBoundaryHasSynchronizedMeshColliderAndNoPhantomSolid()
        {
            foreach(double w in new[]{-2,-1.6,-.8,0,.8,1.6,2})
            {
                Assert.That(world.TrySetW(w),Is.True,world.LastBlockedReason);yield return null;
                foreach(var source in world.Sources)
                {
                    Assert.That(source.SliceW,Is.EqualTo(world.W));var colliders=source.GetComponent<SliceColliderView>();
                    Assert.That(colliders.Revision,Is.EqualTo(source.MeshBuildCount));
                    Assert.That(colliders.ActiveCollider!=null,Is.EqualTo(source.RenderStatus==SliceRenderStatus.Ready),source.Id+" W "+w);
                    if(source.Shape==Shape4D.Hypersphere && colliders.ActiveCollider is SphereCollider sphere)
                        Assert.That(sphere.radius,Is.EqualTo(source.CurrentSlice.AnalyticRadius).Within(1e-6));
                    if(colliders.ActiveCollider is MeshCollider solid)
                    {
                        Assert.That(solid.sharedMesh,Is.SameAs(colliders.GeneratedColliderMesh));
                        Assert.That(solid.sharedMesh.vertices,Is.EqualTo(source.GeneratedMesh.vertices));
                        var expected=source.GeneratedMesh.bounds;var actual=solid.bounds;
                        Assert.That(Vector3.Distance(actual.center,source.transform.TransformPoint(expected.center)),Is.LessThan(.003f),source.Id);
                        Assert.That(Vector3.Distance(actual.size,expected.size),Is.LessThan(.003f),source.Id);
                        Assert.That(solid.Raycast(new Ray(actual.center+Vector3.up*(actual.extents.y+1),Vector3.down),out var hit,3),Is.True,source.Id);
                        Assert.That(hit.point.y,Is.EqualTo(actual.max.y).Within(.003f));
                    }
                }
            }
            var body=Source("B1");Assert.That(body.GetComponent<SliceColliderView>().ActiveCollider,Is.Null);
            Assert.That(Physics.RaycastAll(new Vector3(-4,1.9f,10),Vector3.forward,4).Any(h=>h.collider.GetComponent<GeometrySliceView>()==body),Is.False);
            Assert.That(world.TrySetW(0),Is.True);Assert.That(body.GetComponent<SliceColliderView>().ActiveCollider,Is.Not.Null);
            body.enabled=false;yield return null;Assert.That(body.GetComponent<SliceColliderView>().ActiveCollider,Is.Null);
            body.enabled=true;yield return null;Assert.That(body.GetComponent<SliceColliderView>().ActiveCollider,Is.Not.Null);
        }
        [UnityTest] public IEnumerator ABlockedInteriorStopsTheContinuousTransitionInBothDirections()
        {
            Assert.That(world.TrySetW(-2),Is.True);PlayerFixture(new Vector3(-4,.4f,13.2f));yield return new WaitForSeconds(.1f);
            Assert.That(world.TrySetW(2),Is.False);Assert.That(world.W,Is.InRange(-1.601,-1.59));Assert.That(world.LastBlockedReason,Does.Contain("B1"));
            Assert.That(world.TrySetW(-2),Is.True);PlayerFixture(new Vector3(0,0,11));Assert.That(world.TrySetW(2),Is.True);
            PlayerFixture(new Vector3(-4,.4f,13.2f));yield return new WaitForSeconds(.1f);
            Assert.That(world.TrySetW(-2),Is.False);Assert.That(world.W,Is.InRange(1.59,1.601));
            Assert.That(world.TrySetW(2),Is.True);
            foreach(var source in world.Sources)Assert.That(source.SliceW,Is.EqualTo(world.W));
        }
        [UnityTest] public IEnumerator ThinIntermediatePrismCannotBeSkippedByWheelOrLargeDiagnosticRequest()
        {
            Assert.That(world.TrySetW(-2),Is.True);var originals=world.Sources;
            var node=new GameObject("Thin4DSweepFixture");var source=node.AddComponent<GeometrySliceView>();
            source.ConfigurePrism("test.thin",new Vector4D(0,.9,1.5,.1234567),new Vector4D(.3,.3,.3,1e-6),default);
            node.AddComponent<SliceColliderView>();world.Configure(world.Settings,sandbox.Player,originals.Concat(new[]{source}).ToArray(),UnityEngine.Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None));
            Assert.That(world.TrySetW(2),Is.False);Assert.That(world.W,Is.LessThan(.1234567));Assert.That(world.LastBlockedReason,Does.Contain("test.thin"));
            world.Configure(world.Settings,sandbox.Player,originals,UnityEngine.Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None));UnityEngine.Object.Destroy(node);yield return null;
            Assert.That(world.TrySetW(2),Is.True);
        }
        [UnityTest] public IEnumerator B2CanBeCrossedFromSpawnUsingOnlyMovementJumpAndWorldShiftInput()
        {
            // No player/body pose overrides or direct W setters in this end-to-end route.
            yield return Tap(Key.Digit3);yield return WalkTo(new Vector3(0,0,11.5f));yield return WalkTo(new Vector3(4.25f,0,11.3f));yield return WalkTo(new Vector3(4.25f,0,12.5f));
            yield return JumpTo(14.04f);var condition=(SpatialPlatformCondition)Station("B2").Condition;
            Assert.That(condition.TouchedFirst,Is.True,"P1: "+State());yield return WalkTo(new Vector3(4.25f,0,14.10f));
            yield return FaceForward();Keys(Key.W,Key.E,Key.Space);yield return null;Keys(Key.W,Key.E);
            float until=Time.time+1.6f;while(!condition.TouchedSecond && Time.time<until)yield return null;
            Keys();yield return null;Assert.That(condition.TouchedSecond,Is.True,"P2: "+State());Assert.That(world.W,Is.GreaterThan(.4));
            Keys(Key.W);float ontoUntil=Time.time+.4f;while(sandbox.Player.transform.position.z<17.05f && Time.time<ontoUntil)yield return null;Keys();yield return null;
            string landing=State()+" support="+sandbox.Player.GetComponent<PlayerSupportTracker>().Current;
            Assert.That(condition.FallCount,Is.EqualTo(0),"Fall before P2 "+landing);
            float settleUntil=Time.time+1;while(!sandbox.Player.Controller.isGrounded && Time.time<settleUntil)yield return null;
            yield return new WaitForSeconds(.1f);
            Assert.That(condition.FallCount,Is.EqualTo(0),"Fall while settling P2: "+landing+"; "+State());
            yield return JumpTo(18.4f);yield return new WaitForSeconds(.15f);
            Assert.That(Station("B2").State,Is.EqualTo(StationState.Passed),"P2 landing: "+landing+"; exit: "+State()+" falls="+condition.FallCount);
            var c1=Source("C1").State;Assert.That(Station("B2").TryReset(),Is.True,Station("B2").LastResetMessage);
            Assert.That(world.W,Is.EqualTo(0));Assert.That(sandbox.Player.transform.position,Is.EqualTo(new Vector3(4.25f,0,12)));
            Assert.That(sandbox.Player.VerticalVelocity,Is.EqualTo(0));Assert.That(condition.TouchedFirst,Is.False);
            Assert.That((Source("C1").State.Position-c1.Position).Length,Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator FixedWAttemptFallsAndLocallyRestoresOnlyB2()
        {
            PlayerFixture(new Vector3(4.25f,0,12.5f));yield return new WaitForSeconds(.1f);yield return Tap(Key.Digit3);
            yield return JumpTo(14.04f);yield return WalkTo(new Vector3(4.25f,0,14.10f));
            var condition=(SpatialPlatformCondition)Station("B2").Condition;
            Assert.That(condition.TouchedFirst,Is.True);yield return FaceForward();Keys(Key.W,Key.Space);yield return null;Keys(Key.W);
            float until=Time.time+3;while(condition.FallCount==0 && Time.time<until)yield return null;Keys();yield return null;
            Assert.That(condition.FallCount,Is.GreaterThan(0),State());Assert.That(world.W,Is.EqualTo(0));
            Assert.That(sandbox.Player.transform.position.z,Is.InRange(11.95f,12.2f));Assert.That(Station("B2").State,Is.Not.EqualTo(StationState.Passed));
            Assert.That(Station("B1").State,Is.EqualTo(StationState.Passed));Assert.That(Station("A1").State,Is.EqualTo(StationState.Ready));
        }
        [UnityTest] public IEnumerator B3UsesPhysicalSensorContactsAndKeepsHistoryIndependentOfLiveWAndB2Reset()
        {
            yield return Tap(Key.Digit3);yield return WalkTo(new Vector3(0,0,11.5f));yield return WalkTo(new Vector3(-1,0,16.5f));
            yield return Aim(new Vector3(-1.75f,.7f,17));yield return Tap(Key.F);
            var condition=(DualSliceCondition)Station("B3").Condition;
            Assert.That(condition.Armed,Is.True,Station("B3").LastResetMessage);Assert.That(world.W,Is.EqualTo(-.8));Assert.That(condition.LiveBoth,Is.False);
            Keys(Key.E);float until=Time.time+2;while(world.W < -.07 && Time.time<until)yield return null;Keys();yield return new WaitForSeconds(.3f);
            Assert.That(condition.LiveBoth,Is.True);Assert.That(Station("B3").State,Is.EqualTo(StationState.Passed),condition.Reading);
            Keys(Key.E);until=Time.time+2;while(world.W<.7 && Time.time<until)yield return null;Keys();yield return null;
            Assert.That(condition.LiveBoth,Is.False);Assert.That(Station("B3").CurrentSuccess,Is.False);Assert.That(Station("B3").State,Is.EqualTo(StationState.Passed));
            Assert.That(Station("B2").TryReset(),Is.True);yield return null;
            Assert.That(world.W,Is.EqualTo(0));Assert.That(condition.LiveBoth,Is.True);Assert.That(Station("B3").State,Is.EqualTo(StationState.Passed));
            Assert.That(Station("B3").TryReset(),Is.True);Assert.That(condition.Armed,Is.False);Assert.That(condition.LiveBoth,Is.True,"Reset may not freeze outdated sensor values.");
        }
        [UnityTest] public IEnumerator LocalRecoveryRefusesOccupiedSpawnWithoutMovingForeignObjectsOrChangingW()
        {
            var item=UnityEngine.Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None).Single(i=>i.Id=="a1.cube");
            Assert.That(world.TrySetW(.7),Is.True);item.Body.position=new Vector3(4.25f,.3f,12);item.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
            Vector3 before=item.Body.position;Assert.That(Station("B2").TryReset(),Is.False);Assert.That(Station("B2").LastResetMessage,Does.Contain("занято"));
            Assert.That(item.Body.position,Is.EqualTo(before));Assert.That(world.W,Is.EqualTo(.7));
            item.RestoreInitial();Physics.SyncTransforms();Assert.That(Station("B2").TryReset(),Is.True);yield return null;
            Assert.That(world.W,Is.EqualTo(0));Assert.That(item.Body.position.x,Is.EqualTo(-4).Within(.01f));
        }
        [UnityTest] public IEnumerator TheCommonPlatformWIntervalStillRequiresChangingWDuringTheCrossing()
        {
            Assert.That(world.TrySetW(.5),Is.True);PlayerFixture(new Vector3(4.25f,0,12.5f));yield return new WaitForSeconds(.1f);
            yield return JumpTo(13.35f);yield return WalkTo(new Vector3(4.25f,0,13.4f));
            var condition=(SpatialPlatformCondition)Station("B2").Condition;
            Assert.That(condition.TouchedFirst,Is.True,State());yield return FaceForward();
            Keys(Key.W,Key.Space);yield return null;Keys(Key.W);float until=Time.time+3;
            while(condition.FallCount==0 && Time.time<until)yield return null;Keys();yield return null;
            Assert.That(condition.FallCount,Is.GreaterThan(0),State());Assert.That(Station("B2").State,Is.Not.EqualTo(StationState.Passed));
            Assert.That(world.W,Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator AnOrdinaryRigidbodyAlsoBlocksGrowingGeometryWithoutBeingTeleported()
        {
            Assert.That(world.TrySetW(2),Is.True);
            var item=UnityEngine.Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None).Single(i=>i.Id=="a1.cube");
            item.Body.position=new Vector3(-4,1.9f,13.2f);item.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
            var before=item.Body.position;Assert.That(world.TrySetWExactly(0,out string reason),Is.False);Assert.That(reason,Does.Contain(item.Id));
            Assert.That(world.W,Is.EqualTo(2));Assert.That(item.Body.position,Is.EqualTo(before));
            Assert.That(world.TrySetW(0),Is.False);Assert.That(world.W,Is.GreaterThan(1.5));Assert.That(item.Body.position,Is.EqualTo(before));
            item.RestoreInitial();Physics.SyncTransforms();Assert.That(world.TrySetW(0),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator SelectingWorldShiftSafelyReleasesAnOrdinaryHeldItemAndRetainsItsPhysics()
        {
            yield return WalkTo(new Vector3(-4,0,1.4f));var item=UnityEngine.Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None).Single(i=>i.Id=="a1.cube");
            yield return Aim(item.Body.position);yield return Tap(Key.E);Assert.That(standard.Held,Is.SameAs(item));
            yield return Tap(Key.Digit3);Assert.That(standard.Held,Is.Null);Assert.That(item.IsHeld,Is.False);Assert.That(item.Body.isKinematic,Is.False);Assert.That(item.Body.detectCollisions,Is.True);
            Keys(Key.E);yield return new WaitForSeconds(.2f);Keys();yield return null;double w=world.W;
            yield return Tap(Key.Digit1);Assert.That(world.W,Is.EqualTo(w));Assert.That(standard.IsEquipped,Is.True);
        }
        [UnityTest] public IEnumerator TwentyLocalRepeatsKeepColliderBuffersBoundedAndOtherStationHistoryIntact()
        {
            var b3=(DualSliceCondition)Station("B3").Condition;
            Assert.That(Station("B3").TryBegin(),Is.True);Assert.That(world.TrySetW(0),Is.True);yield return new WaitForSeconds(.3f);
            Assert.That(Station("B3").State,Is.EqualTo(StationState.Passed));
            int objects=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            for(int i=0;i<20;i++)
            {
                Assert.That(world.TrySetW(i%2==0?.5:.8),Is.True,world.LastBlockedReason);
                Assert.That(Station("B2").TryReset(),Is.True,Station("B2").LastResetMessage);
                Assert.That(world.W,Is.EqualTo(0));Assert.That(b3.LiveBoth,Is.True);
                Assert.That(Station("B3").State,Is.EqualTo(StationState.Passed));yield return null;
            }
            Assert.That(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,Is.EqualTo(objects));
            Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Count(m=>m.name=="C1_ColliderSlice" || m.name=="B2.P1_ColliderSlice" || m.name=="B2.P2_ColliderSlice"),Is.EqualTo(6),"Two reusable buffers per polygonal source; spheres use analytic colliders.");
        }
        [UnityTest] public IEnumerator BlockedWResetStillReturnsAFallenPlayerToTheBankWithoutMovingTheBlocker()
        {
            Assert.That(world.TrySetW(2),Is.True);
            var item=UnityEngine.Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None).Single(i=>i.Id=="a1.cube");
            item.Body.position=new Vector3(-4,.7f,13.2f);item.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
            PlayerFixture(new Vector3(4.25f,-2,15.5f));yield return new WaitForSeconds(.2f);
            Assert.That(sandbox.Player.transform.position.y,Is.GreaterThan(-.05f));Assert.That(sandbox.Player.transform.position.z,Is.LessThan(13));
            Assert.That(world.W,Is.EqualTo(2));Assert.That(Station("B2").LastResetMessage,Does.Contain("на берегу"));
            Assert.That(Station("B2").State,Is.EqualTo(StationState.Failed));Assert.That(item.Body.position.x,Is.EqualTo(-4).Within(.01f));
            Assert.That(item.Body.position.z,Is.EqualTo(13.2f).Within(.01f));
            item.RestoreInitial();Physics.SyncTransforms();Assert.That(Station("B2").TryReset(),Is.True);Assert.That(world.W,Is.EqualTo(0));
        }
    }
}
