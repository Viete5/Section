using UnityEngine;
namespace Section.Sandbox
{
    public static class PhysicsContactUtility
    {
        // Rigidbody.position/rotation are the simulation pose. An interpolated Transform can still show an older frame.
        public static void GetPose(Collider collider,out Vector3 position,out Quaternion rotation)
        {
            var body=collider.attachedRigidbody;
            if(body==null){position=collider.transform.position;rotation=collider.transform.rotation;return;}
            Quaternion delta=body.rotation*Quaternion.Inverse(body.transform.rotation);
            position=body.position+delta*(collider.transform.position-body.transform.position);
            rotation=delta*collider.transform.rotation;
        }
    }
}
