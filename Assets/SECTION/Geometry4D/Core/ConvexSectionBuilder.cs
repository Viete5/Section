using System;
using System.Collections.Generic;
using System.Linq;
namespace Section.Geometry4D
{
    public readonly struct Halfspace3D
    {
        public readonly Vector3D N;
        public readonly double D;
        public Halfspace3D(Vector3D normal,double offset){N=normal;D=offset;}
    }
    // Shared face construction for every convex 4D section and projected sweep.
    public static class ConvexSectionBuilder
    {
        public static SliceResult Build(IReadOnlyList<Halfspace3D> planes,double scale,SliceTolerance tolerance)
        {
            double epsilon=tolerance.Distance(scale);
            var vertices=new List<Vector3D>(32);
            for(int a=0;a<planes.Count;a++)for(int b=a+1;b<planes.Count;b++)for(int c=b+1;c<planes.Count;c++)
            {
                var p=planes[a];var q=planes[b];var r=planes[c];
                var qr=Vector3D.Cross(q.N,r.N);double determinant=Vector3D.Dot(p.N,qr);
                if(Math.Abs(determinant)<=tolerance.ParallelDeterminant)continue;
                var v=(qr*p.D+Vector3D.Cross(r.N,p.N)*q.D+Vector3D.Cross(p.N,q.N)*r.D)/determinant;
                if(!v.IsFinite || planes.Any(plane=>Vector3D.Dot(plane.N,v)>plane.D+epsilon*4))continue;
                if(vertices.All(existing=>(existing-v).Length>epsilon*4))vertices.Add(v);
            }
            if(vertices.Count==0)return SliceResult.Empty();
            int dimension=AffineDimension(vertices,epsilon*8);
            if(dimension<3)return new SliceResult(SliceKind.Degenerate,dimension,vertices.ToArray(),Array.Empty<int>());
            var faces=new List<SliceFace>();var triangles=new List<int>();var seen=new HashSet<string>();
            foreach(var plane in planes)
            {
                var indices=Enumerable.Range(0,vertices.Count).Where(i=>Math.Abs(Vector3D.Dot(plane.N,vertices[i])-plane.D)<=epsilon*8).ToList();
                if(indices.Count<3)continue;
                string key=string.Join(",",indices.OrderBy(i=>i));
                if(!seen.Add(key))continue; // coincident 4D constraints must not produce duplicate 3D faces
                Vector3D center=default;foreach(int i in indices)center+=vertices[i];center/=indices.Count;
                var hint=Math.Abs(plane.N.X)<.7?new Vector3D(1,0,0):new Vector3D(0,1,0);
                var u=Vector3D.Cross(hint,plane.N).Normalized;var v=Vector3D.Cross(plane.N,u);
                indices.Sort((i,j)=>Math.Atan2(Vector3D.Dot(vertices[i]-center,v),Vector3D.Dot(vertices[i]-center,u)).CompareTo(
                    Math.Atan2(Vector3D.Dot(vertices[j]-center,v),Vector3D.Dot(vertices[j]-center,u))));
                // Remove only numerically collinear points, preserving real small faces.
                bool removed=true;
                while(removed && indices.Count>3)
                {
                    removed=false;
                    for(int i=0;i<indices.Count;i++)
                    {
                        var before=vertices[indices[(i+indices.Count-1)%indices.Count]];
                        var at=vertices[indices[i]];var after=vertices[indices[(i+1)%indices.Count]];
                        double edgeScale=Math.Max((at-before).Length,(after-at).Length);
                        if(Vector3D.Cross(at-before,after-at).Length<=epsilon*edgeScale)
                        {indices.RemoveAt(i);removed=true;break;}
                    }
                }
                var cross=Vector3D.Cross(vertices[indices[1]]-vertices[indices[0]],vertices[indices[2]]-vertices[indices[0]]);
                if(cross.Length<=epsilon*epsilon)continue;
                if(Vector3D.Dot(cross,plane.N)<0)indices.Reverse();
                faces.Add(new SliceFace(indices.ToArray(),plane.N));
                for(int i=1;i<indices.Count-1;i++)
                {triangles.Add(indices[0]);triangles.Add(indices[i]);triangles.Add(indices[i+1]);}
            }
            return new SliceResult(SliceKind.Volume,3,vertices.ToArray(),triangles.ToArray(),faces.ToArray());
        }
        static int AffineDimension(List<Vector3D> points,double epsilon)
        {
            var origin=points[0];var a=points.OrderByDescending(p=>(p-origin).LengthSquared).First()-origin;
            if(a.Length<=epsilon)return 0;
            var b=points.OrderByDescending(p=>Vector3D.Cross(a,p-origin).LengthSquared).First()-origin;
            var normal=Vector3D.Cross(a,b);
            if(normal.Length<=epsilon*a.Length)return 1;
            normal=normal.Normalized;
            return points.Any(p=>Math.Abs(Vector3D.Dot(normal,p-origin))>epsilon)?3:2;
        }
    }
}
