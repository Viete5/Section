using System.Collections.Generic;
using UnityEngine;

namespace Section.Sandbox
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class LoadPlateSensor : StationCondition
    {
        [SerializeField] float minimumKilograms = .1f;
        [SerializeField] float stableSeconds = .25f;
        struct ContactLoad
        {
            public Rigidbody Body;
            public PhysicalItem Item;
            public Vector3 LocalPoint;
            public float Kilograms;
        }
        readonly Dictionary<Rigidbody, ContactLoad> loads = new Dictionary<Rigidbody, ContactLoad>();
        readonly List<Rigidbody> invalid = new List<Rigidbody>();
        BoxCollider plate;
        float stableTime;
        public float SupportedKilograms { get; private set; }
        public float MinimumKilograms => minimumKilograms;
        public float StableTime => stableTime;
        public override bool IsSatisfied => stableTime >= stableSeconds;
        public override bool HasContact => loads.Count > 0;
        public override string Reading => SupportedKilograms.ToString("0.0") + " / " + minimumKilograms.ToString("0.#") + " kg";
        public void Configure(float threshold, float duration = .25f) { minimumKilograms = threshold; stableSeconds = duration; }
        void Awake() => plate = GetComponent<BoxCollider>();
        void OnCollisionEnter(Collision collision) => Record(collision);
        void OnCollisionStay(Collision collision) => Record(collision);
        void OnCollisionExit(Collision collision) { if (collision.rigidbody != null) loads.Remove(collision.rigidbody); }
        void Record(Collision collision)
        {
            var body = collision.rigidbody;
            if (body == null || body.isKinematic) return;
            Vector3 top = plate.bounds.center + Vector3.up * plate.bounds.extents.y;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                if (Mathf.Abs(contact.normal.y) < .85f || Mathf.Abs(contact.point.y - top.y) > .035f) continue;
                loads[body] = new ContactLoad { Body = body, Item = body.GetComponent<PhysicalItem>(),
                    LocalPoint = body.transform.InverseTransformPoint(contact.point),
                    Kilograms = Mathf.Abs(collision.impulse.y) / (Physics.gravity.magnitude * Time.fixedDeltaTime) };
                return;
            }
            loads.Remove(body);
        }
        void FixedUpdate()
        {
            SupportedKilograms = 0;
            bool stable = true;
            invalid.Clear();
            foreach (var pair in loads)
            {
                var load = pair.Value;
                if (load.Body == null || !load.Body.gameObject.activeInHierarchy || !load.Body.detectCollisions || load.Body.isKinematic ||
                    Vector3.Distance(plate.ClosestPoint(load.Body.transform.TransformPoint(load.LocalPoint)), load.Body.transform.TransformPoint(load.LocalPoint)) > .04f)
                { invalid.Add(pair.Key); continue; }
                if (load.Item != null && (load.Item.IsHeld || load.Item.HasHeldNeighbour())) { continue; }
                if (load.Body.linearVelocity.sqrMagnitude > .0144f || load.Body.angularVelocity.sqrMagnitude > .0625f) stable = false;
                SupportedKilograms += load.Kilograms;
            }
            foreach (var key in invalid) loads.Remove(key);
            stableTime = stable && SupportedKilograms >= minimumKilograms - .015f ? stableTime + Time.fixedDeltaTime : 0;
        }
        public override void ResetCondition() { loads.Clear(); SupportedKilograms = 0; stableTime = 0; }
        void OnDisable() => ResetCondition();
    }
}
