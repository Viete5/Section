using System;
using System.Collections.Generic;
using Section.Geometry4D;
using UnityEngine;
namespace Section.Sandbox
{
    // Limited canonical-body solver. Ordinary matter is W-invariant; bounded 4D bodies use full 4D support.
    public sealed class FourDimensionalPhysics : MonoBehaviour
    {
        [SerializeField] WorldSliceState world;
        readonly Collider[] broadphase=new Collider[192];
        readonly Dictionary<GeometrySliceView,(Transform4D pose,ISupportShape4D shape)> hulls=new Dictionary<GeometrySliceView,(Transform4D,ISupportShape4D)>();
        public string LastBlockedReason { get; private set; }="";
        public int LastIntervals { get; private set; }
        public WorldSliceState World=>world;
        public void Configure(WorldSliceState state)=>world=state;
        public readonly struct MoveResult
        {
            public readonly double Fraction;public readonly Collider Collider;public readonly string Reason;
            public MoveResult(double f,Collider c,string reason){Fraction=f;Collider=c;Reason=reason;}
        }
        static bool Equal(Transform4D a,Transform4D b)
        {if((a.Position-b.Position).LengthSquared!=0)return false;for(int i=0;i<4;i++)for(int j=0;j<4;j++)if(a.Rotation[i,j]!=b.Rotation[i,j])return false;return true;}
        public ISupportShape4D FullShape(GeometrySliceView view,Transform4D pose)
        {
            if(view.Shape==Shape4D.Hypersphere)return new SphereShape4D(pose.Position,view.Extent);
            if(view.Shape==Shape4D.Tesseract)return new BoxShape4D(pose,new Vector4D(view.Extent,view.Extent,view.Extent,view.Extent));
            if(!hulls.TryGetValue(view,out var cached) || !Equal(cached.pose,pose))
            {
                cached=(pose,new HullShape4D(LinearSystem4D.Vertices(view.GetPlanes(pose))));hulls[view]=cached;
            }
            return cached.shape;
        }
        internal static BoxShape4D OrdinaryBox(BoxCollider box)
        {
            PhysicsContactUtility.GetPose(box,out var p,out var rotation);
            var center=p+rotation*Vector3.Scale(box.center,box.transform.lossyScale);
            // Normalize the float quaternion in double; float basis vectors do not satisfy the core SO(4) tolerance.
            double qx=rotation.x,qy=rotation.y,qz=rotation.z,qw=rotation.w;
            double length=Math.Sqrt(qx*qx+qy*qy+qz*qz+qw*qw);qx/=length;qy/=length;qz/=length;qw/=length;
            var matrix=new Matrix4D(new double[]{1-2*(qy*qy+qz*qz),2*(qx*qy-qz*qw),2*(qx*qz+qy*qw),0,
                2*(qx*qy+qz*qw),1-2*(qx*qx+qz*qz),2*(qy*qz-qx*qw),0,
                2*(qx*qz-qy*qw),2*(qy*qz+qx*qw),1-2*(qx*qx+qy*qy),0,0,0,0,1});
            Vector3 half=Vector3.Scale(box.size*.5f,box.transform.lossyScale);
            return new BoxShape4D(new Transform4D(new Vector4D(center.x,center.y,center.z,0),matrix),new Vector4D(Math.Abs(half.x),Math.Abs(half.y),Math.Abs(half.z),0));
        }
        List<ContinuousBoxCollision4D.Obstacle> Obstacles(FourDimensionalBody body,BoxMotion4D motion,HashSet<GeometrySliceView> ignored=null)
        {
            var result=new List<ContinuousBoxCollision4D.Obstacle>();
            var start=motion.Start.Position;var end=start+motion.Translation;double radius=body.Half.Length;
            var center=(start+end)*.5;
            float reach=(float)(radius+motion.Translation.XYZ.Length*.5+.01);
            int count=Physics.OverlapBoxNonAlloc(SliceMeshWriter.ToVector3(center.XYZ),Vector3.one*reach,broadphase,Quaternion.identity,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==broadphase.Length)throw new InvalidOperationException("4D collision broadphase capacity exhausted; motion is not safe to accept.");
            for(int i=0;i<count;i++)
            {
                var collider=broadphase[i];
                if(collider==world.Player.Controller || collider.GetComponentInParent<SliceColliderView>()!=null)continue;
                if(collider is not BoxCollider box)throw new InvalidOperationException("M5 requires explicit support for ordinary collider: "+collider.name);
                result.Add(new ContinuousBoxCollision4D.Obstacle{Shape=OrdinaryBox(box),ProjectXYZ=true,Id=box.name,Owner=box,ContactTolerance=box.attachedRigidbody!=null?.004:1e-5});
            }
            foreach(var source in world.Sources)
            {
                if(source==body.Source || !source.isActiveAndEnabled || ignored!=null && ignored.Contains(source))continue;
                var pose=source.State;double bound=radius+source.SpatialBoundRadius+motion.Translation.Length*.5;
                if((pose.Position-center).LengthSquared>bound*bound)continue;
                if(source.Shape==Shape4D.CompoundBox)
                    for(int part=0;part<2;part++)result.Add(new ContinuousBoxCollision4D.Obstacle{Shape=new BoxShape4D(CompoundBox4D.Pose(pose,part),CompoundBox4D.Half(part)),Id=source.Id,Owner=source});
                else result.Add(new ContinuousBoxCollision4D.Obstacle{Shape=FullShape(source,pose),Id=source.Id,Owner=source});
            }
            // Exactly the player's current W, so an empty visible section does not invent player contacts.
            result.Add(new ContinuousBoxCollision4D.Obstacle{Shape=new SliceCapsuleShape4D(world.PlayerVolume(world.Player.transform.position),world.W),Id="Player",Owner=world.Player.Controller});
            return result;
        }
        double Prefix(FourDimensionalBody body,BoxMotion4D motion,IReadOnlyList<ContinuousBoxCollision4D.Obstacle> obstacles,out ContinuousBoxCollision4D.Obstacle blocker,out int intervals)
        {
            double fraction=1;blocker=null;intervals=0;
            for(int p=0;p<body.PartCount;p++){var part=new BoxMotion4D(motion.Start,body.PartHalf(p),motion.Translation,motion.Plane,motion.Angle,motion.Local,body.PartCenter(p));
                double f=ContinuousBoxCollision4D.FreePrefix(part,obstacles,out var hit,out int n);intervals+=n;if(f<fraction){fraction=f;blocker=hit;}}
            return fraction;
        }
        public MoveResult Move(FourDimensionalBody body,BoxMotion4D motion)
        {
            var obstacles=Obstacles(body,motion);
            double fraction=Prefix(body,motion,obstacles,out var obstacle,out int intervals);LastIntervals=intervals;
            if(fraction<1 && motion.PointTravelBound>1e-12)fraction=Math.Max(0,fraction-1e-5/motion.PointTravelBound);
            LastBlockedReason=fraction<1?"4D: препятствие "+obstacle?.Id:"";
            if(fraction>1e-10){world.CommitBodyPose(body.Source,motion.At(fraction));body.Applied(motion,fraction);}
            return new MoveResult(fraction,obstacle?.Owner as Collider,LastBlockedReason);
        }
        public bool CanPlace(FourDimensionalBody body,Transform4D pose,HashSet<GeometrySliceView> ignored,out string reason)
        {
            var motion=new BoxMotion4D(pose,body.Half,default);
            double prefix=Prefix(body,motion,Obstacles(body,motion,ignored),out var obstacle,out _);
            reason=prefix<1?"4D reset: "+obstacle?.Id:"";return prefix>=1;
        }
        public bool TrySetState(FourDimensionalBody body,Transform4D requested,out string reason)
        {
            var pose=body.Source.State;
            var operations=new List<BoxMotion4D>{new BoxMotion4D(pose,body.Half,requested.Position-pose.Position)};
            pose=pose.Translated(requested.Position-pose.Position);
            foreach(var step in RotationDecomposition4D.Between(pose.Rotation,requested.Rotation))
            {var motion=new BoxMotion4D(pose,body.Half,default,step.Plane,step.Angle);operations.Add(motion);pose=motion.At(1);}
            foreach(var motion in operations)
            {
                if(Prefix(body,motion,Obstacles(body,motion),out var blocker,out _)<1)
                {reason="4D: препятствие "+blocker?.Id;LastBlockedReason=reason;return false;}
            }
            world.CommitBodyPose(body.Source,requested);reason="";return true;
        }
    }
}
