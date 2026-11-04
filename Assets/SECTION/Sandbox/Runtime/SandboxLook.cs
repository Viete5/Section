using UnityEngine;

namespace Section.Sandbox
{
    [DefaultExecutionOrder(-150)]
    public sealed class SandboxLook : MonoBehaviour
    {
        [SerializeField] SandboxDefinition definition;
        [SerializeField] SandboxPlayerInput input;
        [SerializeField] Transform body;
        [SerializeField] Transform view;
        [SerializeField] StandardManipulator manipulator;
        [SerializeField] DimensionManipulator dimension;
        public void SetDimension(DimensionManipulator device)=>dimension=device;
        float pitch;
        public void SetManipulator(StandardManipulator device) => manipulator = device;
        public void ResetPitch(){pitch=0;view.localRotation=Quaternion.identity;}
        public Transform View => view;
        public SandboxPlayerInput Input => input;
        public SandboxDefinition Definition => definition;
        public Transform Body => body;

        public void Configure(SandboxDefinition config, SandboxPlayerInput source, Transform player, Transform cameraView)
        { definition = config; input = source; body = player; view = cameraView; }

        void Awake()
        {
            if (definition == null || input == null || body == null || view == null)
                throw new MissingReferenceException("SandboxLook is not configured.");
            pitch = Mathf.DeltaAngle(0, view.localEulerAngles.x);
        }

        void Update()
        {
            if (!input.HasControl || (manipulator != null && manipulator.IsRotating) || (dimension!=null && dimension.IsRotating)) return;
            body.Rotate(0, input.Look.x * definition.mouseSensitivity, 0, Space.Self);
            pitch = Mathf.Clamp(pitch - input.Look.y * definition.mouseSensitivity, -85, 85);
            view.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
    }
}
