using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
    public sealed class SandboxInteractionTests
    {
        Keyboard keyboard;
        Mouse mouse;
        readonly List<InputDevice> disabled = new List<InputDevice>();
        bool background;
        InputSettings.BackgroundBehavior inputBackground;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        SandboxScene sandbox;
        StandardManipulator device;
        PhysicalItem[] items;
        TestStation[] stations;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            background=Application.runInBackground; inputBackground=InputSystem.settings.backgroundBehavior;
            editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach(var d in InputSystem.devices.ToArray())if(d.enabled&&(d is Keyboard||d is Mouse)){InputSystem.DisableDevice(d);disabled.Add(d);}
            keyboard=InputSystem.AddDevice<Keyboard>("InteractionTestKeyboard");mouse=InputSystem.AddDevice<Mouse>("InteractionTestMouse");
            keyboard.MakeCurrent();mouse.MakeCurrent();
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Dev/Sandbox_01.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor authoring scene test.");
#endif
            sandbox=Object.FindFirstObjectByType<SandboxScene>();device=Object.FindFirstObjectByType<StandardManipulator>();
            items=Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None);
            stations=Object.FindObjectsByType<TestStation>(FindObjectsSortMode.None).Where(s=>s.Id.StartsWith("A")).ToArray();
            yield return new WaitForSeconds(.3f);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            foreach(var d in disabled)if(d.added)InputSystem.EnableDevice(d);disabled.Clear();
            InputSystem.settings.backgroundBehavior=inputBackground;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            Application.runInBackground=background;yield return null;
        }
        PhysicalItem Item(string id)=>items.Single(i=>i.Id==id);
        TestStation Station(string id)=>stations.Single(s=>s.Id==id);
        void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        IEnumerator Tap(Key key){Keys(key);yield return null;Keys();yield return null;}
        void Pose(PhysicalItem item,Vector3 position,Quaternion rotation)
        {if(item.IsHeld)device.Release();item.Body.position=position;item.Body.rotation=rotation;item.Body.linearVelocity=Vector3.zero;item.Body.angularVelocity=Vector3.zero;item.Body.WakeUp();Physics.SyncTransforms();}
        void PlayerAt(Vector3 position)
        {sandbox.Player.Controller.enabled=false;sandbox.Player.transform.position=position;sandbox.Player.Controller.enabled=true;Physics.SyncTransforms();}
        IEnumerator Aim(Vector3 point)
        {
            float until=Time.time+4;
            while(Time.time<until)
            {
                Vector3 direction=point-sandbox.Look.View.position;
                float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
                float pitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
                float dx=Mathf.DeltaAngle(sandbox.Player.transform.eulerAngles.y,yaw);
                float dy=Mathf.DeltaAngle(Mathf.DeltaAngle(0,sandbox.Look.View.localEulerAngles.x),pitch);
                if(Mathf.Abs(dx)<.1f&&Mathf.Abs(dy)<.1f)break;
                InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(Mathf.Clamp(dx,-4,4)/.12f,-Mathf.Clamp(dy,-4,4)/.12f)});
                yield return new WaitForSeconds(.02f);
            }
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
        }
        IEnumerator WalkTo(Vector3 feet)
        {
            float until=Time.time+12;
            while(Time.time<until)
            {
                Vector3 delta=feet-sandbox.Player.transform.position;delta.y=0;
                if(delta.magnitude<.09f)break;
                Vector3 local=sandbox.Player.transform.InverseTransformDirection(delta.normalized);
                var keys=new List<Key>();
                if(local.z>.35f)keys.Add(Key.W);if(local.z<-.35f)keys.Add(Key.S);
                if(local.x>.35f)keys.Add(Key.D);if(local.x<-.35f)keys.Add(Key.A);
                Keys(keys.ToArray());yield return null;
            }
            Keys();yield return null;
            Vector3 difference=feet-sandbox.Player.transform.position;difference.y=0;
            Assert.That(difference.magnitude,Is.LessThan(.14f),"Walking blocked: "+sandbox.Player.transform.position+" target "+feet);
        }
        IEnumerator TargetHeld(Vector3 center)
        {
            Assert.That(device.Held,Is.Not.Null,"Capture lost before aiming held object: "+device.Feedback);
            Vector3 grab=center+device.Held.Body.rotation*device.GrabPointLocal;
            float distance=Mathf.Clamp(Vector3.Distance(sandbox.Look.View.position,grab),device.Settings.minimumDistance,device.Settings.maximumDistance);
            InputSystem.QueueStateEvent(mouse,new MouseState{scroll=new Vector2(0,(distance-device.HoldDistance)/device.Settings.distancePerNotch*120)});
            yield return null;
            yield return Aim(grab);
            yield return new WaitForSeconds(.8f);
            Assert.That(device.Held,Is.Not.Null,"Capture lost while positioning: "+Diagnostic());
        }
        string Diagnostic()
        {
            device.TryGetViewHit(out var hit);
            return device.Feedback + " hit=" + (hit.collider == null ? "none" : hit.collider.name) +
                " feet=" + sandbox.Player.transform.position + " pitch=" + sandbox.Look.View.localEulerAngles.x +
                " error=" + device.LastPositionError + " occluder=" + device.LastOccluder + " A3="+Item("a3.block").Body.position+" rot="+Item("a3.block").Body.rotation.eulerAngles+" grab="+device.GrabPointLocal;
        }
        IEnumerator WaitSuccess(string id)
        {
            float until=Time.time+4;
            while(!Station(id).CurrentSuccess&&Time.time<until)yield return null;
            Assert.That(Station(id).CurrentSuccess,Is.True,id+": "+Station(id).Condition.Reading);
        }

        [UnityTest]
        public IEnumerator EveryApprovedItemCanBeCapturedAndReleasedWithoutChangingItsPhysicsMode()
        {
            Assert.That(items.Length,Is.EqualTo(4));
            foreach(var item in items)
            {
                // Isolated acquisition fixtures use direct player setup; the end-to-end case below does not.
                PlayerAt(item.Id=="a3.block"?new Vector3(-5.1f,0,6.3f):new Vector3(item.InitialPosition.x,0,1.4f));
                yield return Aim(item.Body.position);yield return Tap(Key.Digit1);yield return Tap(Key.E);
                Assert.That(device.Held,Is.SameAs(item),item.Id+" capture: "+Diagnostic());
                Assert.That(item.Body.isKinematic,Is.False);Assert.That(item.Body.useGravity,Is.True);Assert.That(item.Body.detectCollisions,Is.True);
                yield return Tap(Key.E);Assert.That(device.Held,Is.Null);Assert.That(item.IsHeld,Is.False);
            }
            PlayerAt(new Vector3(0,0,2));yield return Aim(new Vector3(0,1,0));yield return Tap(Key.E);
            Assert.That(device.Held,Is.Null,"Wall must not be capturable.");
        }
        [UnityTest]
        public IEnumerator A1RequiresSettledUnheldContactAndImmediatelyLosesLiveSuccessOnRemoval()
        {
            var cube=Item("a1.cube");var sensor=(LoadPlateSensor)Station("A1").Condition;
            Pose(cube,new Vector3(-4,.44f,5.5f),Quaternion.identity);
            yield return new WaitForSeconds(.12f);Assert.That(sensor.IsSatisfied,Is.False);
            yield return WaitSuccess("A1");Assert.That(sensor.SupportedKilograms,Is.EqualTo(2).Within(.12f));
            yield return new WaitForSeconds(1);Assert.That(sensor.IsSatisfied,Is.True,"Sleeping contact must remain valid.");
            PlayerAt(new Vector3(-4,0,4));yield return Aim(cube.Body.position);yield return Tap(Key.E);
            Assert.That(device.Held,Is.SameAs(cube),Diagnostic());
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();Assert.That(sensor.IsSatisfied,Is.False);
            Assert.That(Station("A1").State,Is.EqualTo(StationState.Passed),"History and live contact are separate.");
            device.Release();Pose(cube,new Vector3(-4,1.5f,5.5f),Quaternion.identity);
            yield return new WaitForSeconds(.08f);Assert.That(sensor.IsSatisfied,Is.False,"A falling cube is not settled.");
        }
        [UnityTest]
        public IEnumerator A2UsesRealLoadIncludingStacksAndRejectsLightCubeAndHeldPressure()
        {
            var light=Item("a2.light");var heavy=Item("a2.heavy");var cube=Item("a1.cube");
            var sensor=(LoadPlateSensor)Station("A2").Condition;
            Pose(light,new Vector3(4,.38f,5.5f),Quaternion.identity);
            yield return new WaitForSeconds(1);Assert.That(sensor.IsSatisfied,Is.False);Assert.That(sensor.SupportedKilograms,Is.EqualTo(1).Within(.12f));
            Pose(cube,new Vector3(4,.93f,5.5f),Quaternion.identity);
            yield return new WaitForSeconds(1);Assert.That(sensor.IsSatisfied,Is.False);Assert.That(sensor.SupportedKilograms,Is.EqualTo(3).Within(.2f));
            Pose(heavy,new Vector3(4,1.59f,5.5f),Quaternion.identity);
            yield return WaitSuccess("A2");Assert.That(sensor.SupportedKilograms,Is.EqualTo(8).Within(.4f));
            PlayerAt(new Vector3(4,0,3.5f));yield return Aim(heavy.Body.position);yield return Tap(Key.E);
            Assert.That(device.Held,Is.SameAs(heavy));yield return new WaitForSeconds(.15f);
            Assert.That(sensor.IsSatisfied,Is.False,"A stack assisted by the manipulator is not a valid weight reading.");
        }
        [UnityTest]
        public IEnumerator A2DoesNotCountOtherRoomMassOrAShortImpact()
        {
            var light=Item("a2.light");var sensor=(LoadPlateSensor)Station("A2").Condition;
            Pose(light,new Vector3(4,2.3f,5.5f),Quaternion.identity);light.Body.linearVelocity=Vector3.down*5;
            float until=Time.time+2;
            while(Time.time<until){Assert.That(sensor.IsSatisfied,Is.False,"A 1 kg impact must never pass 4 kg threshold.");yield return null;}
            Assert.That(sensor.SupportedKilograms,Is.EqualTo(1).Within(.15f));
        }
        [UnityTest]
        public IEnumerator SocketRequiresFullContainmentSupportOrientationAndBackStop()
        {
            var block=Item("a3.block");var socket=(SocketSensor)Station("A3").Condition;
            Pose(block,new Vector3(-2.96f,.85f,8.1f),Quaternion.identity);yield return new WaitForSeconds(.4f);
            Assert.That(socket.IsSatisfied,Is.False,"Unrotated block does not fit the channel.");
            Pose(block,new Vector3(-3.5f,.85f,8.1f),Quaternion.Euler(0,90,0));yield return new WaitForSeconds(.5f);
            Assert.That(socket.IsSatisfied,Is.False,"Partial insertion is insufficient.");
            Pose(block,new Vector3(-2.96f,.86f,8.1f),Quaternion.Euler(0,90,0));yield return WaitSuccess("A3");
            Assert.That(socket.FitsAndIsSeated(),Is.True);
            Pose(block,new Vector3(-5.1f,.275f,8.1f),Quaternion.Euler(8,90,0));yield return new WaitForSeconds(.3f);
            Assert.That(socket.IsSatisfied,Is.False,"Angle tolerance alone must not report success.");
        }
        [UnityTest]
        public IEnumerator LocalResetsAreAtomicIndependentAndRestoreClaimVelocitiesAndSensors()
        {
            var cube=Item("a1.cube");var foreign=Item("a2.heavy");var a1=Station("A1");
            Pose(cube,new Vector3(-5,1,5),Quaternion.Euler(20,30,40));
            Pose(foreign,cube.InitialPosition,Quaternion.identity);yield return new WaitForFixedUpdate();
            Vector3 before=cube.Body.position;Vector3 foreignBefore=foreign.Body.position;
            Assert.That(a1.TryReset(),Is.False);Assert.That(a1.LastResetMessage,Is.Not.Empty);
            Assert.That(cube.Body.position,Is.EqualTo(before));Assert.That(foreign.Body.position,Is.EqualTo(foreignBefore));
            foreign.RestoreInitial();yield return new WaitForSeconds(.7f);
            PlayerAt(new Vector3(-5,0,3.8f));yield return Aim(cube.Body.position);yield return Tap(Key.E);
            Assert.That(device.Held,Is.SameAs(cube),Diagnostic());
            Assert.That(a1.TryReset(),Is.True);Assert.That(device.Held,Is.Null);
            Assert.That(cube.Body.position,Is.EqualTo(cube.InitialPosition));Assert.That(cube.Body.linearVelocity,Is.EqualTo(Vector3.zero));
            Assert.That(cube.Body.angularVelocity,Is.EqualTo(Vector3.zero));Assert.That(a1.State,Is.EqualTo(StationState.Ready));
            Assert.That(a1.Condition.IsSatisfied,Is.False);
            foreach(var station in stations)
            {
                foreach(var item in station.Items)Pose(item,item.InitialPosition+Vector3.up*.4f,Quaternion.Euler(15,30,20));
                for(int i=0;i<10;i++)Assert.That(station.TryReset(),Is.True,station.Id);
                foreach(var item in station.Items)Assert.That(item.Body.position,Is.EqualTo(item.InitialPosition));
            }
            Assert.That(Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None).Length,Is.EqualTo(4));
        }
        [UnityTest]
        public IEnumerator HoldingRotationAndWallsRemainPhysicalAndOverloadReleasesSafely()
        {
            var cube=Item("a1.cube");PlayerAt(new Vector3(-4,0,1.4f));yield return Aim(cube.Body.position);yield return Tap(Key.E);
            yield return Aim(new Vector3(-4,1.8f,3));yield return new WaitForSeconds(.5f);
            Assert.That(device.Held,Is.SameAs(cube));
            float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;
            float until=Time.time+.7f;while(Time.time<until){minimum=Mathf.Min(minimum,cube.Body.position.y);maximum=Mathf.Max(maximum,cube.Body.position.y);yield return null;}
            Assert.That(maximum-minimum,Is.LessThan(.035f),"Stationary hold jitter.");
            Quaternion before=cube.Body.rotation;
            InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(200,0)}.WithButton(MouseButton.Right));yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(1.5f);
            Assert.That(Quaternion.Angle(before,cube.Body.rotation),Is.GreaterThan(70));
            Keys(Key.A);yield return new WaitForSeconds(.25f);Keys();yield return null;
            Assert.That(device.Held,Is.SameAs(cube),"Holding while walking.");
            // Reposition only the player to a boundary fixture; no object is teleported through the wall.
            PlayerAt(new Vector3(-6.45f,0,2));yield return Aim(new Vector3(-9,1.5f,2));
            yield return new WaitForSeconds(1);
            Assert.That(cube.Shape.bounds.min.x,Is.GreaterThanOrEqualTo(-7.03f));
            cube.Body.mass=20;yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Assert.That(device.Held,Is.Null);Assert.That(cube.Body.detectCollisions,Is.True);Assert.That(cube.Body.isKinematic,Is.False);
        }
        [UnityTest]
        public IEnumerator HeldNeighbourDoesNotHideAnIndependentFreeLoad()
        {
            var heavy=Item("a2.heavy");var light=Item("a2.light");
            Pose(heavy,new Vector3(4,.48f,5.5f),Quaternion.identity);
            Pose(light,new Vector3(3.4f,.38f,5.5f),Quaternion.identity);
            yield return WaitSuccess("A2");
            PlayerAt(new Vector3(3.4f,0,4));yield return Aim(light.Body.position);yield return Tap(Key.E);
            Assert.That(device.Held,Is.SameAs(light));yield return new WaitForSeconds(.7f);
            Assert.That(Station("A2").CurrentSuccess,Is.True,"An independently supported 5 kg cube still counts.");
            Assert.That(((LoadPlateSensor)Station("A2").Condition).SupportedKilograms,Is.EqualTo(5).Within(.2f));
        }
        [UnityTest]
        public IEnumerator AnonymousPhysicalMassUsesTheSameSensorButCannotBeCaptured()
        {
            var go=new GameObject("UnregisteredPhysicsFixture");
            try
            {
                var shape=go.AddComponent<BoxCollider>();shape.size=Vector3.one*.3f;
                var body=go.AddComponent<Rigidbody>();body.mass=4.2f;body.position=new Vector3(4,.28f,5.5f);
                yield return WaitSuccess("A2");
                Assert.That(((LoadPlateSensor)Station("A2").Condition).SupportedKilograms,Is.EqualTo(4.2f).Within(.2f));
                PlayerAt(new Vector3(4,0,4));yield return Aim(body.position);yield return Tap(Key.E);
                Assert.That(device.Held,Is.Null,"Registration, not mass or a name, allows capture.");
            }
            finally{Object.Destroy(go);}
        }
        [UnityTest]
        public IEnumerator PhysicalResetButtonsAcceptFAndOnlyRestoreTheirOwnStation()
        {
            Vector3[] buttons={new Vector3(-2.9f,.7f,5.5f),new Vector3(5.2f,.7f,5.5f),new Vector3(-5.8f,.7f,9)};
            string[] ids={"A1","A2","A3"};
            for(int i=0;i<ids.Length;i++)
            {
                var station=Station(ids[i]);
                foreach(var item in station.Items)Pose(item,i==2?new Vector3(-5.1f,.28f,6.6f):item.InitialPosition+Vector3.forward*1.2f,Quaternion.identity);
                Vector3 button=buttons[i];PlayerAt(new Vector3(button.x,0,button.z-1.5f));
                yield return Aim(button);yield return Tap(Key.F);yield return new WaitForSeconds(.1f);
                foreach(var item in station.Items)Assert.That(Vector3.Distance(item.Body.position,item.InitialPosition),Is.LessThan(.025f),ids[i]+" reset through F: "+Diagnostic());
                foreach(var other in stations.Where(s=>s!=station))
                    Assert.That(other.State,Is.Not.EqualTo(StationState.Resetting));
            }
        }
        [UnityTest]
        public IEnumerator BlockedResetPreservesCaptureAndOverloadReleasesWithoutDisablingCollision()
        {
            var cube=Item("a1.cube");PlayerAt(new Vector3(-4,0,1.4f));yield return Aim(cube.Body.position);yield return Tap(Key.E);
            Assert.That(device.Held,Is.SameAs(cube));yield return Aim(new Vector3(-4,1.8f,3));yield return new WaitForSeconds(.3f);
            var foreign=Item("a2.heavy");Pose(foreign,cube.InitialPosition,Quaternion.identity);
            Assert.That(Station("A1").TryReset(),Is.False);Assert.That(device.Held,Is.SameAs(cube));
            foreign.RestoreInitial();cube.Body.mass=20;
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Assert.That(device.Held,Is.Null);Assert.That(cube.IsHeld,Is.False);
            Assert.That(cube.Body.detectCollisions&&cube.Body.useGravity,Is.True);Assert.That(cube.Body.isKinematic,Is.False);
            Assert.That(cube.Body.linearVelocity.magnitude,Is.LessThan(1));
            cube.Body.mass=2;Assert.That(Station("A1").TryReset(),Is.True);
            yield return Aim(cube.Body.position);yield return Tap(Key.E);Assert.That(device.Held,Is.SameAs(cube));
            cube.enabled=false;
            Assert.That(device.Held,Is.Null);Assert.That(cube.Body.detectCollisions&&cube.Body.useGravity,Is.True);
        }
        [UnityTest]
        public IEnumerator A1A2A3CanBeCompletedUsingInputWithoutPoseOverrides()
        {
            // All movement, captures, aiming, distance changes and rotation below use the actual input path.
            yield return WalkTo(new Vector3(-4,0,1.4f));
            yield return Aim(Item("a1.cube").Body.position);yield return Tap(Key.E);
            Assert.That(device.Held,Is.SameAs(Item("a1.cube")));
            yield return Aim(new Vector3(-4,1.8f,3));yield return new WaitForSeconds(.4f);
            yield return WalkTo(new Vector3(-5.5f,0,1.4f));yield return WalkTo(new Vector3(-5.5f,0,4.1f));
            yield return WalkTo(new Vector3(-4,0,4.1f));yield return TargetHeld(new Vector3(-4,.65f,5.5f));
            yield return Tap(Key.E);yield return WaitSuccess("A1");
            yield return WalkTo(new Vector3(0,0,4.1f));yield return WalkTo(new Vector3(4.7f,0,1.4f));
            yield return Aim(Item("a2.heavy").Body.position);yield return Tap(Key.E);
            Assert.That(device.Held,Is.SameAs(Item("a2.heavy")));
            yield return Aim(new Vector3(4.7f,1.8f,3));yield return new WaitForSeconds(.4f);
            yield return WalkTo(new Vector3(4,0,4.1f));yield return TargetHeld(new Vector3(4,.72f,5.5f));
            yield return Tap(Key.E);yield return WaitSuccess("A2");
            yield return WalkTo(new Vector3(0,0,6.55f));yield return WalkTo(new Vector3(-5.1f,0,6.55f));
            yield return Aim(Item("a3.block").Body.position);yield return Tap(Key.E);
            Assert.That(device.Held,Is.SameAs(Item("a3.block")));
            yield return Aim(new Vector3(-5.1f,1.8f,8.1f));yield return new WaitForSeconds(.5f);
            InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(200,0)}.WithButton(MouseButton.Right));yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(1.5f);
            Assert.That(Mathf.Abs(Vector3.Dot(Item("a3.block").transform.forward,Vector3.right)),Is.GreaterThan(.99f));
            yield return WalkTo(new Vector3(-6.0f,0,8.1f));
            yield return TargetHeld(new Vector3(-4.65f,.9f,8.1f));
            yield return WalkTo(new Vector3(-4.45f,0,8.1f));
            yield return TargetHeld(new Vector3(-2.96f,.9f,8.1f));
            yield return Tap(Key.E);yield return WaitSuccess("A3");
            foreach(var station in stations)Assert.That(station.State,Is.EqualTo(StationState.Passed));
        }
    }
}
