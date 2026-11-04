using System;
using System.Collections.Generic;
namespace Section.Geometry4D
{
    // Exact union of the two approved D3 boxes, expressed in one canonical frame.
    public static class CompoundBox4D
    {
        public static Vector4D Center(int part)=>new Vector4D(0,0,0,part==0?.5:1.025);
        public static Vector4D Half(int part)=>part==0?new Vector4D(.2,.2,.2,.75):new Vector4D(1,.2,.2,.225);
        public static Transform4D Pose(Transform4D pose,int part)=>new Transform4D(pose.Position+pose.Rotation*Center(part),pose.Rotation);
        public static Halfspace4D[] Planes(Transform4D pose,int part)=>ConvexPrism4D.Planes(Half(part),default,Pose(pose,part));
        public static SliceResult[] Parts(Transform4D pose,double w)=>new[]{ConvexPrism4D.Slice(Half(0),default,Pose(pose,0),w),ConvexPrism4D.Slice(Half(1),default,Pose(pose,1),w)};
        // Clip each boundary polygon by the other solid, retaining only exterior pieces.
        public static SliceResult Slice(Transform4D pose,double w)
        {
            var parts=Parts(pose,w);var vertices=new List<Vector3D>();var triangles=new List<int>();var faces=new List<SliceFace>();
            for(int p=0;p<2;p++)
            {
                var section=parts[p];if(section.Kind!=SliceKind.Volume)continue;
                Vector3D shift=(pose.Rotation*Center(p)).XYZ;var other=parts[1-p];
                foreach(var face in section.Faces)
                {
                    var polygon=new List<Vector3D>();foreach(int i in face.Indices)polygon.Add(section.Vertices[i]+shift);
                    var pending=new List<List<Vector3D>>{polygon};var exterior=new List<List<Vector3D>>();
                    if(other.Kind==SliceKind.Volume)
                    {
                        var otherShift=(pose.Rotation*Center(1-p)).XYZ;
                        foreach(var cut in other.Faces)
                        {
                            var normal=cut.OutwardNormal;double offset=Vector3D.Dot(normal,other.Vertices[cut.Indices[0]]+otherShift);
                            var next=new List<List<Vector3D>>();
                            foreach(var poly in pending)
                            {
                                Split(poly,normal,offset,out var inside,out var outside);
                                if(outside.Count>=3)exterior.Add(outside);if(inside.Count>=3)next.Add(inside);
                            }
                            pending=next;if(pending.Count==0)break;
                        }
                    }
                    else exterior.Add(polygon);
                    // Overlapping coplanar outer faces must be emitted once, not removed twice.
                    if(other.Kind==SliceKind.Volume && p==0)
                        foreach(var poly in pending)
                            foreach(var f in other.Faces)
                                if(Vector3D.Dot(face.OutwardNormal,f.OutwardNormal)>.999999 && Math.Abs(Vector3D.Dot(f.OutwardNormal,poly[0]-other.Vertices[f.Indices[0]]-(pose.Rotation*Center(1-p)).XYZ))<1e-9){exterior.Add(poly);break;}
                    foreach(var poly in exterior)
                    {
                        int start=vertices.Count;vertices.AddRange(poly);var indices=new int[poly.Count];for(int i=0;i<indices.Length;i++)indices[i]=start+i;
                        for(int i=1;i<poly.Count-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}faces.Add(new SliceFace(indices,face.OutwardNormal));
                    }
                }
            }
            return vertices.Count==0?SliceResult.Empty():new SliceResult(SliceKind.Volume,3,vertices.ToArray(),triangles.ToArray(),faces.ToArray());
        }
        static void Split(List<Vector3D> polygon,Vector3D normal,double offset,out List<Vector3D> inside,out List<Vector3D> outside)
        {
            inside=new List<Vector3D>();outside=new List<Vector3D>();
            for(int i=0;i<polygon.Count;i++)
            {
                var a=polygon[i];var b=polygon[(i+1)%polygon.Count];double da=Vector3D.Dot(normal,a)-offset,db=Vector3D.Dot(normal,b)-offset;
                if(da<=1e-10)inside.Add(a);if(da>1e-10)outside.Add(a);
                if(da*db< -1e-20){var point=a+(b-a)*(da/(da-db));inside.Add(point);outside.Add(point);}
            }
        }
    }
}
