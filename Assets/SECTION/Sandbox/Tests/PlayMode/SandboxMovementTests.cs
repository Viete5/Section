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
    public sealed class SandboxMovementTests
    {
        const string ScenePath = "Assets/Scenes/Dev/Sandbox_01.unity";
        Keyboard keyboard;
        Mouse mouse;
        readonly List<InputDevice> disabledDevices = new List<InputDevice>();
        SandboxScene sandbox;
        bool previousBackground;
        InputSettings.BackgroundBehavior previousInputBackground;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousBackground = Application.runInBackground;
            previousInputBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach (var device in InputSystem.devices.ToArray())
                if (device.enabled && (device is Keyboard || device is Mouse))
                { InputSystem.DisableDevice(device); disabledDevices.Add(device); }
            keyboard = InputSystem.AddDevice<Keyboard>("SandboxTestKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("SandboxTestMouse");
            keyboard.MakeCurrent(); mouse.MakeCurrent();
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("This authoring scene test uses the Editor scene loader; no Build Settings changes are required.");
#endif
            sandbox = Object.FindFirstObjectByType<SandboxScene>();
            Assert.That(sandbox, Is.Not.Null);
            yield return Frames(3);
            Assert.That(sandbox.Player.Input.HasControl, Is.True);
            Assert.That(sandbox.Player.Controller.isGrounded, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            foreach (var device in disabledDevices)
                if (device.added) InputSystem.EnableDevice(device);
            disabledDevices.Clear();
            InputSystem.settings.backgroundBehavior = previousInputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            Application.runInBackground = previousBackground;
            yield return null;
        }

        [UnityTest]
        public IEnumerator WalksFromStartThroughAllThreeOpeningsAndBack()
        {
            Vector3 start = sandbox.Player.transform.position;
            Keys(Key.W);
            float until = Time.time + 11;
            while (Time.time < until && sandbox.Player.transform.position.z < 38) yield return null;
            Keys(); yield return Frames(2);
            Assert.That(sandbox.Player.transform.position.z, Is.GreaterThan(38));
            Assert.That(sandbox.Player.transform.position.x, Is.EqualTo(0).Within(.02f));
            Assert.That(sandbox.Player.transform.position.y, Is.EqualTo(start.y).Within(.04f));
            Keys(Key.S);
            until = Time.time + 11;
            while (Time.time < until && sandbox.Player.transform.position.z > 1.5f) yield return null;
            Keys(); yield return Frames(2);
            Assert.That(sandbox.Player.transform.position.z, Is.LessThanOrEqualTo(1.5f));
            Assert.That(sandbox.Player.Controller.isGrounded, Is.True);
        }

        [UnityTest]
        public IEnumerator SpeedIsFourMetersPerSecondWithoutSprintOrDiagonalBoost()
        {
            yield return MeasureSpeed(new[] { Key.W });
            yield return MeasureSpeed(new[] { Key.W, Key.LeftShift });
            yield return MeasureSpeed(new[] { Key.W, Key.D });
        }

        IEnumerator MeasureSpeed(Key[] keys)
        {
            Keys(keys); yield return Frames(2);
            Vector3 start = sandbox.Player.transform.position;
            float begin = Time.time;
            while (Time.time - begin < .3f) yield return null;
            Vector3 delta = sandbox.Player.transform.position - start;
            delta.y = 0;
            Assert.That(delta.magnitude / (Time.time - begin), Is.EqualTo(4).Within(.06f));
            Keys(); yield return Frames(2);
        }

        [UnityTest]
        public IEnumerator MouseLooksAndEscapeReleasesAndClickResumesControl()
        {
            float yaw = sandbox.Player.transform.eulerAngles.y;
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100, 50) });
            yield return Frames(2);
            Assert.That(Mathf.DeltaAngle(yaw, sandbox.Player.transform.eulerAngles.y), Is.EqualTo(12).Within(.05f));
            Assert.That(Mathf.DeltaAngle(0, sandbox.Look.View.localEulerAngles.x), Is.EqualTo(-6).Within(.05f));
            Keys(Key.Escape); yield return Frames(2);
            Assert.That(sandbox.Player.Input.HasControl, Is.False);
            Vector3 start = sandbox.Player.transform.position;
            Keys(Key.W); yield return new WaitForSeconds(.2f);
            Assert.That(Vector3.Distance(start, sandbox.Player.transform.position), Is.LessThan(.02f));
            Keys();
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return Frames(2);
            Assert.That(sandbox.Player.Input.HasControl, Is.True);
            InputSystem.QueueStateEvent(mouse, new MouseState());
        }

        [UnityTest]
        public IEnumerator JumpHasExpectedHeightAndCannotRepeatInMidair()
        {
            float ground = sandbox.Player.transform.position.y;
            Keys(Key.Space); yield return Frames(2);
            Keys(); yield return Frames(2);
            Keys(Key.Space); // A second press in flight must not create a second impulse.
            float peak = sandbox.Player.transform.position.y;
            float until = Time.time + 1.2f;
            while (Time.time < until)
            { peak = Mathf.Max(peak, sandbox.Player.transform.position.y); yield return null; }
            Keys(); yield return Frames(2);
            Assert.That(peak - ground, Is.EqualTo(.6f).Within(.05f));
            Assert.That(sandbox.Player.transform.position.y, Is.EqualTo(ground).Within(.04f));
            Assert.That(sandbox.Player.Controller.isGrounded, Is.True);
        }

        [UnityTest]
        public IEnumerator SolidWallsPartitionsAndCeilingStopTheCapsule()
        {
            // Isolated collision fixtures: direct setup is deliberately not used as route completion evidence.
            Place(new Vector3(-6, 0, 5)); Keys(Key.A);
            yield return new WaitForSeconds(.5f); Keys(); yield return Frames(2);
            Assert.That(sandbox.Player.transform.position.x, Is.EqualTo(-6.65f).Within(.06f));
            Place(new Vector3(6, 0, 5)); Keys(Key.D);
            yield return new WaitForSeconds(.5f); Keys(); yield return Frames(2);
            Assert.That(sandbox.Player.transform.position.x, Is.EqualTo(6.65f).Within(.06f));
            Place(new Vector3(0, 0, 1)); Keys(Key.S);
            yield return new WaitForSeconds(.5f); Keys(); yield return Frames(2);
            Assert.That(sandbox.Player.transform.position.z, Is.EqualTo(.35f).Within(.06f));
            Place(new Vector3(0, 0, 39)); Keys(Key.W);
            yield return new WaitForSeconds(.5f); Keys(); yield return Frames(2);
            Assert.That(sandbox.Player.transform.position.z, Is.EqualTo(39.65f).Within(.06f));
            Place(new Vector3(6.4f, 0, 9)); Keys(Key.W);
            yield return new WaitForSeconds(.5f); Keys(); yield return Frames(2);
            Assert.That(sandbox.Player.transform.position.z, Is.EqualTo(9.5f).Within(.06f));
            Place(new Vector3(0, 0, 5));
            var flags = sandbox.Player.Controller.Move(Vector3.up * 10);
            Assert.That((flags & CollisionFlags.Above) != 0, Is.True);
            Assert.That(sandbox.Player.transform.position.y + 1.8f, Is.LessThanOrEqualTo(5.03f));
        }

        [UnityTest]
        public IEnumerator PitIsAnActualOpeningAndStopsFallingAtItsBottom()
        {
            // Isolate M1's physical pit/bottom/lining checks from M4's separately tested automatic rescue.
            var rescue = Object.FindFirstObjectByType<SpatialPlatformCondition>();
            rescue.enabled = false;
            try
            {
                Assert.That(Physics.Raycast(new Vector3(4.25f, 1, 15.5f), Vector3.down, out var hit, 5), Is.True);
                Assert.That(hit.point.y, Is.EqualTo(-2.5f).Within(.001f));
                Place(new Vector3(4.25f, 0, 12.4f));
                Keys(Key.W); yield return new WaitForSeconds(.9f);
                Keys(); yield return new WaitForSeconds(1);
                Assert.That(sandbox.Player.transform.position.y, Is.EqualTo(-2.5f).Within(.04f));
                Assert.That(sandbox.Player.Controller.isGrounded, Is.True);
                Keys(Key.A); yield return new WaitForSeconds(.8f);
                Keys(); yield return Frames(2);
                Assert.That(sandbox.Player.transform.position.x, Is.EqualTo(2.85f).Within(.06f), "Pit lining prevents movement beneath the floor.");
            }
            finally { rescue.enabled = true; }
        }

        void Place(Vector3 feet)
        {
            sandbox.Player.Controller.enabled = false;
            sandbox.Player.transform.SetPositionAndRotation(feet, Quaternion.identity);
            sandbox.Player.Controller.enabled = true;
            Physics.SyncTransforms();
        }
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    }
}
