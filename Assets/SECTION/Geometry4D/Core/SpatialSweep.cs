using System;
using System.Collections.Generic;
namespace Section.Geometry4D
{
    public readonly struct Capsule3D
    {
        public readonly Vector3D A,B;
        public readonly double Radius;
        public Capsule3D(Vector3D a,Vector3D b,double radius)
        {if(!a.IsFinite || !b.IsFinite || !(radius>0) || !Numeric.IsFinite(radius))throw new ArgumentOutOfRangeException(nameof(radius));A=a;B=b;Radius=radius;}
    }
    public static class SpatialSweep
    {
        public static bool IntersectsSphereInterval(Capsule3D capsule,Vector4D center,double radius,double from,double to)
        {
            double nearest=Math.Max(Math.Min(from,to),Math.Min(Math.Max(from,to),center.W));
            double dw=nearest-center.W;
            if(Math.Abs(dw)>=radius)return false; // tangent 4D point has no solid volume
            double sectionRadius=Math.Sqrt((radius-Math.Abs(dw))*(radius+Math.Abs(dw)));
            double distance=PointSegmentSquared(center.XYZ,capsule.A,capsule.B);
            double combined=sectionRadius+capsule.Radius;
            return distance < combined*combined-1e-12;
        }
        public static bool IntersectsPolytopeInterval(Capsule3D capsule,Halfspace4D[] planes,double from,double to)
        {
            var projected=ConvexPrism4D.ProjectInterval(planes,from,to,out bool empty);
            if(empty)return false;
            // Exact segment/convex-volume intersection before measuring its boundary distance.
            double low=0,high=1;
            foreach(var p in projected)
            {
                double d=p.D-Vector3D.Dot(p.N,capsule.A),v=Vector3D.Dot(p.N,capsule.B-capsule.A);
                if(Math.Abs(v)<1e-12){if(d<0){low=2;break;}}
                else if(v>0)high=Math.Min(high,d/v);else low=Math.Max(low,d/v);
            }
            if(low<=high)return true;
            var surface=ConvexSectionBuilder.Build(projected,10,SliceTolerance.Default);
            double squared=capsule.Radius*capsule.Radius;
            for(int i=0;i<surface.Triangles.Count;i+=3)
            {
                var a=surface.Vertices[surface.Triangles[i]];var b=surface.Vertices[surface.Triangles[i+1]];var c=surface.Vertices[surface.Triangles[i+2]];
                if(SegmentTriangleSquared(capsule.A,capsule.B,a,b,c)<squared-1e-12)return true;
            }
            // A zero-thickness projected prism is still a possible boundary contact.
            foreach(var p in surface.Vertices)if(PointSegmentSquared(p,capsule.A,capsule.B)<squared-1e-12)return true;
            return false;
        }
        public static double PointSegmentSquared(Vector3D p,Vector3D a,Vector3D b)
        {var d=b-a;double t=d.LengthSquared<1e-24?0:Math.Max(0,Math.Min(1,Vector3D.Dot(p-a,d)/d.LengthSquared));return (p-a-d*t).LengthSquared;}
        static double SegmentSegmentSquared(Vector3D p,Vector3D q,Vector3D a,Vector3D b)
        {
            var d=q-p;var e=b-a;var r=p-a;double dd=d.LengthSquared,ee=e.LengthSquared,de=Vector3D.Dot(d,e),dr=Vector3D.Dot(d,r),er=Vector3D.Dot(e,r);
            if(dd<1e-24)return PointSegmentSquared(p,a,b);if(ee<1e-24)return PointSegmentSquared(a,p,q);
            double denominator=dd*ee-de*de;
            double s=denominator>1e-24?Math.Max(0,Math.Min(1,(de*er-dr*ee)/denominator)):0;
            double t=(de*s+er)/ee;
            if(t<0){t=0;s=Math.Max(0,Math.Min(1,-dr/dd));}
            else if(t>1){t=1;s=Math.Max(0,Math.Min(1,(de-dr)/dd));}
            return (r+d*s-e*t).LengthSquared;
        }
        static double PointTriangleSquared(Vector3D p,Vector3D a,Vector3D b,Vector3D c)
        {
            var n=Vector3D.Cross(b-a,c-a);double nn=n.LengthSquared;
            if(nn>1e-24)
            {
                var projected=p-n*(Vector3D.Dot(p-a,n)/nn);
                if(InsideTriangle(projected,a,b,c,n))return (p-projected).LengthSquared;
            }
            return Math.Min(PointSegmentSquared(p,a,b),Math.Min(PointSegmentSquared(p,b,c),PointSegmentSquared(p,c,a)));
        }
        static bool InsideTriangle(Vector3D p,Vector3D a,Vector3D b,Vector3D c,Vector3D n)=>
            Vector3D.Dot(Vector3D.Cross(b-a,p-a),n)>=-1e-12 && Vector3D.Dot(Vector3D.Cross(c-b,p-b),n)>=-1e-12 && Vector3D.Dot(Vector3D.Cross(a-c,p-c),n)>=-1e-12;
        static double SegmentTriangleSquared(Vector3D p,Vector3D q,Vector3D a,Vector3D b,Vector3D c)
        {
            var n=Vector3D.Cross(b-a,c-a);double denominator=Vector3D.Dot(q-p,n);
            if(Math.Abs(denominator)>1e-14)
            {double t=Vector3D.Dot(a-p,n)/denominator;if(t>=0 && t<=1 && InsideTriangle(p+(q-p)*t,a,b,c,n))return 0;}
            return Math.Min(Math.Min(PointTriangleSquared(p,a,b,c),PointTriangleSquared(q,a,b,c)),
                Math.Min(SegmentSegmentSquared(p,q,a,b),Math.Min(SegmentSegmentSquared(p,q,b,c),SegmentSegmentSquared(p,q,c,a))));
        }
    }
}
