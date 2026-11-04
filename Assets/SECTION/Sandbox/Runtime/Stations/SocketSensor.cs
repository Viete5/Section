using UnityEngine;
namespace Section.Sandbox
{
    public sealed class SocketSensor : StationCondition
    {
        [SerializeField] PhysicalItem item;
        [SerializeField] BoxCollider support;
        [SerializeField] Vector3 cavitySize = new Vector3(1.55f, .65f, .65f);
        [SerializeField] float angularTolerance = 8f;
        float stableTime;
        public PhysicalItem Item => item;
        public Vector3 CavitySize => cavitySize;
        public override bool IsSatisfied => stableTime >= .25f;
        public override bool HasContact => item != null && item.HasSupport(support);
        public override string Reading => IsSatisfied ? "SEATED" : HasContact ? "ALIGN / INSERT" : "EMPTY";
        public void Configure(PhysicalItem target, BoxCollider floor) { item = target; support = floor; }
        public bool FitsAndIsSeated()
        {
            if (item == null || item.IsHeld || !item.IsStable || !item.HasSupport(support)) return false;
            float cosine = Mathf.Cos(angularTolerance * Mathf.Deg2Rad);
            if (Mathf.Abs(Vector3.Dot(item.transform.forward, transform.right)) < cosine ||
                Mathf.Abs(Vector3.Dot(item.transform.up, transform.up)) < cosine) return false;
            var box = item.Shape;
            Vector3 half = cavitySize * .5f;
            float maximumX = float.NegativeInfinity;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = transform.InverseTransformPoint(item.transform.TransformPoint(box.center +
                            Vector3.Scale(box.size * .5f, new Vector3(x, y, z))));
                        if (Mathf.Abs(corner.x) > half.x + .004f || Mathf.Abs(corner.y) > half.y + .004f || Mathf.Abs(corner.z) > half.z + .004f) return false;
                        maximumX = Mathf.Max(maximumX, corner.x);
                    }
            return maximumX >= half.x - .05f;
        }
        void FixedUpdate() => stableTime = FitsAndIsSeated() ? stableTime + Time.fixedDeltaTime : 0;
        public override void ResetCondition() => stableTime = 0;
    }
}
