using Section.Geometry4D;
using UnityEngine;
namespace Section.Sandbox
{
    // The full XYZ projection is physical only for W-invariant ordinary Rigidbody items.
    // It is excluded from ray targeting and from the player's active-slice contact model.
    [ExecuteAlways,RequireComponent(typeof(MeshCollider))]
    public sealed class OrdinaryContactProjection : MonoBehaviour
    {
        [SerializeField] FourDimensionalBody body;
        [SerializeField] int partIndex;
        MeshCollider colliderView;
        Mesh current,spare;
        Matrix4D builtRotation;
        Vector4D builtHalf;
        public MeshCollider Collider=>colliderView;
        public void Configure(FourDimensionalBody owner,int part=0){body=owner;partIndex=part;}
        void OnEnable()
        {
            colliderView=GetComponent<MeshCollider>();colliderView.enabled=false;colliderView.sharedMesh=null;colliderView.convex=true;colliderView.contactOffset=.003f;
            if(body==null)return;body.Source.SliceChanged+=Changed;Changed(body.Source);
        }
        void Changed(GeometrySliceView source)
        {
            if(body==null || !body.isActiveAndEnabled){colliderView.enabled=false;return;}
            var pose=source.State;bool same=builtRotation!=null && (builtHalf-body.Half).LengthSquared==0;
            if(same)for(int i=0;i<4;i++)for(int j=0;j<4;j++)if(builtRotation[i,j]!=pose.Rotation[i,j])same=false;
            if(!same)
            {
                colliderView.enabled=false;colliderView.sharedMesh=null;
                var localPose=new Transform4D(new Vector4D(0,0,0,pose.Position.W),pose.Rotation);
                var full=ConvexPrism4D.Planes(body.PartHalf(partIndex),default,new Transform4D(localPose.Position+localPose.Rotation*body.PartCenter(partIndex),localPose.Rotation));
                var planes=ConvexPrism4D.ProjectInterval(full,pose.Position.W-body.Half.Length*2,pose.Position.W+body.Half.Length*2,out bool empty);
                if(empty)throw new System.InvalidOperationException("Full body projection cannot be empty.");
                var result=ConvexSectionBuilder.Build(planes,body.Half.Length,SliceTolerance.Default);
                var previous=current;current=spare;spare=previous;
                if(current==null)current=new Mesh{name=body.Id+"_OrdinaryProjection",hideFlags=HideFlags.DontSave};
                if(SliceMeshWriter.Write(current,result)!=SliceRenderStatus.Ready)throw new System.InvalidOperationException("Invalid full-body projection: "+body.Id);
                Physics.BakeMesh(current.GetInstanceID(),true,colliderView.cookingOptions);colliderView.sharedMesh=current;colliderView.enabled=true;
                builtRotation=pose.Rotation;builtHalf=body.Half;
            }
            RefreshPairs();
        }
        void RefreshPairs()
        {
            if(body==null || body.Physics==null || colliderView==null || !colliderView.enabled)return;
            var world=body.Physics.World;
            Physics.IgnoreCollision(colliderView,world.Player.Controller,true);
            foreach(var visible in body.Source.GetComponent<SliceColliderView>().ActiveColliders)
                foreach(var item in world.ConventionalItems)
                    if(item!=null && item.Shape.enabled)Physics.IgnoreCollision(visible,item.Shape,true);
        }
        void LateUpdate()
        {
            if(body==null)return;
            if(!body.isActiveAndEnabled){colliderView.enabled=false;builtRotation=null;return;}
            if(!colliderView.enabled)Changed(body.Source);else RefreshPairs();
        }
        void OnDisable()
        {
            if(body!=null)body.Source.SliceChanged-=Changed;
            if(colliderView!=null){colliderView.enabled=false;colliderView.sharedMesh=null;}
            if(current!=null){if(Application.isPlaying)Destroy(current);else DestroyImmediate(current);current=null;}
            if(spare!=null){if(Application.isPlaying)Destroy(spare);else DestroyImmediate(spare);spare=null;}
            builtRotation=null;
        }
    }
}
