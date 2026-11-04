using System;
namespace Section.Geometry4D
{
    public enum RotationPlane { XY, XZ, YZ, XW, YW, ZW }

    // Row-major matrix acting on column vectors. The backing data cannot be mutated by callers.
    public sealed class Matrix4D
    {
        readonly double[] values;
        public double this[int row,int column] => values[row*4+column];
        public Matrix4D(double[] entries)
        {
            if(entries==null || entries.Length!=16)throw new ArgumentException("A 4x4 matrix requires 16 entries.",nameof(entries));
            values=(double[])entries.Clone();
            foreach(double value in values)Numeric.RequireFinite(value,nameof(entries));
        }
        public double[] ToArray() => (double[])values.Clone();
        public static Matrix4D Identity => new Matrix4D(new double[]{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1});
        public static Matrix4D Rotation(RotationPlane plane,double radians)
        {
            Numeric.RequireFinite(radians,nameof(radians));
            int a,b;
            switch(plane)
            {
                case RotationPlane.XY:a=0;b=1;break;
                case RotationPlane.XZ:a=0;b=2;break;
                case RotationPlane.YZ:a=1;b=2;break;
                case RotationPlane.XW:a=0;b=3;break;
                case RotationPlane.YW:a=1;b=3;break;
                case RotationPlane.ZW:a=2;b=3;break;
                default:throw new ArgumentOutOfRangeException(nameof(plane));
            }
            var data=Identity.ToArray();double c=Math.Cos(radians),s=Math.Sin(radians);
            data[a*4+a]=c;data[b*4+b]=c;data[a*4+b]=-s;data[b*4+a]=s;
            return new Matrix4D(data);
        }
        public static Vector4D operator *(Matrix4D m,Vector4D v)=>new Vector4D(
            m[0,0]*v.X+m[0,1]*v.Y+m[0,2]*v.Z+m[0,3]*v.W,
            m[1,0]*v.X+m[1,1]*v.Y+m[1,2]*v.Z+m[1,3]*v.W,
            m[2,0]*v.X+m[2,1]*v.Y+m[2,2]*v.Z+m[2,3]*v.W,
            m[3,0]*v.X+m[3,1]*v.Y+m[3,2]*v.Z+m[3,3]*v.W);
        public static Matrix4D operator *(Matrix4D a,Matrix4D b)
        {
            var data=new double[16];
            for(int i=0;i<4;i++)for(int j=0;j<4;j++)for(int k=0;k<4;k++)data[i*4+j]+=a[i,k]*b[k,j];
            return new Matrix4D(data);
        }
        public Matrix4D Transposed()
        {
            var data=new double[16];for(int i=0;i<4;i++)for(int j=0;j<4;j++)data[i*4+j]=this[j,i];return new Matrix4D(data);
        }
        public double Determinant
        {
            get
            {
                double result=0;
                for(int column=0;column<4;column++)
                {
                    var minor=new double[9];int index=0;
                    for(int r=1;r<4;r++)for(int c=0;c<4;c++)if(c!=column)minor[index++]=this[r,c];
                    double det=minor[0]*(minor[4]*minor[8]-minor[5]*minor[7])-minor[1]*(minor[3]*minor[8]-minor[5]*minor[6])+minor[2]*(minor[3]*minor[7]-minor[4]*minor[6]);
                    result+=(column%2==0?1:-1)*this[0,column]*det;
                }
                return result;
            }
        }
        public bool IsProperRotation(double tolerance=1e-9)
        {
            for(int i=0;i<4;i++)for(int j=0;j<4;j++)
            {
                double dot=0;for(int k=0;k<4;k++)dot+=this[k,i]*this[k,j];
                if(Math.Abs(dot-(i==j?1:0))>tolerance)return false;
            }
            return Math.Abs(Determinant-1)<=tolerance*4;
        }
        public Matrix4D InverseRotation()
        {if(!IsProperRotation())throw new InvalidOperationException("Matrix is not an SO(4) rotation.");return Transposed();}
    }
}
