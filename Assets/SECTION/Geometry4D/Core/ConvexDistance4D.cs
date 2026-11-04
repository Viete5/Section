using System;
using System.Collections.Generic;
namespace Section.Geometry4D
{
    // Dimension-independent closest-simplex GJK, used in 4D and in XYZ projection (W=0).
    // Its returned axis is only a candidate; continuous collision acceptance independently proves separation.
    public static class ConvexDistance4D
    {
        public readonly struct Result
        {
            public readonly Vector4D Axis;public readonly double Distance;
            public Result(Vector4D axis,double distance){Axis=axis;Distance=distance;}
        }
        public static Result Query(ISupportShape4D a,ISupportShape4D b)
        {
            Vector4D Support(Vector4D d)=>a.Support(d)-b.Support(-d);
            var direction=a.Center-b.Center;if(direction.Length<1e-12)direction=new Vector4D(1,0,0,0);
            var simplex=new List<Vector4D>{Support(-direction)};var closest=simplex[0];var last=direction/direction.Length;
            for(int iteration=0;iteration<40;iteration++)
            {
                if(closest.Length<1e-10)return new Result(last,0);
                last=closest/closest.Length;var point=Support(-closest);
                double progress=closest.LengthSquared-Vector4D.Dot(closest,point);
                if(progress<=1e-11*Math.Max(1,closest.LengthSquared))break;
                bool duplicate=false;foreach(var v in simplex)if((v-point).Length<1e-11){duplicate=true;break;}if(duplicate)break;
                simplex.Add(point);closest=Closest(simplex);
            }
            return new Result(closest.Length>1e-12?closest/closest.Length:last,closest.Length);
        }
        static Vector4D Closest(List<Vector4D> simplex)
        {
            int count=simplex.Count,bestMask=0;double best=double.PositiveInfinity;Vector4D bestPoint=default;
            for(int mask=1;mask<(1<<count);mask++)
            {
                var ids=new List<int>();for(int i=0;i<count;i++)if((mask&(1<<i))!=0)ids.Add(i);
                int n=ids.Count;var origin=simplex[ids[0]];var weights=new double[n];weights[0]=1;
                if(n>1)
                {
                    var gram=new double[n-1,n-1];var rhs=new double[n-1];
                    for(int i=1;i<n;i++)
                    {
                        var di=simplex[ids[i]]-origin;rhs[i-1]=-Vector4D.Dot(di,origin);
                        for(int j=1;j<n;j++)gram[i-1,j-1]=Vector4D.Dot(di,simplex[ids[j]]-origin);
                    }
                    if(!LinearSystem4D.Solve(gram,rhs,out var solved))continue;
                    for(int i=1;i<n;i++){weights[i]=solved[i-1];weights[0]-=weights[i];}
                }
                bool valid=true;foreach(double value in weights)if(value< -1e-10){valid=false;break;}if(!valid)continue;
                Vector4D point=default;for(int i=0;i<n;i++)point+=simplex[ids[i]]*weights[i];
                if(point.LengthSquared<best){best=point.LengthSquared;bestPoint=point;bestMask=mask;}
            }
            for(int i=count-1;i>=0;i--)if((bestMask&(1<<i))==0)simplex.RemoveAt(i);
            return bestPoint;
        }
    }
}
