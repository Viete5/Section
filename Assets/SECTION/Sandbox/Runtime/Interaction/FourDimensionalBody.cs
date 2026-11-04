using System;
using Section.Geometry4D;
using UnityEngine;
namespace Section.Sandbox
{
    [RequireComponent(typeof(GeometrySliceView))]
    [DefaultExecutionOrder(-70)]
    public sealed class FourDimensionalBody : MonoBehaviour
    {
        [SerializeField] FourDimensionalPhysics physics;
        GeometrySliceView source;
        DimensionManipulator owner;
        Vector3 velocity;
        float stableTime;
        public GeometrySliceView Source=>source!=null?source:GetComponent<GeometrySliceView>();
        public FourDimensionalPhysics Physics=>physics;
        public string Id=>Source.Id;
        public DimensionManipulator Owner=>owner;
        public bool IsHeld=>owner!=null;
        public Vector3 LinearVelocity=>velocity;
        public double AngularSpeed=>0; // M5 has servo rotations, not an unapproved free angular-dynamics solver.
        public Collider SupportingCollider { get; private set; }
        public bool IsStable=>!IsHeld && SupportingCollider!=null && velocity.sqrMagnitude<.0025f && stableTime>=.2f;
        public int PartCount=>Source.Shape==Shape4D.CompoundBox?2:1;
        public Vector4D PartHalf(int p)=>PartCount==1?Half:CompoundBox4D.Half(p);
        public Vector4D PartCenter(int p)=>PartCount==1?default:CompoundBox4D.Center(p);
        public Vector4D Half=>Source.Shape==Shape4D.CompoundBox?new Vector4D(1,.2,.2,1.25):Source.Shape==Shape4D.Tesseract?new Vector4D(Source.Extent,Source.Extent,Source.Extent,Source.Extent):Source.PrismHalfSize;
        public event Action<BoxMotion4D,double> MotionAccepted;
        public void Configure(FourDimensionalPhysics solver)=>physics=solver;
        void Awake()
        {
            source=GetComponent<GeometrySliceView>();if(physics==null)throw new MissingReferenceException("4D body has no physics: "+Id);
            if(source.Shape!=Shape4D.CompoundBox && source.Shape!=Shape4D.Tesseract && (source.Shape!=Shape4D.ConvexPrism || source.PrismShear.Length>0 || source.GetPlanes(source.State).Length!=8))
                throw new InvalidOperationException("M5 movable body must be an uncut 4D orthogonal box: "+Id);
        }
        public bool TryClaim(DimensionManipulator claimant)
        {if(owner!=null || !isActiveAndEnabled)return false;owner=claimant;velocity=Vector3.zero;stableTime=0;SupportingCollider=null;return true;}
        public void Release(DimensionManipulator claimant)
        {if(owner!=claimant)return;owner=null;velocity=Vector3.zero;stableTime=0;SupportingCollider=null;}
        internal void Restored()
        {if(owner!=null)owner.Release("Локальный сброс 4D-предмета");velocity=Vector3.zero;stableTime=0;SupportingCollider=null;}
        internal void Applied(BoxMotion4D motion,double fraction){MotionAccepted?.Invoke(motion,fraction);}
        void FixedUpdate()
        {
            if(IsHeld)return;
            float dt=Time.fixedDeltaTime;
            velocity+=Vector3.down*9.81f*dt;velocity=Vector3.ClampMagnitude(velocity,12);
            var motion=new BoxMotion4D(Source.State,Half,new Vector4D(velocity.x*dt,velocity.y*dt,velocity.z*dt,0));
            var result=physics.Move(this,motion);
            if(result.Fraction<1)
            {SupportingCollider=result.Collider;velocity=Vector3.zero;stableTime+=dt;}
            else{SupportingCollider=null;stableTime=0;}
        }
        void OnDisable(){if(owner!=null)owner.Release("4D-предмет отключён");}
    }
}
