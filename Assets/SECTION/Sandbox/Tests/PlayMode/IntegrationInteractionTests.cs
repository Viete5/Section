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
    public sealed class IntegrationInteractionTests
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
                    string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../Artifacts/Sandbox01/M6"));Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,name+suffix+".png"),texture.EncodeToPNG());
                    var target=device.Held;
                    File.WriteAllText(Path.Combine(dir,name+".txt"),"Player "+sandbox.Player.transform.position+" / W "+world.W+" / Held "+target?.Id+
                        " / Body W "+target?.Source.State.Position.W+" / XYZ "+target?.Source.transform.position+" / Slice "+target?.Source.CurrentSlice.Kind);
                }
                finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(texture);}
            }
            Save(sandbox.Look.View.GetComponent<Camera>(),"");
            if(name.StartsWith("D"))
            {
                var node=new GameObject("M6_Observer_OnlyForExecutedTestCapture"){hideFlags=HideFlags.DontSave};
                try
                {
                    var observer=node.AddComponent<Camera>();observer.enabled=false;observer.orthographic=true;observer.orthographicSize=6;
                    observer.nearClipPlane=.01f;observer.farClipPlane=20;observer.clearFlags=CameraClearFlags.SolidColor;observer.backgroundColor=new Color(.15f,.18f,.2f);
                    node.transform.SetPositionAndRotation(new Vector3(0,4.8f,35),Quaternion.Euler(90,0,0));Save(observer,"-overview");
                }
                finally{UnityEngine.Object.Destroy(node);}
            }
#endif
        }
        IEnumerator ShiftTo(float w)
        {
            yield return Tap(Key.Digit3);float until=Time.time+12;
            while(Math.Abs(world.W-w)>.025 && Time.time<until){Keys(world.W<w?Key.E:Key.Q);yield return null;}
            Keys();yield return null;Assert.That(world.W,Is.EqualTo(w).Within(.06),world.LastBlockedReason);
        }
        IEnumerator EnterD()
        {yield return WalkTo(new Vector3(0,0,30.8f));yield return ShiftTo(2);yield return WalkTo(new Vector3(0,0,34.1f));yield return WalkTo(new Vector3(-2.6f,0,34.1f));}
        [UnityTest] public IEnumerator D1BlocksWalkingAndJumpingButPassesWithWorldShiftAndReturns()
        {
            yield return WalkTo(new Vector3(0,0,30.8f));Keys(Key.W);yield return new WaitForSeconds(1.2f);Keys();yield return null;
            Assert.That(sandbox.Player.transform.position.z,Is.LessThan(32));
            Keys(Key.W,Key.Space);yield return new WaitForSeconds(.8f);Keys();yield return new WaitForSeconds(.3f);Assert.That(sandbox.Player.transform.position.z,Is.LessThan(33));
            yield return WalkTo(new Vector3(0,0,30.8f));yield return ShiftTo(2);CaptureFrame("D1-01-empty-gate");
            yield return WalkTo(new Vector3(0,0,34.45f));yield return WalkTo(new Vector3(-2.6f,0,34.45f));yield return ShiftTo(0);
            yield return WalkTo(new Vector3(-2.6f,0,34.8f));yield return new WaitForFixedUpdate();Assert.That(Station("D1").State,Is.EqualTo(StationState.Passed));CaptureFrame("D1-02-returned");
            Assert.That(Station("D1").TryReset(),Is.True);Assert.That(Station("D1").State,Is.EqualTo(StationState.Ready));
        }
        [UnityTest] public IEnumerator D2CrossesByTimingAndRecoversFromContactWithAStationaryPlayer()
        {
            yield return EnterD();var walls=Station("D2").GetComponent<MovingWallsCondition>();
            yield return new WaitForSeconds(5.1f); // Observe a complete real cycle from the side bypass.
            while(!(walls.Phase>2.2 && walls.Phase<2.5))yield return null;
            var signal=new MaterialPropertyBlock();GameObject.Find("D2_PhaseSignal_-1").GetComponent<Renderer>().GetPropertyBlock(signal);
            Assert.That(signal.GetColor("_BaseColor").r,Is.GreaterThan(.9f));Assert.That(signal.GetColor("_BaseColor").g,Is.LessThan(.2f));
            yield return WalkTo(new Vector3(0,0,34.1f));
            while(!(walls.Phase>4.05 && walls.Phase<4.25))yield return null;
            yield return WalkTo(new Vector3(0,0,37));Assert.That(Station("D2").State,Is.EqualTo(StationState.Passed));CaptureFrame("D2-01-timed-crossing");
            Assert.That(Station("D2").TryReset(),Is.True,Station("D2").LastResetMessage);
            // Reset returns player to the entrance. Enter and stop; the moving wall must detect a stationary capsule.
            yield return ShiftTo(2);yield return WalkTo(new Vector3(0,0,34.1f));
            while(!(walls.Phase>4.05 && walls.Phase<4.25))yield return null;
            yield return WalkTo(new Vector3(0,0,35.65f));Keys();float until=Time.time+6;
            while(walls.FailureCount==0 && Time.time<until)yield return null;
            Assert.That(walls.FailureCount,Is.EqualTo(1));Assert.That(walls.PendingReset,Is.False);Assert.That(sandbox.Player.transform.position.z,Is.LessThan(34.3f));
            Assert.That(walls.Phase,Is.LessThan(.2));CaptureFrame("D2-02-contact-recovery");
        }
        [UnityTest] public IEnumerator D3UsesOneCompoundBodyReleaseAndWorldShiftToConnectRealContacts()
        {
            yield return EnterD();yield return WalkTo(new Vector3(4,0,34.1f));yield return ShiftTo(0);yield return WalkTo(new Vector3(4,0,35.8f));yield return Capture("D3");CaptureFrame("D3-01-captured");
            yield return TargetCenter(new Vector3(4,1.7f,37));yield return Plane(RotationPlane.XW);yield return Rotate(-90);
            yield return TargetCenter(new Vector3(4,1.25f,38));var sensor=Station("D3").GetComponent<IntegrationConnectorCondition>();
            Assert.That(sensor.FirstContact || sensor.SecondContact,Is.False,"The narrow slice cannot reach both receiver contacts.");
            yield return Tap(Key.E);yield return new WaitForSeconds(.6f);Assert.That(device.Held,Is.Null);Assert.That(Body("D3").IsStable,Is.True,Diagnostic());
            CaptureFrame("D3-02-installed-narrow");yield return ShiftTo(1);yield return new WaitForSeconds(.3f);CaptureFrame("D3-03-wide-connected");
            Assert.That(sensor.FirstContact && sensor.SecondContact,Is.True,sensor.Reading);Assert.That(Station("D3").State,Is.EqualTo(StationState.Passed));
            yield return Tap(Key.Digit1);yield return new WaitForSeconds(.25f);Assert.That(Station("D3").CurrentSuccess,Is.True,"A physical connection survives equipment switching.");
            yield return ShiftTo(0);Assert.That(sensor.FirstContact || sensor.SecondContact,Is.False);Assert.That(Station("D3").State,Is.EqualTo(StationState.Passed));
            Assert.That(Station("D3").TryReset(),Is.True,Station("D3").LastResetMessage);Assert.That(Body("D3").IsHeld,Is.False);Assert.That(Body("D3").LinearVelocity,Is.EqualTo(Vector3.zero));
            Assert.That(Body("D3").Source.State.Position.Z,Is.EqualTo(37));Assert.That(Body("D3").Source.State.Rotation[0,3],Is.EqualTo(-1).Within(1e-9));
        }
    }
}
