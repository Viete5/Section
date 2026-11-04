using UnityEngine;
namespace Section.Sandbox
{
    public sealed class GeometryResetHandler : MonoBehaviour
    {
        [SerializeField] WorldSliceState world;
        [SerializeField] GeometrySliceView[] owned;
        [SerializeField] bool restorePlayer;
        [SerializeField] Vector3 safeFeet=new Vector3(4.25f,0,12);
        [SerializeField] StandardManipulator standard;
        [SerializeField] SandboxLook look;
        public void Configure(WorldSliceState state,GeometrySliceView[] views,bool playerReset,StandardManipulator device,SandboxLook camera)
        {world=state;owned=views;restorePlayer=playerReset;standard=device;look=camera;}
        public void SetSafeFeet(Vector3 feet)=>safeFeet=feet;
        bool Has4DCapture()
        {foreach(var source in world.Sources){var body=source.GetComponent<FourDimensionalBody>();if(body!=null && body.IsHeld)return true;}return false;}
        public bool TryRestore(out string reason)
        {
            if(restorePlayer && Has4DCapture()){reason="Освободите 4D-захват (2 → E) перед восстановлением игрока";return false;}
            if(restorePlayer && standard.Held!=null){reason="Отпустите предмет перед восстановлением игрока";return false;}
            if(!world.TryRestore(owned,restorePlayer,restorePlayer?safeFeet:(Vector3?)null,out reason))return false;
            if(restorePlayer)look.ResetPitch();return true;
        }
        public bool TryReturnToBankKeepingW()
        {
            if(!restorePlayer || standard.Held!=null || Has4DCapture())return false;
            // Emergency recovery must leave the reason for the refused full reset intact.
            // A safe bank return is not reported as a successful W=0 reset.
            foreach(float z in new[]{11.1f,11.6f,12.1f})
                foreach(float x in new[]{4.25f,5.5f,3.1f})
                    if(world.TryRestore(owned,false,new Vector3(x,0,z),out _))
                    {look.ResetPitch();return true;}
            return false;
        }
    }
}
