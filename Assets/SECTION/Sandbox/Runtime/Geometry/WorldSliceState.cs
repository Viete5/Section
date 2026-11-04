using System;
using System.Collections.Generic;
using Section.Geometry4D;
using UnityEngine;
namespace Section.Sandbox
{
    [DefaultExecutionOrder(-140)]
    public sealed class WorldSliceState : MonoBehaviour
    {
        [SerializeField] WorldShiftSettings settings;
        [SerializeField] SandboxMotor player;
        [SerializeField] GeometrySliceView[] sources=Array.Empty<GeometrySliceView>();
        [SerializeField] PhysicalItem[] conventionalItems=Array.Empty<PhysicalItem>();
        [SerializeField] double currentW;
        public double W=>currentW;
        public WorldShiftSettings Settings=>settings;
        public GeometrySliceView[] Sources=>sources;
        public PhysicalItem[] ConventionalItems=>conventionalItems;
        public SandboxMotor Player=>player;
        public bool IsCommitting { get; private set; }
        public string LastBlockedReason { get; private set; }="";
        public double LastSweepMilliseconds { get; private set; }
        public float LastResetTime { get; private set; }
        public void Configure(WorldShiftSettings config,SandboxMotor motor,GeometrySliceView[] views,PhysicalItem[] items)
        {settings=config;player=motor;sources=views;conventionalItems=items;foreach(var view in sources)view.BindWorld(this);}
        void Awake()
        {
            if(settings==null || player==null || sources.Length==0)throw new MissingReferenceException("World slice is not configured.");
            settings.Validate();var ids=new HashSet<string>();
            foreach(var view in sources){if(view==null || !ids.Add(view.Id))throw new InvalidOperationException("Missing/duplicate 4D identity.");view.BindWorld(this);}
        }
        void Start()=>Commit(currentW,null);
        public static Vector3D D(Vector3 value)=>new Vector3D(value.x,value.y,value.z);
        public Capsule3D PlayerVolume(Vector3 feet)
        {
            var cc=player.Controller;var center=feet+cc.center;
            double radius=cc.radius-cc.skinWidth; // Same non-penetrating inner envelope used by CharacterController contacts.
            float line=Mathf.Max(0,cc.height*.5f-cc.radius);
            return new Capsule3D(D(center-Vector3.up*line),D(center+Vector3.up*line),radius);
        }
        bool Intersects(GeometrySliceView view,Transform4D pose,Capsule3D capsule,double a,double b)
        {
            double bound=view.SpatialBoundRadius+capsule.Radius;
            if(SpatialSweep.PointSegmentSquared(pose.Position.XYZ,capsule.A,capsule.B)>bound*bound)return false;
            if(view.Shape==Shape4D.CompoundBox){for(int i=0;i<2;i++)if(SpatialSweep.IntersectsPolytopeInterval(capsule,CompoundBox4D.Planes(pose,i),a,b))return true;return false;}
            return view.Shape==Shape4D.Hypersphere?SpatialSweep.IntersectsSphereInterval(capsule,pose.Position,view.Extent,a,b):
                SpatialSweep.IntersectsPolytopeInterval(capsule,view.GetPlanes(pose),a,b);
        }
        string Obstruction(double a,double b,Capsule3D capsule,Dictionary<GeometrySliceView,Transform4D> overrides=null)
        {
            foreach(var view in sources)
            {
                if(!view.isActiveAndEnabled)continue;
                var pose=overrides!=null && overrides.TryGetValue(view,out var replacement)?replacement:view.State;
                if(Intersects(view,pose,capsule,a,b))return view.Id+": переход пересекает игрока";
                foreach(var item in conventionalItems)
                {
                    if(item==null || !item.isActiveAndEnabled)continue;
                    // Conservative ordinary-item envelope; never mutate or duplicate a 3D item on W change.
                    Vector3 center=item.Body.position+item.Body.rotation*Vector3.Scale(item.Shape.center,item.transform.lossyScale);
                    double radius=Vector3.Scale(item.Shape.size*.5f,item.transform.lossyScale).magnitude;
                    var volume=new Capsule3D(D(center),D(center),radius);
                    if(Intersects(view,pose,volume,a,b))return view.Id+": обычный предмет "+item.Id;
                }
            }
            return "";
        }
        public bool TrySetW(double requested)
        {
            Numeric.RequireFinite(requested,nameof(requested));
            double target=Math.Max(settings.minimumW,Math.Min(settings.maximumW,requested));
            if(Math.Abs(target-currentW)<1e-12)
            {LastBlockedReason=target==requested?"":"Предел диапазона W";return target==requested;}
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var capsule=PlayerVolume(player.transform.position);
            string reason=Obstruction(currentW,target,capsule);
            double accepted=target;
            if(reason.Length>0)
            {
                double safe=0,blocked=1;
                for(int i=0;i<20;i++)
                {double mid=(safe+blocked)*.5;double candidate=currentW+(target-currentW)*mid;
                    if(Obstruction(currentW,candidate,capsule).Length==0)safe=mid;else blocked=mid;}
                accepted=currentW+(target-currentW)*safe;
            }
            Commit(accepted,null);
            LastBlockedReason=reason.Length>0?reason:target==requested?"":"Предел диапазона W";
            timer.Stop();LastSweepMilliseconds=timer.Elapsed.TotalMilliseconds;
            return reason.Length==0 && target==requested;
        }
        public bool TrySetWExactly(double target,out string reason)
        {
            Numeric.RequireFinite(target,nameof(target));
            if(target<settings.minimumW || target>settings.maximumW){reason="Предел диапазона W";return false;}
            reason=Obstruction(currentW,target,PlayerVolume(player.transform.position));
            if(reason.Length>0){LastBlockedReason=reason;return false;}
            Commit(target,null);LastBlockedReason="";return true;
        }
        void Commit(double value,Dictionary<GeometrySliceView,Transform4D> poses)
        {
            IsCommitting=true;
            try
            {
                currentW=value;
                if(poses!=null)foreach(var pair in poses)pair.Key.SetState(pair.Value);
                foreach(var view in sources)view.SetSlice(currentW);
                Physics.SyncTransforms();
            }
            finally{IsCommitting=false;}
        }
        internal void CommitBodyPose(GeometrySliceView source,Transform4D pose)
        {Commit(currentW,new Dictionary<GeometrySliceView,Transform4D>{{source,pose}});}
        public bool TrySetBodyState(GeometrySliceView source,Transform4D pose,out string reason)
        {
            var body=source.GetComponent<FourDimensionalBody>();
            if(body!=null && body.isActiveAndEnabled)return body.Physics.TrySetState(body,pose,out reason);
            var poses=new Dictionary<GeometrySliceView,Transform4D>{{source,pose}};
            reason=Obstruction(currentW,currentW,PlayerVolume(player.transform.position),poses);
            if(reason.Length>0){LastBlockedReason=reason;return false;}
            Commit(currentW,poses);return true;
        }
        public bool TryRestore(GeometrySliceView[] views,bool resetW,Vector3? safeFeet,out string reason)
        {
            var poses=new Dictionary<GeometrySliceView,Transform4D>();
            foreach(var view in views)poses.Add(view,view.InitialState);
            var ignored=new HashSet<GeometrySliceView>(views);
            foreach(var view in views)
            {
                var body=view.GetComponent<FourDimensionalBody>();
                if(body!=null && body.isActiveAndEnabled && !body.Physics.CanPlace(body,view.InitialState,ignored,out reason))
                {LastBlockedReason=reason;return false;}
            }
            Vector3 feet=safeFeet??player.transform.position;
            var capsule=PlayerVolume(feet);
            if(safeFeet.HasValue)
            {
                foreach(var collider in Physics.OverlapCapsule(SliceMeshWriter.ToVector3(capsule.A),SliceMeshWriter.ToVector3(capsule.B),(float)capsule.Radius,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    if(collider!=player.Controller && collider.GetComponentInParent<SliceColliderView>()==null)
                    {reason="Место восстановления занято: "+collider.name;LastBlockedReason=reason;return false;}
            }
            double target=resetW?0:currentW;
            reason=Obstruction(currentW,target,capsule,poses);
            if(reason.Length>0){LastBlockedReason=reason;return false;}
            foreach(var view in views){var body=view.GetComponent<FourDimensionalBody>();if(body!=null)body.Restored();}
            Commit(target,poses);
            if(safeFeet.HasValue)player.RestoreSafePose(feet,Quaternion.identity);
            Physics.SyncTransforms();LastBlockedReason="";LastResetTime=Time.time;return true;
        }
    }
}
