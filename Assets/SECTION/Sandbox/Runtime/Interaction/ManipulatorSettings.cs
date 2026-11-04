using UnityEngine;

namespace Section.Sandbox
{
    [CreateAssetMenu(menuName = "SECTION/Manipulator Settings")]
    public sealed class ManipulatorSettings : ScriptableObject
    {
        public float acquisitionRange = 2.8f;
        public float minimumDistance = 1.0f;
        public float maximumDistance = 3f;
        public float distancePerNotch = .18f;
        public float maximumMass = 12f;
        public float positionSpring = 90f;
        public float positionDamping = 18f;
        public float maximumForce = 250f;
        public float rotationSensitivity = .45f;
        public float angularSpring = 45f;
        public float angularDamping = 12f;
        public float maximumAngularAcceleration = 35f;
        public float maximumError = 1.2f;
        public float obstructionGrace = .45f;
    }
}
