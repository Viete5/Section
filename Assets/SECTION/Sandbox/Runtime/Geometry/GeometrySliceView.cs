using System;
using System.Globalization;
using Section.Geometry4D;
using UnityEngine;
namespace Section.Sandbox
{
    [ExecuteAlways,RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class GeometrySliceView : MonoBehaviour
    {
        [SerializeField] string stableId="Geometry";
        [SerializeField] Shape4D shape;
        [SerializeField] double extent=1.6;
        [SerializeField] int sphereSubdivisions=3;
        [SerializeField] double halfX=.9,halfY=.125,halfZ=.2,halfW=.325,shearX,shearY,shearZ;
        [Serializable] public struct CanonicalPlane
        {
            public double x,y,z,w,offset;
            public Halfspace4D Value=>new Halfspace4D(new Vector4D(x,y,z,w),offset);
        }
        [SerializeField] CanonicalPlane[] clips=Array.Empty<CanonicalPlane>();
        [SerializeField] WorldSliceState world;
        [SerializeField,HideInInspector] double initialX,initialY,initialZ,initialW;
        [SerializeField,HideInInspector] double x,y,z,w;
        [SerializeField] double initialXW;
        [SerializeField,HideInInspector] double sliceW;
        [SerializeField,HideInInspector] double[] orientation={1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1};
        Mesh mesh;
        string builtSignature;
        bool pending;
        public string Id=>stableId;
        public Shape4D Shape=>shape;
        public double Extent=>extent;
        public double SliceW=>sliceW;
        public Transform4D State=>new Transform4D(new Vector4D(x,y,z,w),new Matrix4D(orientation));
        public Transform4D InitialState=>new Transform4D(new Vector4D(initialX,initialY,initialZ,initialW),Matrix4D.Rotation(RotationPlane.XW,initialXW));
        public SliceResult CurrentSlice { get; private set; }
        public SliceRenderStatus RenderStatus { get; private set; }
        public int MeshBuildCount { get; private set; }
        public double LastBuildMilliseconds { get; private set; }
        public Mesh GeneratedMesh=>mesh;
        public WorldSliceState World=>world;
        public double SpatialBoundRadius
        {
            get
            {
                if(shape==Shape4D.CompoundBox)return 1.65;
                if(shape==Shape4D.Hypersphere)return extent;
                if(shape==Shape4D.Tesseract)return 2*extent;
                var h=PrismHalfSize;var s=PrismShear;
                double bx=h.X+Math.Abs(s.X)*h.W,by=h.Y+Math.Abs(s.Y)*h.W,bz=h.Z+Math.Abs(s.Z)*h.W;
                return Math.Sqrt(bx*bx+by*by+bz*bz+h.W*h.W);
            }
        }
        public Vector4D PrismHalfSize=>new Vector4D(halfX,halfY,halfZ,halfW);
        public Vector3D PrismShear=>new Vector3D(shearX,shearY,shearZ);
        public void BindWorld(WorldSliceState owner)=>world=owner;
        public SliceResult ComputeSliceAt(double value,Transform4D state)=>shape==Shape4D.CompoundBox?CompoundBox4D.Slice(state,value):shape==Shape4D.ConvexPrism?
            ConvexPrism4D.Slice(PrismHalfSize,PrismShear,state,value,ClipValues()):SliceEngine.Compute(shape,extent,state,value,sphereSubdivisions);
        public Halfspace4D[] GetPlanes(Transform4D state)=>ConvexPrism4D.Planes(shape==Shape4D.Tesseract?
            new Vector4D(extent,extent,extent,extent):PrismHalfSize,shape==Shape4D.Tesseract?default:PrismShear,state,shape==Shape4D.Tesseract?null:ClipValues());
        Halfspace4D[] ClipValues()=>Array.ConvertAll(clips,p=>p.Value);
        public event Action<GeometrySliceView> SliceChanged;
        public string ConfigurationSignature
        {
            get
            {
                string F(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
                var values=new string[orientation.Length];for(int i=0;i<values.Length;i++)values[i]=F(orientation[i]);
                return stableId+":"+shape+":"+F(extent)+":"+sphereSubdivisions+":"+F(initialX)+","+F(initialY)+","+F(initialZ)+","+F(initialW)+":"+
                    (initialXW==0?"":F(initialXW)+":")+F(x)+","+F(y)+","+F(z)+","+F(w)+":"+F(sliceW)+":"+string.Join(",",values)+
                    (shape==Shape4D.ConvexPrism?":"+F(halfX)+","+F(halfY)+","+F(halfZ)+","+F(halfW)+":"+F(shearX)+","+F(shearY)+","+F(shearZ)+(clips.Length==0?"":":"+
                        string.Join(";",Array.ConvertAll(clips,p=>F(p.x)+","+F(p.y)+","+F(p.z)+","+F(p.w)+","+F(p.offset)))):"");
            }
        }
        public void Configure(string id,Shape4D type,double size,Vector4D position)
        {
            stableId=id;shape=type;extent=size;
            initialX=position.X;initialY=position.Y;initialZ=position.Z;initialW=position.W;
            x=position.X;y=position.Y;z=position.Z;w=position.W;orientation=Matrix4D.Identity.ToArray();sliceW=0;
            Rebuild();
        }
        public void SetState(Transform4D value)
        {
            SliceMeshWriter.ToVector3(value.Position.XYZ);
            if(value.Rotation==null)throw new ArgumentException("Uninitialized transform.",nameof(value));
            if(Application.isPlaying && world!=null && !world.IsCommitting)
            {if(!world.TrySetBodyState(this,value,out string reason))throw new InvalidOperationException(reason);return;}
            x=value.Position.X;y=value.Position.Y;z=value.Position.Z;w=value.Position.W;orientation=value.Rotation.ToArray();Rebuild();
        }
        public void SetSlice(double value)
        {
            Numeric.RequireFinite(value,nameof(value));
            if(Application.isPlaying && world!=null && !world.IsCommitting){world.TrySetW(value);return;}
            sliceW=value;Rebuild();
        }
        public void ConfigureCompound(string id,Vector4D center)
        {initialXW=Math.PI*.5;Configure(id,Shape4D.CompoundBox,1,center);SetState(InitialState);}
        public void ConfigurePrism(string id,Vector4D center,Vector4D halfSize,Vector3D shear,CanonicalPlane[] clipping=null)
        {halfX=halfSize.X;halfY=halfSize.Y;halfZ=halfSize.Z;halfW=halfSize.W;shearX=shear.X;shearY=shear.Y;shearZ=shear.Z;clips=clipping??Array.Empty<CanonicalPlane>();Configure(id,Shape4D.ConvexPrism,1,center);}
        public void Rotate(RotationPlane plane,double degrees,bool local=false)
        {Numeric.RequireFinite(degrees,nameof(degrees));SetState(State.RotatedAboutCenter(plane,Math.IEEERemainder(degrees,360)*Math.PI/180,local));}
        public void ResetTransform()=>SetState(InitialState);
        void OnEnable()=>Rebuild();
        void OnValidate()=>pending=true;
        void Update(){if(pending){pending=false;Rebuild();}}
        public void Rebuild()
        {
            var canonical=State;
            var position=SliceMeshWriter.ToVector3(canonical.Position.XYZ);
            transform.SetPositionAndRotation(position,Quaternion.identity);transform.localScale=Vector3.one;
            string signature=ConfigurationSignature;
            if(signature==builtSignature && mesh!=null)return;
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var result=ComputeSliceAt(sliceW,canonical);
            if(mesh==null){mesh=new Mesh{name=stableId+"_ComputedSlice",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();}
            mesh.name=stableId+"_ComputedSlice";
            RenderStatus=SliceMeshWriter.Write(mesh,result);
            transform.SetPositionAndRotation(position,Quaternion.identity);transform.localScale=Vector3.one;
            GetComponent<MeshFilter>().sharedMesh=mesh;
            GetComponent<MeshRenderer>().enabled=RenderStatus==SliceRenderStatus.Ready;
            CurrentSlice=result;builtSignature=signature;MeshBuildCount++;pending=false;
            timer.Stop();LastBuildMilliseconds=timer.Elapsed.TotalMilliseconds;
            SliceChanged?.Invoke(this);
        }
        void OnDisable()
        {
            CurrentSlice=SliceResult.Empty();RenderStatus=SliceRenderStatus.NoVolume;
            if(mesh!=null)mesh.Clear();
            SliceChanged?.Invoke(this);
            if(mesh==null)return;
            var filter=GetComponent<MeshFilter>();if(filter!=null && filter.sharedMesh==mesh)filter.sharedMesh=null;
            if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);
            mesh=null;builtSignature=null;
        }
    }
}
