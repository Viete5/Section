using System;
using System.Collections.Generic;
namespace Section.Geometry4D
{
    public readonly struct Halfspace4D
    {
        public readonly Vector4D Normal;
        public readonly double Offset;
        public Halfspace4D(Vector4D normal,double offset){Normal=normal;Offset=offset;}
    }

    // A bounded, genuine 4D parallelepiped: |xyz - shear*w| <= halfSize, |w| <= halfW.
    // Shear is part of its canonical geometry, not a visual animation or arbitrary rotation substitute.
    public static class ConvexPrism4D
    {
        public static Halfspace4D[] Planes(Vector4D halfSize,Vector3D shear,Transform4D transform,IReadOnlyList<Halfspace4D> clips=null)
        {
            if(!(halfSize.X>0 && halfSize.Y>0 && halfSize.Z>0 && halfSize.W>0) || !halfSize.IsFinite || !shear.IsFinite)
                throw new ArgumentOutOfRangeException(nameof(halfSize));
            var local=new[]{new Vector4D(1,0,0,-shear.X),new Vector4D(0,1,0,-shear.Y),
                new Vector4D(0,0,1,-shear.Z),new Vector4D(0,0,0,1)};
            var sizes=new[]{halfSize.X,halfSize.Y,halfSize.Z,halfSize.W};
            var result=new Halfspace4D[8+(clips?.Count??0)];
            for(int axis=0;axis<4;axis++)for(int side=0;side<2;side++)
            {
                var n=transform.Rotation*local[axis]*(side==0?-1:1);
                result[axis*2+side]=new Halfspace4D(n,sizes[axis]+Vector4D.Dot(n,transform.Position));
            }
            if(clips!=null)for(int i=0;i<clips.Count;i++)
            {
                var clip=clips[i];
                if(!clip.Normal.IsFinite || clip.Normal.Length<1e-12 || !Numeric.IsFinite(clip.Offset))throw new ArgumentException("Invalid canonical clipping halfspace.");
                var n=transform.Rotation*clip.Normal;
                result[8+i]=new Halfspace4D(n,clip.Offset+Vector4D.Dot(n,transform.Position));
            }
            return result;
        }
        public static SliceResult Slice(Vector4D halfSize,Vector3D shear,Transform4D transform,double w,IReadOnlyList<Halfspace4D> clips=null)
        {
            Numeric.RequireFinite(w,nameof(w));
            var planes=Planes(halfSize,shear,new Transform4D(new Vector4D(0,0,0,transform.Position.W),transform.Rotation),clips);
            var section=new List<Halfspace3D>();
            foreach(var p in planes)
            {
                double d=p.Offset-p.Normal.W*w;double length=p.Normal.XYZ.Length;
                if(length<1e-12){if(d < -1e-10)return SliceResult.Empty();continue;}
                section.Add(new Halfspace3D(p.Normal.XYZ/length,d/length));
            }
            return ConvexSectionBuilder.Build(section,Math.Max(Math.Max(halfSize.X,halfSize.Y),Math.Max(halfSize.Z,halfSize.W)),SliceTolerance.Default);
        }

        // Exact Fourier-Motzkin projection of P intersect {a <= w <= b} onto XYZ.
        // The result represents the UNION of all intermediate sections, not sampled endpoints.
        public static Halfspace3D[] ProjectInterval(Halfspace4D[] source,double a,double b,out bool empty)
        {
            Numeric.RequireFinite(a,nameof(a));Numeric.RequireFinite(b,nameof(b));
            var positive=new List<Halfspace4D>();var negative=new List<Halfspace4D>();var result=new List<Halfspace3D>();
            var planes=new List<Halfspace4D>(source){new Halfspace4D(new Vector4D(0,0,0,1),Math.Max(a,b)),
                new Halfspace4D(new Vector4D(0,0,0,-1),-Math.Min(a,b))};
            empty=false;
            bool Add(Vector3D n,double d)
            {
                double length=n.Length;
                if(length<1e-12){if(d < -1e-10)return false;return true;}
                result.Add(new Halfspace3D(n/length,d/length));return true;
            }
            foreach(var p in planes)
            {
                if(p.Normal.W>1e-12)positive.Add(p);
                else if(p.Normal.W < -1e-12)negative.Add(p);
                else if(!Add(p.Normal.XYZ,p.Offset)){empty=true;return Array.Empty<Halfspace3D>();}
            }
            foreach(var p in positive)foreach(var n in negative)
                if(!Add(p.Normal.XYZ/p.Normal.W-n.Normal.XYZ/n.Normal.W,p.Offset/p.Normal.W-n.Offset/n.Normal.W))
                {empty=true;return Array.Empty<Halfspace3D>();}
            return result.ToArray();
        }
    }
}
