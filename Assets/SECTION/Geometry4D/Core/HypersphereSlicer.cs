using System;
using System.Collections.Generic;
namespace Section.Geometry4D
{
    public static class HypersphereSlicer
    {
        sealed class UnitSphere
        {
            public Vector3D[] Vertices;
            public int[] Triangles;
        }
        static readonly object cacheLock=new object();
        static readonly Dictionary<int,UnitSphere> cache=new Dictionary<int,UnitSphere>();
        public static SliceResult Slice(double radius,Transform4D transform,double worldW,int subdivisions,SliceTolerance tolerance)
        {
            Numeric.RequireFinite(radius,nameof(radius));Numeric.RequireFinite(worldW,nameof(worldW));
            if(radius<=0)throw new ArgumentOutOfRangeException(nameof(radius));
            if(subdivisions<0 || subdivisions>5)throw new ArgumentOutOfRangeException(nameof(subdivisions));
            double distance=Math.Abs(worldW-transform.Position.W);
            double epsilon=tolerance.Distance(radius);
            if(distance>radius+epsilon)return SliceResult.Empty();
            if(distance>=radius-epsilon)return new SliceResult(SliceKind.Degenerate,0,new[]{new Vector3D(0,0,0)},Array.Empty<int>());
            double sectionRadius=Math.Sqrt((radius-distance)*(radius+distance));
            UnitSphere unit;
            lock(cacheLock)
            {if(!cache.TryGetValue(subdivisions,out unit)){unit=Build(subdivisions);cache.Add(subdivisions,unit);}}
            var vertices=new Vector3D[unit.Vertices.Length];
            for(int i=0;i<vertices.Length;i++)vertices[i]=unit.Vertices[i]*sectionRadius;
            return new SliceResult(SliceKind.Volume,3,vertices,unit.Triangles,null,unit.Vertices,sectionRadius);
        }
        static UnitSphere Build(int levels)
        {
            double t=(1+Math.Sqrt(5))/2;
            var vertices=new List<Vector3D>{new Vector3D(-1,t,0),new Vector3D(1,t,0),new Vector3D(-1,-t,0),new Vector3D(1,-t,0),
                new Vector3D(0,-1,t),new Vector3D(0,1,t),new Vector3D(0,-1,-t),new Vector3D(0,1,-t),
                new Vector3D(t,0,-1),new Vector3D(t,0,1),new Vector3D(-t,0,-1),new Vector3D(-t,0,1)};
            for(int i=0;i<vertices.Count;i++)vertices[i]=vertices[i].Normalized;
            var triangles=new List<int>{0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,
                3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1};
            for(int level=0;level<levels;level++)
            {
                var midpoint=new Dictionary<long,int>();var next=new List<int>(triangles.Count*4);
                int Middle(int a,int b)
                {
                    long key=((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);
                    if(midpoint.TryGetValue(key,out int index))return index;
                    index=vertices.Count;vertices.Add((vertices[a]+vertices[b]).Normalized);midpoint.Add(key,index);return index;
                }
                void Triangle(int a,int b,int c){next.Add(a);next.Add(b);next.Add(c);}
                for(int i=0;i<triangles.Count;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2],ab=Middle(a,b),bc=Middle(b,c),ca=Middle(c,a);
                    Triangle(a,ab,ca);Triangle(b,bc,ab);Triangle(c,ca,bc);Triangle(ab,bc,ca);
                }
                triangles=next;
            }
            for(int i=0;i<triangles.Count;i+=3)
                if(Vector3D.Dot(Vector3D.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]),vertices[triangles[i]])<0)
                {int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
            return new UnitSphere{Vertices=vertices.ToArray(),Triangles=triangles.ToArray()};
        }
    }
}
