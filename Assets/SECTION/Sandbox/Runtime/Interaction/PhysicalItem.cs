using System.Collections.Generic;
using UnityEngine;

namespace Section.Sandbox
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class PhysicalItem : MonoBehaviour
    {
        [SerializeField] string itemId;
        [SerializeField] bool canManipulate = true;
        [SerializeField] Vector3 initialPosition;
        [SerializeField] Quaternion initialRotation = Quaternion.identity;
        Rigidbody body;
        BoxCollider shape;
        readonly HashSet<Collider> contacts = new HashSet<Collider>();
        readonly HashSet<PhysicalItem> visited = new HashSet<PhysicalItem>();
        readonly Dictionary<Collider, Vector3> supports = new Dictionary<Collider, Vector3>();
        public string Id => itemId;
        public bool CanManipulate => canManipulate && enabled && gameObject.activeInHierarchy;
        public Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();
        public BoxCollider Shape => shape != null ? shape : shape = GetComponent<BoxCollider>();
        public StandardManipulator Holder { get; private set; }
        public bool IsHeld => Holder != null;
        public Vector3 InitialPosition => initialPosition;
        public Quaternion InitialRotation => initialRotation;
        public bool IsStable => Body.linearVelocity.sqrMagnitude < .0144f && Body.angularVelocity.sqrMagnitude < .0625f;
        public void Configure(string id, Vector3 position, Quaternion rotation)
        { itemId = id; initialPosition = position; initialRotation = rotation; }
        public bool TryClaim(StandardManipulator holder)
        {
            if (!CanManipulate || IsHeld || Body.isKinematic || !Body.detectCollisions) return false;
            Holder = holder; Body.WakeUp(); return true;
        }
        public void ClearClaim(StandardManipulator holder) { if (Holder == holder) Holder = null; }
        public bool HasSupport(Collider support) => supports.ContainsKey(support);
        public bool HasHeldNeighbour() { visited.Clear(); return HeldInChain(visited); }
        bool HeldInChain(HashSet<PhysicalItem> chain)
        {
            if (!chain.Add(this)) return false;
            if (IsHeld) return true;
            foreach (var collider in contacts)
            {
                if (collider == null) continue;
                var item = collider.GetComponentInParent<PhysicalItem>();
                if (item != null && item.HeldInChain(chain)) return true;
            }
            return false;
        }
        void OnCollisionEnter(Collision collision) => Record(collision);
        void OnCollisionStay(Collision collision) => Record(collision);
        void OnCollisionExit(Collision collision) { supports.Remove(collision.collider); contacts.Remove(collision.collider); }
        void Record(Collision collision)
        {
            bool vertical = false;
            bool supported = false;
            Vector3 supportPoint = default;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var point = collision.GetContact(i);
                if (Mathf.Abs(point.normal.y) > .7f) vertical = true;
                if (point.normal.y > .7f) { supported = true; supportPoint = point.point; }
            }
            // Only a vertical load path makes a held neighbour part of the assisted stack.
            if (vertical) contacts.Add(collision.collider); else contacts.Remove(collision.collider);
            if (supported) supports[collision.collider] = supportPoint; else supports.Remove(collision.collider);
        }
        public void RestoreInitial()
        {
            if (Holder != null) Holder.Release("Локальный сброс");
            supports.Clear(); contacts.Clear();
            var interpolation = Body.interpolation;
            Body.interpolation = RigidbodyInterpolation.None;
            Body.position = initialPosition; Body.rotation = initialRotation;
            Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero;
            Body.interpolation = interpolation;
            Body.WakeUp();
        }
        void OnDisable() { if (Holder != null) Holder.Release("Предмет недоступен"); supports.Clear(); contacts.Clear(); }
    }
}
