using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    public sealed class DimensionInteractionTests
    {
        Keyboard keyboard;Mouse mouse;readonly List<InputDevice> disabled=new List<InputDevice>();
        bool background;InputSettings.BackgroundBehavior backgroundInput;InputSettings.EditorInputBehaviorInPlayMode editorInput;
        SandboxScene sandbox;DimensionManipulator device;WorldSliceState world;
        FourDimensionalBody Body(string id)=>world.Sources.Single(s=>s.Id==id).GetComponent<FourDimensionalBody>();
        TestStation Station(string id)=>UnityEngine.Object.FindObjectsByType<TestStation>(FindObjectsSortMode.None).Single(s=>s.Id==id);
        [UnitySetUp] public IEnumerator SetUp()
        {
            background=Application.runInBackground;backgroundInput=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach(var d in InputSystem.devices.ToArray())if(d.enabled&&(d is Keyboard||d is Mouse)){InputSystem.DisableDevice(d);disabled.Add(d);}
            keyboard=InputSystem.AddDevice<Keyboard>("DimensionTestKeyboard");mouse=InputSystem.AddDevice<Mouse>("DimensionTestMouse");keyboard.MakeCurrent();mouse.MakeCurrent();
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Dev/Sandbox_01.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor scene integration test.");
#endif
            sandbox=UnityEngine.Object.FindFirstObjectByType<SandboxScene>();world=UnityEngine.Object.FindFirstObjectByType<WorldSliceState>();device=UnityEngine.Object.FindFirstObjectByType<DimensionManipulator>();
            yield return new WaitForSeconds(.25f);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            foreach(var d in disabled)if(d.added)InputSystem.EnableDevice(d);disabled.Clear();
            InputSystem.settings.backgroundBehavior=backgroundInput;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;Application.runInBackground=background;yield return null;
        }
        void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        IEnumerator Tap(Key key){Keys(key);yield return null;Keys();yield return null;}
        string Diagnostic()=>"feet="+sandbox.Player.transform.position+" held="+device.Held?.Id+" feedback="+device.Feedback+" body="+device.Held?.Source.State.Position.XYZ.X+","+device.Held?.Source.State.Position.Y+","+device.Held?.Source.State.Position.Z;
        IEnumerator WalkTo(Vector3 target)
        {
            float until=Time.time+15;
            while(Time.time<until)
            {
                Vector3 delta=target-sandbox.Player.transform.position;delta.y=0;if(delta.magnitude<.09f)break;
                var local=sandbox.Player.transform.InverseTransformDirection(delta.normalized);var keys=new List<Key>();
                if(local.x>.35f)keys.Add(Key.D);if(local.x<-.35f)keys.Add(Key.A);if(local.z>.35f)keys.Add(Key.W);if(local.z<-.35f)keys.Add(Key.S);
                Keys(keys.ToArray());yield return null;
            }
            Keys();yield return null;var error=target-sandbox.Player.transform.position;error.y=0;
            Assert.That(error.magnitude,Is.LessThan(.17f),"Walk blocked: "+Diagnostic());
        }
        IEnumerator Aim(Vector3 point)
        {
            float until=Time.time+4;
            while(Time.time<until)
            {
                Vector3 d=point-sandbox.Look.View.position;float yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,pitch=-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg;
                float dx=Mathf.DeltaAngle(sandbox.Player.transform.eulerAngles.y,yaw),dy=Mathf.DeltaAngle(Mathf.DeltaAngle(0,sandbox.Look.View.localEulerAngles.x),pitch);
                if(Mathf.Abs(dx)<.1f && Mathf.Abs(dy)<.1f)break;
                InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(Mathf.Clamp(dx,-5,5)/.12f,-Mathf.Clamp(dy,-5,5)/.12f)});yield return null;
            }
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
        }
        IEnumerator Capture(string id)
        {yield return Tap(Key.Digit2);yield return Aim(SliceMeshWriter.ToVector3(Body(id).Source.State.Position.XYZ));yield return Tap(Key.E);Assert.That(device.Held,Is.SameAs(Body(id)),Diagnostic());}
        IEnumerator Plane(RotationPlane plane)
        {
            if(((int)plane>=3)!=((int)device.Plane>=3))yield return Tap(Key.T);
            for(int i=0;i<3 && device.Plane!=plane;i++)yield return Tap(Key.R);
            Assert.That(device.Plane,Is.EqualTo(plane));
        }
        IEnumerator Rotate(float degrees)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{buttons=2,delta=new Vector2(degrees/device.Settings.rotationDegreesPerPixel,0)});yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(Mathf.Abs(degrees)/device.Settings.rotationDegreesPerSecond+.18f);
        }
        IEnumerator ObjectW(float delta)
        {
            Keys(Key.LeftShift);InputSystem.QueueStateEvent(mouse,new MouseState{scroll=new Vector2(0,delta/device.Settings.wPerNotch*120)});yield return null;
            Keys();InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(Mathf.Abs(delta)/device.Settings.wUnitsPerSecond+.15f);
        }
        IEnumerator TargetCenter(Vector3 center)
        {
            Assert.That(device.Held,Is.Not.Null,Diagnostic());float distance=Vector3.Distance(sandbox.Look.View.position,center);
            Assert.That(distance,Is.InRange(device.Settings.minimumDistance-.01f,device.Settings.maximumDistance+.01f),"Target outside device range.");
            InputSystem.QueueStateEvent(mouse,new MouseState{scroll=new Vector2(0,(distance-device.HoldDistance)/device.Settings.distancePerNotch*120)});yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return Aim(center);yield return new WaitForSeconds(.45f);
        }
        void CaptureFrame(string name)
        {
#if UNITY_EDITOR
            void Save(Camera camera,string suffix)
            {
                var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);var rt=new RenderTexture(1280,720,24);
                var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
                try
                {
                    camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                    string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../Artifacts/Sandbox01/M5"));Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,name+suffix+".png"),texture.EncodeToPNG());
                    var target=device.Held;
                    File.WriteAllText(Path.Combine(dir,name+".txt"),"Player "+sandbox.Player.transform.position+" / W "+world.W+" / Held "+target?.Id+
                        " / Body W "+target?.Source.State.Position.W+" / XYZ "+target?.Source.transform.position+" / Slice "+target?.Source.CurrentSlice.Kind);
                }
                finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(texture);}
            }
            Save(sandbox.Look.View.GetComponent<Camera>(),"");
            if(name.StartsWith("C2") || name.StartsWith("C3"))
            {
                var node=new GameObject("M5_Observer_OnlyForExecutedTestCapture"){hideFlags=HideFlags.DontSave};
                try
                {
                    var observer=node.AddComponent<Camera>();observer.enabled=false;observer.orthographic=true;observer.orthographicSize=6;
                    observer.nearClipPlane=.01f;observer.farClipPlane=20;observer.clearFlags=CameraClearFlags.SolidColor;observer.backgroundColor=new Color(.15f,.18f,.2f);
                    node.transform.SetPositionAndRotation(new Vector3(0,4.8f,25),Quaternion.Euler(90,0,0));Save(observer,"-overview");
                }
                finally{UnityEngine.Object.Destroy(node);}
            }
#endif
        }
        [UnityTest] public IEnumerator C1AllSixPlanesAndInverseGesturesPreserveCanonicalIdentity()
        {
            yield return WalkTo(new Vector3(0,0,21.1f));yield return WalkTo(new Vector3(-2,0,22.1f));yield return Capture("C1");
            yield return TargetCenter(new Vector3(-4,2.7f,23.4f));
            var original=Body("C1").Source.State.Rotation;
            foreach(RotationPlane plane in Enum.GetValues(typeof(RotationPlane)))
            {
                yield return Plane(plane);yield return Rotate(15);Assert.That(device.Held,Is.SameAs(Body("C1")));yield return Rotate(-15);
                var actual=Body("C1").Source.State.Rotation;for(int r=0;r<4;r++)for(int c=0;c<4;c++)Assert.That(actual[r,c],Is.EqualTo(original[r,c]).Within(2e-5),plane.ToString()+" "+Diagnostic());
            }
            yield return Plane(RotationPlane.XW);yield return Rotate(45);CaptureFrame("C1-01-XW45");
            yield return Plane(RotationPlane.YW);yield return Rotate(28);CaptureFrame("C1-02-XW45-YW28");
            yield return Plane(RotationPlane.ZW);yield return Rotate(17);CaptureFrame("C1-03-composed");
            Assert.That(Body("C1").Source.State.Rotation.IsProperRotation(),Is.True);Assert.That(Body("C1").Source.CurrentSlice.Faces.Count,Is.GreaterThan(6));
            yield return Aim(new Vector3(-2.6f,.7f,22.8f));yield return Tap(Key.F);
            Assert.That(device.Held,Is.Null,Station("C1").LastResetMessage);Assert.That(Body("C1").LinearVelocity,Is.EqualTo(Vector3.zero));
            Assert.That(Body("C1").Source.State.Position.Y,Is.EqualTo(1.8).Within(1e-5));
        }
        [UnityTest] public IEnumerator InvisibleCaptureSurvivesObjectWAndEquipmentWorldShiftChanges()
        {
            yield return WalkTo(new Vector3(0,0,21.1f));yield return WalkTo(new Vector3(-2,0,22.1f));yield return Capture("C1");yield return TargetCenter(new Vector3(-4,2.7f,23.4f));
            var body=Body("C1");yield return ObjectW(2);Assert.That(body.Source.CurrentSlice.Kind,Is.EqualTo(SliceKind.Empty));Assert.That(device.Held,Is.SameAs(body));Assert.That(body.Source.GetComponent<SliceColliderView>().ActiveCollider,Is.Null);
            var pose=body.Source.State;yield return Tap(Key.Digit3);Keys(Key.E);yield return new WaitForSeconds(1.5f);Keys();yield return null;
            Assert.That(device.Held,Is.SameAs(body));Assert.That((body.Source.State.Position-pose.Position).Length,Is.LessThan(1e-8));Assert.That(body.Source.CurrentSlice.Kind,Is.EqualTo(SliceKind.Volume));
            yield return Tap(Key.Digit2);Assert.That(device.Held,Is.SameAs(body));yield return ObjectW(-2);
            Assert.That(body.Source.State.Position.W,Is.EqualTo(0).Within(1e-6));Assert.That(world.W,Is.GreaterThan(1.3));
            yield return Tap(Key.Digit1);Assert.That(device.Held,Is.Null);Assert.That(UnityEngine.Object.FindFirstObjectByType<StandardManipulator>().IsEquipped,Is.True);
        }
        [UnityTest] public IEnumerator C2IsSolvedThroughThePhysicalWindowUsingOnlyGameplayInputFromSpawn()
        {
            // No pose, rotation, W setters or success setters in this route.
            yield return WalkTo(new Vector3(0,0,21.5f));yield return WalkTo(new Vector3(4.5f,0,21.5f));yield return Capture("C2");CaptureFrame("C2-01-capture");
            yield return ObjectW(1.5f);Assert.That(Body("C2").Source.CurrentSlice.Kind,Is.EqualTo(SliceKind.Empty));CaptureFrame("C2-02-invisible-held");
            yield return TargetCenter(new Vector3(4.5f,1.1f,24.2f));
            Assert.That(Body("C2").Source.State.Position.Z,Is.LessThan(24.06),"Invisible wide body must still hit the real wall: "+Diagnostic());
            Assert.That(device.Held,Is.SameAs(Body("C2")));CaptureFrame("C2-03-wide-blocked");
            yield return TargetCenter(new Vector3(4.5f,1.1f,23.3f));yield return ObjectW(-1.5f);
            yield return Plane(RotationPlane.XW);yield return Rotate(90);CaptureFrame("C2-04-thin-XW");
            Assert.That(Body("C2").Source.State.Rotation[0,3],Is.EqualTo(-1).Within(.005),Diagnostic());
            yield return TargetCenter(new Vector3(4.5f,1.1f,23.5f));yield return Tap(Key.E);yield return new WaitForSeconds(.4f);
            yield return WalkTo(new Vector3(6.6f,0,21.5f));yield return WalkTo(new Vector3(6.6f,0,24.35f));yield return WalkTo(new Vector3(6,0,24.35f));yield return Capture("C2");
            yield return TargetCenter(new Vector3(4.5f,1.1f,24.0f));yield return TargetCenter(new Vector3(4.5f,1.1f,25.8f));CaptureFrame("C2-05-through-window");
            yield return TargetCenter(new Vector3(4.5f,.65f,26.55f));yield return Tap(Key.E);
            yield return new WaitForSeconds(.9f);CaptureFrame("C2-06-placed");
            var condition=(PassagePlacementCondition)Station("C2").Condition;
            Assert.That(condition.TraversedAperture,Is.True,Diagnostic());Assert.That(Station("C2").CurrentSuccess,Is.True,condition.Reading+" "+Diagnostic());
            Assert.That(Body("C2").SupportingCollider.name,Is.EqualTo("C2_ReceiverPad"));
            Assert.That(Station("C2").TryReset(),Is.True,Station("C2").LastResetMessage);Assert.That(condition.TraversedAperture,Is.False);Assert.That(device.Held,Is.Null);
        }
        [UnityTest] public IEnumerator C3IsConnectedByRealRotationWCorrectionAndPlacementThroughGameplayInput()
        {
            // Entire route uses actual movement, mouse, equipment and interaction commands.
            yield return WalkTo(new Vector3(0,0,25.2f));yield return WalkTo(new Vector3(-4,0,25.2f));yield return Capture("C3");CaptureFrame("C3-01-capture");
            var condition=(SpatialConnectorCondition)Station("C3").Condition;
            Assert.That(condition.IsSatisfied,Is.False);yield return Plane(RotationPlane.XW);yield return Rotate(90);CaptureFrame("C3-02-rotated-invisible");
            Assert.That(device.Held,Is.SameAs(Body("C3")));Assert.That(Body("C3").Source.CurrentSlice.Kind,Is.EqualTo(SliceKind.Empty));Assert.That(condition.IsSatisfied,Is.False);
            yield return ObjectW(-.7f);Assert.That(Body("C3").Source.CurrentSlice.Kind,Is.EqualTo(SliceKind.Volume));Assert.That(condition.FirstContact && condition.SecondContact,Is.False,"Correct angle alone is not contact.");
            yield return TargetCenter(new Vector3(-4,1.2f,27));yield return new WaitForSeconds(.25f);CaptureFrame("C3-03-connected");
            Assert.That(condition.FirstContact && condition.SecondContact,Is.True,condition.Reading+" "+Diagnostic());Assert.That(Station("C3").State,Is.EqualTo(StationState.Passed));
            yield return Tap(Key.E);yield return new WaitForSeconds(.3f);Assert.That(condition.IsSatisfied,Is.True);
            yield return Tap(Key.Digit3);Keys(Key.E);yield return new WaitForSeconds(.5f);Keys();yield return null;
            Assert.That(condition.FirstContact || condition.SecondContact,Is.False);Assert.That(Station("C3").State,Is.EqualTo(StationState.Passed));
            Assert.That(Station("C3").TryReset(),Is.True,Station("C3").LastResetMessage);Assert.That(Body("C3").Source.State.Position.W,Is.EqualTo(.7));
            Assert.That(Body("C3").LinearVelocity,Is.EqualTo(Vector3.zero));
        }
        [UnityTest] public IEnumerator LocalResetRefusesAForeignObjectWithoutLosingTheCaptureOrResettingNeighbours()
        {
            yield return WalkTo(new Vector3(0,0,25.2f));yield return WalkTo(new Vector3(-4,0,25.2f));yield return Capture("C3");
            yield return TargetCenter(new Vector3(-4,1.6f,27));var item=UnityEngine.Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None).Single(i=>i.Id=="a1.cube");
            item.Body.position=new Vector3(-4,1.35f,26.4f);item.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
            var before=Body("C3").Source.State;Assert.That(Station("C3").TryReset(),Is.False);Assert.That(device.Held,Is.SameAs(Body("C3")));
            Assert.That((Body("C3").Source.State.Position-before.Position).Length,Is.EqualTo(0));Assert.That(item.Body.position.x,Is.EqualTo(-4));
            item.RestoreInitial();Physics.SyncTransforms();Assert.That(Station("C3").TryReset(),Is.True);Assert.That(device.Held,Is.Null);
        }
        [UnityTest] public IEnumerator InvisibleFullBodyContactsOrdinaryMatterButDoesNotBecomeAPhantomPlayerWall()
        {
            yield return WalkTo(new Vector3(0,0,21.1f));yield return WalkTo(new Vector3(-2,0,22.1f));yield return Capture("C1");
            yield return TargetCenter(new Vector3(-4,2.7f,23.4f));yield return ObjectW(2);
            yield return TargetCenter(new Vector3(0,1.1f,23.4f));
            float until=Time.time+3;while((Body("C1").Source.State.Position.XYZ-new Vector3D(0,1.1,23.4)).Length>.03 && Time.time<until)yield return null;
            Assert.That((Body("C1").Source.State.Position.XYZ-new Vector3D(0,1.1,23.4)).Length,Is.LessThan(.04),Diagnostic());
            yield return Tap(Key.Digit3);
            var body=Body("C1");Assert.That(body.Source.CurrentSlice.Kind,Is.EqualTo(SliceKind.Empty));
            Assert.That(body.Source.GetComponent<SliceColliderView>().ActiveCollider,Is.Null);
            var proxy=body.GetComponentInChildren<OrdinaryContactProjection>();Assert.That(proxy.Collider.enabled,Is.True);
            Assert.That(Physics.GetIgnoreCollision(proxy.Collider,sandbox.Player.Controller),Is.True);
            yield return WalkTo(new Vector3(0,0,22.1f));yield return WalkTo(new Vector3(0,0,24.7f));
            Assert.That(device.Held,Is.SameAs(body));Assert.That(world.W,Is.EqualTo(0));
            var item=UnityEngine.Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None).Single(i=>i.Id=="a1.cube");
            item.Body.position=new Vector3(0,2.9f,23.4f);item.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();yield return new WaitForSeconds(1);
            Assert.That(item.Body.position.y,Is.EqualTo(2.2f).Within(.04f),"Ordinary matter: "+Diagnostic()+" projection="+proxy.Collider.bounds+" mesh="+proxy.Collider.sharedMesh+" layerIgnored="+Physics.GetIgnoreLayerCollision(0,2));
            Assert.That(item.Body.position.z,Is.EqualTo(23.4f).Within(.04f));
        }
        [UnityTest] public IEnumerator BoundedBodiesUseTrueFourDimensionalContactRatherThanXYZOverlap()
        {
            var first=Body("C1");var second=Body("C3");
            Assert.That(first.Physics.TrySetState(first,new Transform4D(new Vector4D(0,2.5,24,0),Matrix4D.Identity),out string reason),Is.True,reason);
            Assert.That(second.Physics.TrySetState(second,new Transform4D(new Vector4D(0,2.5,24,4),Matrix4D.Identity),out reason),Is.True,reason);
            Assert.That(second.Source.CurrentSlice.Kind,Is.EqualTo(SliceKind.Empty));
            var move=second.Physics.Move(second,new BoxMotion4D(second.Source.State,second.Half,new Vector4D(0,0,0,-8)));
            Assert.That(move.Fraction,Is.EqualTo(.275).Within(.002));Assert.That(second.Source.State.Position.W,Is.GreaterThanOrEqualTo(1.7999));
            Assert.That(move.Reason,Does.Contain("C1"));yield return null;
        }
        [UnityTest] public IEnumerator APlayerResetCannotDragAClaimedNeighbourThroughTheWorld()
        {
            yield return WalkTo(new Vector3(0,0,21.1f));yield return WalkTo(new Vector3(-2,0,22.1f));yield return Capture("C1");
            var feet=sandbox.Player.transform.position;var pose=Body("C1").Source.State;
            Assert.That(Station("B2").TryReset(),Is.False);Assert.That(Station("B2").LastResetMessage,Does.Contain("4D"));
            Assert.That(device.Held,Is.SameAs(Body("C1")));Assert.That(sandbox.Player.transform.position,Is.EqualTo(feet));
            Assert.That((Body("C1").Source.State.Position-pose.Position).Length,Is.EqualTo(0));
            yield return Tap(Key.E);Assert.That(Station("B2").TryReset(),Is.True);Assert.That(device.Held,Is.Null);
        }
    }
}
