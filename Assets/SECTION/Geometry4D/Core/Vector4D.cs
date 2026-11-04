using System;
namespace Section.Geometry4D
{
    public readonly struct Vector4D
    {
        public readonly double X, Y, Z, W;
        public Vector4D(double x,double y,double z,double w){X=x;Y=y;Z=z;W=w;}
        public double this[int index] => index switch {0=>X,1=>Y,2=>Z,3=>W,_=>throw new ArgumentOutOfRangeException(nameof(index))};
        public Vector3D XYZ => new Vector3D(X,Y,Z);
        public bool IsFinite => XYZ.IsFinite && Numeric.IsFinite(W);
        public double LengthSquared => Dot(this,this);
        public double Length => Math.Sqrt(LengthSquared);
        public static double Dot(Vector4D a,Vector4D b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z+a.W*b.W;
        public static Vector4D operator +(Vector4D a,Vector4D b)=>new Vector4D(a.X+b.X,a.Y+b.Y,a.Z+b.Z,a.W+b.W);
        public static Vector4D operator -(Vector4D a,Vector4D b)=>a+b*-1;
        public static Vector4D operator -(Vector4D a)=>a*-1;
        public static Vector4D operator *(Vector4D a,double b)=>new Vector4D(a.X*b,a.Y*b,a.Z*b,a.W*b);
        public static Vector4D operator /(Vector4D a,double b)=>a*(1/b);
    }
}
