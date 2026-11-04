using System;
namespace Section.Geometry4D
{
    public readonly struct Transform4D
    {
        public Vector4D Position { get; }
        public Matrix4D Rotation { get; }
        public Transform4D(Vector4D position,Matrix4D rotation)
        {
            if(!position.IsFinite)throw new ArgumentException("Position must be finite.",nameof(position));
            if(rotation==null || !rotation.IsProperRotation())throw new ArgumentException("Rotation must belong to SO(4).",nameof(rotation));
            Position=position;Rotation=rotation;
        }
        public static Transform4D Identity=>new Transform4D(default,Matrix4D.Identity);
        public Vector4D TransformPoint(Vector4D local)=>Rotation*local+Position;
        public Vector4D InverseTransformPoint(Vector4D world)=>Rotation.Transposed()*(world-Position);
        public Transform4D Inverse()=>new Transform4D(-(Rotation.Transposed()*Position),Rotation.Transposed());
        // a.Compose(b) applies b first, then a, including translation.
        public Transform4D Compose(Transform4D b)=>new Transform4D(TransformPoint(b.Position),Rotation*b.Rotation);
        public Transform4D Translated(Vector4D delta)=>new Transform4D(Position+delta,Rotation);
        public Transform4D RotatedAboutCenter(RotationPlane plane,double radians,bool local=false)
        {var r=Matrix4D.Rotation(plane,radians);return new Transform4D(Position,local?Rotation*r:r*Rotation);}
    }
}
