using UnityEngine;
namespace Section.Sandbox
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerSupportTracker : MonoBehaviour
    {
        Collider support;
        int lastFrame=-100;
        public Collider Current=>Time.frameCount-lastFrame<=1 && GetComponent<CharacterController>().isGrounded?support:null;
        void OnControllerColliderHit(ControllerColliderHit hit)
        {if(hit.normal.y>.65f){support=hit.collider;lastFrame=Time.frameCount;}}
    }
}
