using System;
using System.Collections.Generic;
namespace Section.Geometry4D
{
    public enum Shape4D { Hypersphere, Tesseract, ConvexPrism, CompoundBox }
    public enum SliceKind { Empty, Degenerate, Volume }
    public sealed class SliceFace
    {
        public IReadOnlyList<int> Indices { get; }
        public Vector3D OutwardNormal { get; }
        public SliceFace(int[] indices,Vector3D normal){Indices=Array.AsReadOnly((int[])indices.Clone());OutwardNormal=normal;}
    }
    public sealed class SliceResult
    {
        public SliceKind Kind { get; }
        public int Dimension { get; }
        // All vertices are relative to the XYZ translation of the canonical 4D body.
        public IReadOnlyList<Vector3D> Vertices { get; }
        public IReadOnlyList<int> Triangles { get; }
        public IReadOnlyList<Vector3D> SmoothNormals { get; }
        public IReadOnlyList<SliceFace> Faces { get; }
        public double AnalyticRadius { get; }
        public Vector3D Minimum { get; }
        public Vector3D Maximum { get; }
        public SliceResult(SliceKind kind,int dimension,Vector3D[] vertices,int[] triangles,
            SliceFace[] faces=null,Vector3D[] smoothNormals=null,double radius=0)
        {
            Kind=kind;Dimension=dimension;AnalyticRadius=radius;
            Vertices=Array.AsReadOnly((Vector3D[])vertices.Clone());Triangles=Array.AsReadOnly((int[])triangles.Clone());
            Faces=Array.AsReadOnly(faces==null?Array.Empty<SliceFace>():(SliceFace[])faces.Clone());
            SmoothNormals=Array.AsReadOnly(smoothNormals==null?Array.Empty<Vector3D>():(Vector3D[])smoothNormals.Clone());
            if(vertices.Length>0)
            {
                double minX=double.PositiveInfinity,minY=minX,minZ=minX,maxX=double.NegativeInfinity,maxY=maxX,maxZ=maxX;
                foreach(var v in vertices){minX=Math.Min(minX,v.X);minY=Math.Min(minY,v.Y);minZ=Math.Min(minZ,v.Z);maxX=Math.Max(maxX,v.X);maxY=Math.Max(maxY,v.Y);maxZ=Math.Max(maxZ,v.Z);}
                Minimum=new Vector3D(minX,minY,minZ);Maximum=new Vector3D(maxX,maxY,maxZ);
            }
        }
        public static SliceResult Empty()=>new SliceResult(SliceKind.Empty,-1,Array.Empty<Vector3D>(),Array.Empty<int>());
    }
    public readonly struct SliceTolerance
    {
        public readonly double RelativeDistance,AbsoluteDistance,ParallelDeterminant;
        public SliceTolerance(double relativeDistance,double absoluteDistance,double parallelDeterminant)
        {
            if(!(relativeDistance>0&&absoluteDistance>0&&parallelDeterminant>0) || !Numeric.IsFinite(relativeDistance+absoluteDistance+parallelDeterminant))
                throw new ArgumentOutOfRangeException(nameof(relativeDistance));
            RelativeDistance=relativeDistance;AbsoluteDistance=absoluteDistance;ParallelDeterminant=parallelDeterminant;
        }
        public static SliceTolerance Default=>new SliceTolerance(1e-10,1e-12,1e-12);
        public double Distance(double size)=>Math.Max(AbsoluteDistance,Math.Abs(size)*RelativeDistance);
    }
    public static class SliceEngine
    {
        public static SliceResult Compute(Shape4D shape,double extent,Transform4D transform,double worldW,int sphereSubdivisions=3)
        {
            Numeric.RequireFinite(extent,nameof(extent));Numeric.RequireFinite(worldW,nameof(worldW));
            if(extent<=0)throw new ArgumentOutOfRangeException(nameof(extent));
            if(transform.Rotation==null)throw new ArgumentException("Transform must be initialized.",nameof(transform));
            switch(shape)
            {
                case Shape4D.Hypersphere:return HypersphereSlicer.Slice(extent,transform,worldW,sphereSubdivisions,SliceTolerance.Default);
                case Shape4D.Tesseract:return TesseractSlicer.Slice(extent,transform,worldW,SliceTolerance.Default);
                default:throw new ArgumentOutOfRangeException(nameof(shape));
            }
        }
    }
}
