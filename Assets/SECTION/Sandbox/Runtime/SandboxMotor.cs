using UnityEngine;

namespace Section.Sandbox
{
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-100)]
    public sealed class SandboxMotor : MonoBehaviour
    {
        [SerializeField] SandboxDefinition definition;
        [SerializeField] SandboxPlayerInput input;
        CharacterController controller;
        float verticalVelocity;
        public SandboxDefinition Definition => definition;
        public SandboxPlayerInput Input => input;
        public CharacterController Controller => controller != null ? controller : GetComponent<CharacterController>();
        public float VerticalVelocity => verticalVelocity;

        public void Configure(SandboxDefinition config, SandboxPlayerInput source)
        { definition = config; input = source; }

        public void RestoreSafePose(Vector3 feet,Quaternion rotation)
        {
            var cc=Controller;cc.enabled=false;transform.SetPositionAndRotation(feet,rotation);
            verticalVelocity=0;cc.enabled=true;
        }

        void Awake()
        {
            if (definition == null || input == null) throw new MissingReferenceException("SandboxMotor is not configured.");
            controller = GetComponent<CharacterController>();
        }

        void Update()
        {
            // Substeps preserve speed at low render rates while bounding gravity/collision steps.
            // No time is discarded. Camera input is applied once, before this component updates.
            float remaining = Time.deltaTime;
            bool jump = input.JumpPressed;
            Vector3 horizontal = transform.TransformDirection(new Vector3(input.Move.x, 0, input.Move.y)) * definition.moveSpeed;
            while (remaining > 0.000001f)
            {
                float dt = Mathf.Min(remaining, 0.02f);
                if (controller.isGrounded && verticalVelocity < 0) verticalVelocity = -2f;
                if (jump && controller.isGrounded)
                    verticalVelocity = Mathf.Sqrt(2f * definition.gravity * definition.jumpHeight);
                jump = false;
                float dy = verticalVelocity * dt - 0.5f * definition.gravity * dt * dt;
                verticalVelocity -= definition.gravity * dt;
                var flags = controller.Move(horizontal * dt + Vector3.up * dy);
                if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0) verticalVelocity = 0;
                if ((flags & CollisionFlags.Below) != 0 && verticalVelocity < 0) verticalVelocity = -2f;
                remaining -= dt;
            }
        }
    }
}
