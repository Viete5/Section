using UnityEngine;

namespace Section.Sandbox
{
    // Explicit scene references; no singleton, progression, equipment or station logic in M1.
    public sealed class SandboxScene : MonoBehaviour
    {
        [SerializeField] SandboxDefinition definition;
        [SerializeField] SandboxMotor player;
        [SerializeField] SandboxLook look;
        public SandboxDefinition Definition => definition;
        public SandboxMotor Player => player;
        public SandboxLook Look => look;
        public void Configure(SandboxDefinition config, SandboxMotor motor, SandboxLook cameraLook)
        { definition = config; player = motor; look = cameraLook; }
    }
}
