using System;
namespace Section.Geometry4D
{
    public readonly struct Vector3D
    {
        public readonly double X, Y, Z;
        public Vector3D(double x, double y, double z) { X=x; Y=y; Z=z; }
        public double LengthSquared => Dot(this,this);
        public double Length => Math.Sqrt(LengthSquared);
        public bool IsFinite => Numeric.IsFinite(X) && Numeric.IsFinite(Y) && Numeric.IsFinite(Z);
        public Vector3D Normalized => Length>0 ? this/Length : default;
        public static double Dot(Vector3D a, Vector3D b) => a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        public static Vector3D Cross(Vector3D a, Vector3D b) => new Vector3D(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        public static Vector3D operator +(Vector3D a, Vector3D b) => new Vector3D(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static Vector3D operator -(Vector3D a, Vector3D b) => new Vector3D(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static Vector3D operator -(Vector3D a) => a*-1;
        public static Vector3D operator *(Vector3D a, double b) => new Vector3D(a.X*b,a.Y*b,a.Z*b);
        public static Vector3D operator /(Vector3D a, double b) => a*(1/b);
    }
    public static class Numeric
    {
        public static bool IsFinite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        public static void RequireFinite(double n, string name) { if(!IsFinite(n)) throw new ArgumentOutOfRangeException(name,"Value must be finite."); }
    }
}
