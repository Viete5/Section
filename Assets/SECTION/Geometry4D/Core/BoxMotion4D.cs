using System;
using System.Collections.Generic;
namespace Section.Geometry4D
{
    public readonly struct BoxMotion4D
    {
        public readonly Transform4D Start;
        public readonly Vector4D Half,Translation,LocalCenter;
        public readonly RotationPlane Plane;
        public readonly double Angle;
        public readonly bool Local;
        public BoxMotion4D(Transform4D start,Vector4D half,Vector4D translation,RotationPlane plane=RotationPlane.XY,double angle=0,bool local=false,Vector4D localCenter=default)
        {LocalCenter=localCenter;Start=start;Half=half;Translation=translation;Plane=plane;Angle=angle;Local=local;Numeric.RequireFinite(angle,nameof(angle));if(!translation.IsFinite)throw new ArgumentException("Invalid translation.");}
        public Transform4D At(double t)=>Start.RotatedAboutCenter(Plane,Angle*t,Local).Translated(Translation*t);
        public Transform4D ShapeAt(double t){var pose=At(t);return new Transform4D(pose.Position+pose.Rotation*LocalCenter,pose.Rotation);}
        public double PointTravelBound=>Translation.Length+(Half.Length+LocalCenter.Length)*Math.Abs(Angle);
        public static (int,int) Axes(RotationPlane plane)=>plane switch{RotationPlane.XY=>(0,1),RotationPlane.XZ=>(0,2),RotationPlane.YZ=>(1,2),RotationPlane.XW=>(0,3),RotationPlane.YW=>(1,3),RotationPlane.ZW=>(2,3),_=>throw new ArgumentOutOfRangeException(nameof(plane))};
        public void ProjectionInterval(Vector4D axis,double t0,double t1,bool xyz,out double minimum,out double maximum)
        {
            if(xyz)axis=new Vector4D(axis.X,axis.Y,axis.Z,0);
            var (p,q)=Axes(Plane);double radius=0;
            for(int i=0;i<4;i++)
            {
                var column=new Vector4D(Start.Rotation[0,i],Start.Rotation[1,i],Start.Rotation[2,i],Start.Rotation[3,i]);
                double a,b,c;
                if(Local)
                {
                    double np=0,nq=0;for(int r=0;r<4;r++){np+=axis[r]*Start.Rotation[r,p];nq+=axis[r]*Start.Rotation[r,q];}
                    if(i==p){a=np;b=nq;c=0;}else if(i==q){a=nq;b=-np;c=0;}else{a=b=0;c=Vector4D.Dot(axis,column);}
                }
                else{a=axis[p]*column[p]+axis[q]*column[q];b=-axis[p]*column[q]+axis[q]*column[p];c=Vector4D.Dot(axis,column)-a;}
                radius+=Half[i]*MaximumAbsoluteTrig(a,b,c,Angle*t0,Angle*t1);
            }
            double first=Vector4D.Dot(axis,Start.Position+Translation*t0),last=Vector4D.Dot(axis,Start.Position+Translation*t1);
            OffsetRange(axis,p,q,t0,t1,out double offsetMin,out double offsetMax);
            minimum=Math.Min(first,last)-radius+offsetMin;maximum=Math.Max(first,last)+radius+offsetMax;
        }
        void OffsetRange(Vector4D axis,int p,int q,double t0,double t1,out double min,out double max)
        {
            var center=Start.Rotation*LocalCenter;double a,b,c;
            if(Local){var n=Start.Rotation.Transposed()*axis;a=n[p]*LocalCenter[p]+n[q]*LocalCenter[q];b=-n[p]*LocalCenter[q]+n[q]*LocalCenter[p];c=Vector4D.Dot(n,LocalCenter)-a;}
            else{a=axis[p]*center[p]+axis[q]*center[q];b=-axis[p]*center[q]+axis[q]*center[p];c=Vector4D.Dot(axis,center)-a;}
            double lo=Angle*t0,hi=Angle*t1;if(lo>hi)(lo,hi)=(hi,lo);
            double F(double t)=>a*Math.Cos(t)+b*Math.Sin(t)+c;min=Math.Min(F(lo),F(hi));max=Math.Max(F(lo),F(hi));
            if(hi-lo>=2*Math.PI){double amp=Math.Sqrt(a*a+b*b);min=c-amp;max=c+amp;return;}
            double phase=Math.Atan2(b,a);for(int k=(int)Math.Ceiling((lo-phase)/Math.PI);k<=Math.Floor((hi-phase)/Math.PI);k++){double v=F(phase+k*Math.PI);min=Math.Min(min,v);max=Math.Max(max,v);}
        }
        public static double MaximumAbsoluteTrig(double a,double b,double c,double lo,double hi)
        {
            if(lo>hi)(lo,hi)=(hi,lo);double amplitude=Math.Sqrt(a*a+b*b);
            if(hi-lo>=Math.PI*2)return Math.Abs(c)+amplitude;
            double F(double t)=>Math.Abs(a*Math.Cos(t)+b*Math.Sin(t)+c);
            double result=Math.Max(F(lo),F(hi)),phase=Math.Atan2(b,a);
            for(int k=(int)Math.Ceiling((lo-phase)/Math.PI);k<=Math.Floor((hi-phase)/Math.PI);k++)result=Math.Max(result,F(phase+k*Math.PI));
            return result;
        }
    }
    public static class ContinuousBoxCollision4D
    {
        public sealed class Obstacle
        {
            public ISupportShape4D Shape;public bool ProjectXYZ;public string Id;public object Owner;public double ContactTolerance=1e-5;
        }
        public static double FreePrefix(BoxMotion4D motion,IReadOnlyList<Obstacle> obstacles,out Obstacle blocker,out int intervals)
        {
            Obstacle found=null;int visited=0;
            double Traverse(double lo,double hi,int depth)
            {
                visited++;
                bool safe=true;
                foreach(var obstacle in obstacles)
                    if(!Separated(motion,obstacle,lo,hi)){safe=false;found=obstacle;break;}
                if(safe)return hi;
                if(depth>=18 || visited>=2048 || (hi-lo)*motion.PointTravelBound<1e-6)return lo;
                double mid=(lo+hi)*.5;double left=Traverse(lo,mid,depth+1);
                return left<mid-1e-12?left:Traverse(mid,hi,depth+1);
            }
            double result=Traverse(0,1,0);blocker=result>=1?null:found;intervals=visited;return result;
        }
        static bool Separated(BoxMotion4D motion,Obstacle obstacle,double lo,double hi)
        {
            bool Proves(Vector4D n)
            {
                if(obstacle.ProjectXYZ)n=new Vector4D(n.X,n.Y,n.Z,0);
                if(n.Length<1e-10)return false;n/=n.Length;
                motion.ProjectionInterval(n,lo,hi,obstacle.ProjectXYZ,out double min,out double max);
                double otherMax=Vector4D.Dot(obstacle.Shape.Support(n),n),otherMin=Vector4D.Dot(obstacle.Shape.Support(-n),n);
                return min>=otherMax-obstacle.ContactTolerance || max<=otherMin+obstacle.ContactTolerance;
            }
            // Fixed contact normals allow exact tangential travel, including resting on the ordinary floor.
            for(int i=0;i<4;i++)
            {if(Proves(new Vector4D(i==0?1:0,i==1?1:0,i==2?1:0,i==3?1:0)))return true;}
            var middle=motion.ShapeAt((lo+hi)*.5);ISupportShape4D body=new BoxShape4D(middle,motion.Half);
            if(obstacle.ProjectXYZ)body=new ProjectedShape4D(body);
            var distance=ConvexDistance4D.Query(body,obstacle.Shape);
            return Proves(distance.Axis);
        }
    }
    public static class RotationDecomposition4D
    {
        public readonly struct Step
        {public readonly RotationPlane Plane;public readonly double Angle;public Step(RotationPlane p,double a){Plane=p;Angle=a;}}
        public static Step[] Between(Matrix4D start,Matrix4D target)
        {
            var r=(target*start.Transposed()).ToArray();var eliminate=new List<Step>();
            RotationPlane Plane(int a,int b)=>(a,b) switch{(0,1)=>RotationPlane.XY,(0,2)=>RotationPlane.XZ,(0,3)=>RotationPlane.XW,(1,2)=>RotationPlane.YZ,(1,3)=>RotationPlane.YW,(2,3)=>RotationPlane.ZW,_=>throw new ArgumentException()};
            for(int c=0;c<3;c++)for(int row=3;row>c;row--)
            {
                double angle=Math.Atan2(-r[row*4+c],r[c*4+c]);if(Math.Abs(angle)<1e-14)continue;
                var p=Plane(c,row);var next=Matrix4D.Rotation(p,angle)*new Matrix4D(r);r=next.ToArray();eliminate.Add(new Step(p,angle));
            }
            eliminate.Reverse();return eliminate.ConvertAll(s=>new Step(s.Plane,-s.Angle)).ToArray();
        }
    }
}
