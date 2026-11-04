using System;
using System.Collections.Generic;
using System.Linq;
namespace Section.Geometry4D
{
    public static class TesseractSlicer
    {
        public static SliceResult Slice(double halfExtent,Transform4D transform,double worldW,SliceTolerance tolerance)
        {
            Numeric.RequireFinite(worldW,nameof(worldW));Numeric.RequireFinite(halfExtent,nameof(halfExtent));
            if(halfExtent<=0)throw new ArgumentOutOfRangeException(nameof(halfExtent));
            var rotation=transform.Rotation??throw new ArgumentException("Uninitialized transform.");
            double epsilon=tolerance.Distance(halfExtent);
            double delta=worldW-transform.Position.W;
            double support=0;for(int i=0;i<4;i++)support+=halfExtent*Math.Abs(rotation[3,i]);
            if(Math.Abs(delta)>support+epsilon)return SliceResult.Empty();
            var planes=new List<Halfspace3D>(8);
            // R^T * (x,y,z,deltaW) lies inside the original [-a,a]^4 box.
            for(int axis=0;axis<4;axis++)
            {
                var n=new Vector3D(rotation[0,axis],rotation[1,axis],rotation[2,axis]);
                double length=n.Length;
                for(int sign=-1;sign<=1;sign+=2)
                {
                    double d=halfExtent-sign*rotation[3,axis]*delta;
                    if(length<=tolerance.ParallelDeterminant)
                    {if(d < -epsilon)return SliceResult.Empty();continue;}
                    planes.Add(new Halfspace3D(n*(sign/length),d/length));
                }
            }
            return ConvexSectionBuilder.Build(planes,halfExtent,tolerance);
        }
    }
}
