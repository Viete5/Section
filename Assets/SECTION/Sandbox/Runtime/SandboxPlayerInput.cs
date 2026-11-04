using UnityEngine;
using UnityEngine.InputSystem;

namespace Section.Sandbox
{
    [DefaultExecutionOrder(-200)]
    public sealed class SandboxPlayerInput : MonoBehaviour
    {
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool UsePressed { get; private set; }
        public bool SelectStandard { get; private set; }
        public bool SelectWorldShift { get; private set; }
        public bool SelectDimension { get; private set; }
        public bool NextPlane { get; private set; }
        public bool TogglePlaneGroup { get; private set; }
        public bool ModifyObjectW { get; private set; }
        public float ShiftAxis { get; private set; }
        public bool ToggleDiagnostics { get; private set; }
        public bool RotateHeld { get; private set; }
        public float ScrollNotches { get; private set; }
        public bool HasControl { get; private set; }

        void OnEnable()
        {
            if (Application.isPlaying) SetControl(true);
        }

        void Update()
        {
            Move = Look = Vector2.zero;
            JumpPressed = InteractPressed = UsePressed = SelectStandard = RotateHeld = false;
            ScrollNotches = 0;
            ShiftAxis=0;SelectWorldShift=SelectDimension=NextPlane=TogglePlaneGroup=ModifyObjectW=ToggleDiagnostics=false;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetControl(!HasControl);
                return;
            }
            if (!HasControl)
            {
                if (mouse != null && mouse.leftButton.wasPressedThisFrame) SetControl(true);
                return;
            }
            if (keyboard != null)
            {
                Move = Vector2.ClampMagnitude(new Vector2(
                    (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)), 1);
                JumpPressed = keyboard.spaceKey.wasPressedThisFrame;
                InteractPressed = keyboard.eKey.wasPressedThisFrame;
                UsePressed = keyboard.fKey.wasPressedThisFrame;
                SelectStandard = keyboard.digit1Key.wasPressedThisFrame;
                SelectWorldShift=keyboard.digit3Key.wasPressedThisFrame;
                SelectDimension=keyboard.digit2Key.wasPressedThisFrame;
                NextPlane=keyboard.rKey.wasPressedThisFrame;TogglePlaneGroup=keyboard.tKey.wasPressedThisFrame;
                ModifyObjectW=keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                ShiftAxis=(keyboard.eKey.isPressed?1:0)-(keyboard.qKey.isPressed?1:0);
                ToggleDiagnostics=keyboard.f3Key.wasPressedThisFrame;
            }
            if (mouse != null)
            {
                Look = mouse.delta.ReadValue();
                RotateHeld = mouse.rightButton.isPressed;
                ScrollNotches = mouse.scroll.ReadValue().y / 120f;
            }
        }

        void SetControl(bool value)
        {
            HasControl = value;
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;
        }

        void OnDisable()
        {
            if (HasControl) SetControl(false);
        }
    }
}
