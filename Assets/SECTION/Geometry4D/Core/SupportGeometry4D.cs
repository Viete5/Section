using System;
using System.Collections.Generic;
namespace Section.Geometry4D
{
    public interface ISupportShape4D
    {
        Vector4D Center { get; }
        Vector4D Support(Vector4D direction);
    }
    public sealed class BoxShape4D : ISupportShape4D
    {
        public Transform4D Pose { get; }
        public Vector4D Half { get; }
        public Vector4D Center=>Pose.Position;
        public BoxShape4D(Transform4D pose,Vector4D half){Pose=pose;Half=half;}
        public Vector4D Support(Vector4D direction)
        {
            var local=Pose.Rotation.Transposed()*direction;
            return Pose.TransformPoint(new Vector4D(local.X>=0?Half.X:-Half.X,local.Y>=0?Half.Y:-Half.Y,
                local.Z>=0?Half.Z:-Half.Z,local.W>=0?Half.W:-Half.W));
        }
    }
    public sealed class SphereShape4D : ISupportShape4D
    {
        public Vector4D Center { get; }
        readonly double radius;
        public SphereShape4D(Vector4D center,double r){Center=center;radius=r;}
        public Vector4D Support(Vector4D d)=>Center+(d.Length>1e-15?d/d.Length:new Vector4D(1,0,0,0))*radius;
    }
    public sealed class HullShape4D : ISupportShape4D
    {
        readonly Vector4D[] points;
        public Vector4D Center { get; }
        public HullShape4D(IReadOnlyList<Vector4D> vertices)
        {
            if(vertices.Count==0)throw new ArgumentException("Support hull must be nonempty.");
            points=new Vector4D[vertices.Count];Vector4D center=default;
            for(int i=0;i<points.Length;i++){points[i]=vertices[i];center+=vertices[i];}Center=center/points.Length;
        }
        public Vector4D Support(Vector4D d)
        {var best=points[0];double value=Vector4D.Dot(best,d);for(int i=1;i<points.Length;i++){double v=Vector4D.Dot(points[i],d);if(v>value){value=v;best=points[i];}}return best;}
    }
    public sealed class ProjectedShape4D : ISupportShape4D
    {
        readonly ISupportShape4D source;
        public Vector4D Center=>XYZ(source.Center);
        public ProjectedShape4D(ISupportShape4D shape)=>source=shape;
        static Vector4D XYZ(Vector4D v)=>new Vector4D(v.X,v.Y,v.Z,0);
        public Vector4D Support(Vector4D d)=>XYZ(source.Support(XYZ(d)));
    }
    // A 3D capsule embedded in the player's actual W slice, not a 4D capsule of W thickness.
    public sealed class SliceCapsuleShape4D : ISupportShape4D
    {
        readonly Vector4D a,b;readonly double radius;
        public Vector4D Center=>(a+b)*.5;
        public SliceCapsuleShape4D(Capsule3D capsule,double w)
        {a=new Vector4D(capsule.A.X,capsule.A.Y,capsule.A.Z,w);b=new Vector4D(capsule.B.X,capsule.B.Y,capsule.B.Z,w);radius=capsule.Radius;}
        public Vector4D Support(Vector4D d)
        {
            var end=Vector4D.Dot(a,d)>Vector4D.Dot(b,d)?a:b;double length=d.XYZ.Length;
            return end+(length>1e-15?new Vector4D(d.X/length,d.Y/length,d.Z/length,0)*radius:default);
        }
    }
    public static class LinearSystem4D
    {
        public static bool Solve(double[,] matrix,double[] rhs,out double[] solution)
        {
            int n=rhs.Length;var a=(double[,])matrix.Clone();var b=(double[])rhs.Clone();solution=new double[n];
            for(int k=0;k<n;k++)
            {
                int pivot=k;for(int r=k+1;r<n;r++)if(Math.Abs(a[r,k])>Math.Abs(a[pivot,k]))pivot=r;
                if(Math.Abs(a[pivot,k])<1e-14)return false;
                if(pivot!=k){for(int c=k;c<n;c++)(a[k,c],a[pivot,c])=(a[pivot,c],a[k,c]);(b[k],b[pivot])=(b[pivot],b[k]);}
                double scale=a[k,k];for(int c=k;c<n;c++)a[k,c]/=scale;b[k]/=scale;
                for(int r=0;r<n;r++)if(r!=k){double f=a[r,k];for(int c=k;c<n;c++)a[r,c]-=f*a[k,c];b[r]-=f*b[k];}
            }
            solution=b;return true;
        }
        public static Vector4D[] Vertices(Halfspace4D[] planes)
        {
            var result=new List<Vector4D>();
            for(int a=0;a<planes.Length;a++)for(int b=a+1;b<planes.Length;b++)for(int c=b+1;c<planes.Length;c++)for(int d=c+1;d<planes.Length;d++)
            {
                int[] ids={a,b,c,d};var m=new double[4,4];var rhs=new double[4];
                for(int r=0;r<4;r++){rhs[r]=planes[ids[r]].Offset;for(int col=0;col<4;col++)m[r,col]=planes[ids[r]].Normal[col];}
                if(!Solve(m,rhs,out var x))continue;var v=new Vector4D(x[0],x[1],x[2],x[3]);bool inside=v.IsFinite;
                foreach(var p in planes)if(Vector4D.Dot(p.Normal,v)>p.Offset+1e-8){inside=false;break;}
                if(!inside)continue;bool unique=true;foreach(var p in result)if((p-v).Length<1e-8){unique=false;break;}if(unique)result.Add(v);
            }
            return result.ToArray();
        }
    }
}
