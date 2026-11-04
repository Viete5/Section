using System;
using System.Collections.Generic;
using Section.Geometry4D;
using UnityEngine;
using UnityEngine.Rendering;
namespace Section.Sandbox
{
    public enum SliceRenderStatus { Ready, NoVolume, BelowFloatPrecision }
    public static class SliceMeshWriter
    {
        public static SliceRenderStatus Write(Mesh mesh,SliceResult slice)
        {
            mesh.Clear();
            if(slice.Kind!=SliceKind.Volume)return SliceRenderStatus.NoVolume;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            if(slice.Faces.Count>0)
            {
                foreach(var face in slice.Faces)
                {
                    int start=vertices.Count;
                    foreach(int index in face.Indices){vertices.Add(ToVector3(slice.Vertices[index]));normals.Add(ToVector3(face.OutwardNormal));}
                    for(int i=1;i<face.Indices.Count-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
                }
            }
            else
            {
                foreach(var v in slice.Vertices)vertices.Add(ToVector3(v));
                foreach(var n in slice.SmoothNormals)normals.Add(ToVector3(n));
                foreach(int i in slice.Triangles)triangles.Add(i);
            }
            if(vertices.Count!=normals.Count)throw new InvalidOperationException("Every rendered vertex requires a computed normal.");
            // A valid double section can be smaller than float resolution. Never upload a collapsed solid.
            for(int i=0;i<triangles.Count;i+=3)
            {
                var cross=Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]);
                if(!(cross.sqrMagnitude>0) || !float.IsFinite(cross.sqrMagnitude))return SliceRenderStatus.BelowFloatPrecision;
            }
            mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            return SliceRenderStatus.Ready;
        }
        public static Vector3 ToVector3(Vector3D v)
        {
            if(!v.IsFinite || Math.Abs(v.X)>1e7 || Math.Abs(v.Y)>1e7 || Math.Abs(v.Z)>1e7)
                throw new ArgumentOutOfRangeException(nameof(v),"Geometry is outside the supported Unity presentation range.");
            return new Vector3((float)v.X,(float)v.Y,(float)v.Z);
        }
    }
}
