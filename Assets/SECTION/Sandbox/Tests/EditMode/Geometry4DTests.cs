using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Section.Geometry4D;
namespace Section.Sandbox.Tests
{
    public sealed class Geometry4DTests
    {
        [TestCase(RotationPlane.XY)][TestCase(RotationPlane.XZ)][TestCase(RotationPlane.YZ)]
        [TestCase(RotationPlane.XW)][TestCase(RotationPlane.YW)][TestCase(RotationPlane.ZW)]
        public void EveryPlanePreservesMetricAndHasTheExpectedInverse(RotationPlane plane)
        {
            var random=new Random(941);var rotation=Matrix4D.Rotation(plane,.731);
            Assert.That(rotation.IsProperRotation(1e-13),Is.True);Assert.That(rotation.Determinant,Is.EqualTo(1).Within(1e-13));
            for(int i=0;i<100;i++)
            {
                var point=new Vector4D(random.NextDouble()*4-2,random.NextDouble()*4-2,random.NextDouble()*4-2,random.NextDouble()*4-2);
                var transformed=rotation*point;
                Assert.That(transformed.Length,Is.EqualTo(point.Length).Within(1e-12));
                Assert.That((rotation.InverseRotation()*transformed-point).Length,Is.LessThan(1e-12));
                Assert.That((Matrix4D.Rotation(plane,0)*point-point).Length,Is.EqualTo(0));
            }
        }
        [Test]
        public void CoordinateConventionCompositionAndTranslationAreUnambiguous()
        {
            var point=new Vector4D(1,2,3,4);var rotated=Matrix4D.Rotation(RotationPlane.XW,Math.PI/2)*point;
            Assert.That((rotated-new Vector4D(-4,2,3,1)).Length,Is.LessThan(1e-12));
            var a=new Transform4D(new Vector4D(2,-3,5,.4),Matrix4D.Rotation(RotationPlane.XW,.7));
            var b=new Transform4D(new Vector4D(-1,6,2,-.8),Matrix4D.Rotation(RotationPlane.YW,.4));
            Assert.That((a.Compose(b).TransformPoint(point)-a.TransformPoint(b.TransformPoint(point))).Length,Is.LessThan(1e-12));
            Assert.That((a.Inverse().TransformPoint(a.TransformPoint(point))-point).Length,Is.LessThan(1e-12));
            Assert.That((a.InverseTransformPoint(a.TransformPoint(point))-point).Length,Is.LessThan(1e-12));
            Assert.That((a.Compose(b).TransformPoint(point)-b.Compose(a).TransformPoint(point)).Length,Is.GreaterThan(.1));
            var local=a.RotatedAboutCenter(RotationPlane.YW,.4,true);var world=a.RotatedAboutCenter(RotationPlane.YW,.4);
            Assert.That((local.TransformPoint(point)-world.TransformPoint(point)).Length,Is.GreaterThan(.1));
            Assert.That((a.Translated(new Vector4D(0,0,0,2)).Position-a.Position-new Vector4D(0,0,0,2)).Length,Is.EqualTo(0));
        }
        [Test]
        public void LongRotationSequencesStayInSO4AndCoreHasNoUnityDependency()
        {
            var rotation=Matrix4D.Identity;var point=new Vector4D(1.2,-.4,2.1,.7);
            for(int i=0;i<10000;i++)rotation=Matrix4D.Rotation((RotationPlane)(i%6),.001*(1+i%13))*rotation;
            Assert.That(rotation.IsProperRotation(1e-10),Is.True);
            Assert.That((rotation*point).Length,Is.EqualTo(point.Length).Within(1e-10));
            Assert.That(typeof(Vector4D).Assembly.GetReferencedAssemblies().Any(a=>a.Name.StartsWith("UnityEngine")),Is.False);
            var array=rotation.ToArray();array[0]=99;Assert.That(rotation[0,0],Is.Not.EqualTo(99));
        }
        [Test]
        public void InvalidParametersAreRejectedWithoutNaNGeometry()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>Matrix4D.Rotation(RotationPlane.XW,double.NaN));
            Assert.Throws<ArgumentException>(()=>new Transform4D(new Vector4D(0,0,0,double.PositiveInfinity),Matrix4D.Identity));
            Assert.Throws<ArgumentException>(()=>new Transform4D(default,new Matrix4D(new double[16])));
            Assert.Throws<ArgumentOutOfRangeException>(()=>SliceEngine.Compute(Shape4D.Tesseract,0,Transform4D.Identity,0));
            Assert.Throws<ArgumentOutOfRangeException>(()=>SliceEngine.Compute(Shape4D.Hypersphere,1.6,Transform4D.Identity,double.NaN));
        }
        [TestCase(0,1.6)][TestCase(.8,1.385640646055102)][TestCase(-.8,1.385640646055102)]
        public void HypersphereMatchesAnalyticRadius(double w,double radius)
        {
            var slice=SliceEngine.Compute(Shape4D.Hypersphere,1.6,Transform4D.Identity,w);
            Assert.That(slice.Kind,Is.EqualTo(SliceKind.Volume));Assert.That(slice.AnalyticRadius,Is.EqualTo(radius).Within(1e-12));
            for(int i=0;i<slice.Vertices.Count;i++)
            {Assert.That(slice.Vertices[i].Length,Is.EqualTo(radius).Within(1e-12));Assert.That(slice.SmoothNormals[i].Length,Is.EqualTo(1).Within(1e-12));}
            ValidateClosedSurface(slice);
        }
        [Test]
        public void HypersphereRotationIsInvariantAndBoundaryIsAPoint()
        {
            var state=new Transform4D(new Vector4D(-4,1.9,13.2,.3),Matrix4D.Rotation(RotationPlane.XW,.71)*Matrix4D.Rotation(RotationPlane.YW,.34));
            var sphere=SliceEngine.Compute(Shape4D.Hypersphere,1.6,state,1.1);
            Assert.That(sphere.AnalyticRadius,Is.EqualTo(Math.Sqrt(1.6*1.6-.8*.8)).Within(1e-12));
            foreach(double sign in new[]{-1d,1d})
            {
                var boundary=SliceEngine.Compute(Shape4D.Hypersphere,1.6,Transform4D.Identity,sign*1.6);
                Assert.That(boundary.Kind,Is.EqualTo(SliceKind.Degenerate));Assert.That(boundary.Dimension,Is.EqualTo(0));
                Assert.That(boundary.Vertices.Count,Is.EqualTo(1));Assert.That(boundary.Triangles,Is.Empty);
                Assert.That(SliceEngine.Compute(Shape4D.Hypersphere,1.6,Transform4D.Identity,sign*2).Kind,Is.EqualTo(SliceKind.Empty));
                Assert.That(SliceEngine.Compute(Shape4D.Hypersphere,1.6,Transform4D.Identity,sign*(1.6-1e-7)).Kind,Is.EqualTo(SliceKind.Volume));
            }
        }
        [Test]
        public void TesseractReferenceSlicesMatchTheActualFourDimensionalBox()
        {
            var plain=SliceEngine.Compute(Shape4D.Tesseract,.8,Transform4D.Identity,0);
            Assert.That(plain.Vertices.Count,Is.EqualTo(8));Assert.That(plain.Faces.Count,Is.EqualTo(6));
            Assert.That(plain.Maximum.X,Is.EqualTo(.8).Within(1e-12));Assert.That(Volume(plain),Is.EqualTo(1.6*1.6*1.6).Within(1e-10));
            var state=Transform4D.Identity.RotatedAboutCenter(RotationPlane.XW,Math.PI/4);
            var rotated=SliceEngine.Compute(Shape4D.Tesseract,.8,state,0);
            Assert.That(rotated.Maximum.X,Is.EqualTo(.8*Math.Sqrt(2)).Within(1e-12));
            Assert.That(rotated.Minimum.X,Is.EqualTo(-.8*Math.Sqrt(2)).Within(1e-12));
            Assert.That(rotated.Maximum.Y,Is.EqualTo(.8).Within(1e-12));Assert.That(rotated.Maximum.Z,Is.EqualTo(.8).Within(1e-12));
            ValidateClosedSurface(plain);ValidateClosedSurface(rotated);CheckInside4D(rotated,state,0,.8);
        }
        [Test]
        public void FacetBoundaryIsClosedButTangentBoundaryHasLowerDimension()
        {
            Assert.That(SliceEngine.Compute(Shape4D.Tesseract,.8,Transform4D.Identity,.8).Kind,Is.EqualTo(SliceKind.Volume));
            Assert.That(SliceEngine.Compute(Shape4D.Tesseract,.8,Transform4D.Identity,.800001).Kind,Is.EqualTo(SliceKind.Empty));
            var rotated=Transform4D.Identity.RotatedAboutCenter(RotationPlane.XW,Math.PI/4);
            var tangent=SliceEngine.Compute(Shape4D.Tesseract,.8,rotated,.8*Math.Sqrt(2));
            Assert.That(tangent.Kind,Is.EqualTo(SliceKind.Degenerate));Assert.That(tangent.Dimension,Is.EqualTo(2));Assert.That(tangent.Vertices.Count,Is.EqualTo(4));Assert.That(tangent.Triangles,Is.Empty);
            var thin=SliceEngine.Compute(Shape4D.Tesseract,.8,rotated,.8*Math.Sqrt(2)-1e-7);Assert.That(thin.Kind,Is.EqualTo(SliceKind.Volume));ValidateClosedSurface(thin);
            var generic=Transform4D.Identity.RotatedAboutCenter(RotationPlane.XW,.6).RotatedAboutCenter(RotationPlane.YW,.4).RotatedAboutCenter(RotationPlane.ZW,.3);
            double support=0;for(int i=0;i<4;i++)support+=.8*Math.Abs(generic.Rotation[3,i]);
            var point=SliceEngine.Compute(Shape4D.Tesseract,.8,generic,support);
            Assert.That(point.Kind,Is.EqualTo(SliceKind.Degenerate));Assert.That(point.Dimension,Is.EqualTo(0));Assert.That(point.Vertices.Count,Is.EqualTo(1));
        }
        [Test]
        public void TangentEdgeHasDimensionOneAndRightAngleRotationsKeepCubeSymmetry()
        {
            var state=Transform4D.Identity.RotatedAboutCenter(RotationPlane.XW,.6).RotatedAboutCenter(RotationPlane.YW,.4);
            double support=0;for(int i=0;i<4;i++)support+=.8*Math.Abs(state.Rotation[3,i]);
            var edge=SliceEngine.Compute(Shape4D.Tesseract,.8,state,support);
            Assert.That(edge.Kind,Is.EqualTo(SliceKind.Degenerate));Assert.That(edge.Dimension,Is.EqualTo(1));Assert.That(edge.Vertices.Count,Is.EqualTo(2));
            foreach(RotationPlane plane in Enum.GetValues(typeof(RotationPlane)))
                foreach(double angle in new[]{Math.PI/2,Math.PI,Math.PI*1.5,Math.PI*2})
                {
                    var slice=SliceEngine.Compute(Shape4D.Tesseract,.8,Transform4D.Identity.RotatedAboutCenter(plane,angle),0);
                    Assert.That(Volume(slice),Is.EqualTo(4.096).Within(1e-10));ValidateClosedSurface(slice);
                }
        }
        [Test]
        public void CompoundRotationsProduceRealPolyhedraAndRespectBodyTranslation()
        {
            var state=new Transform4D(new Vector4D(-4,1.8,23.4,.2),Matrix4D.Identity)
                .RotatedAboutCenter(RotationPlane.XW,.61).RotatedAboutCenter(RotationPlane.YW,.49).RotatedAboutCenter(RotationPlane.ZW,.3);
            var slice=SliceEngine.Compute(Shape4D.Tesseract,.8,state,.35);
            Assert.That(slice.Faces.Count,Is.GreaterThan(6));Assert.That(slice.Vertices.Count,Is.GreaterThan(8));
            ValidateClosedSurface(slice);CheckInside4D(slice,state,.35,.8);
            var shifted=SliceEngine.Compute(Shape4D.Tesseract,.8,state.Translated(new Vector4D(2,-3,4,1.7)),2.05);
            Assert.That(shifted.Vertices.Count,Is.EqualTo(slice.Vertices.Count));
            for(int i=0;i<slice.Vertices.Count;i++)Assert.That((slice.Vertices[i]-shifted.Vertices[i]).Length,Is.LessThan(1e-11));
        }
        [Test]
        public void RandomSectionsStayFiniteClosedOrExplicitlyDegenerate()
        {
            var random=new Random(8123);
            for(int sample=0;sample<250;sample++)
            {
                var state=Transform4D.Identity;
                for(int p=0;p<6;p++)state=state.RotatedAboutCenter((RotationPlane)p,(random.NextDouble()*2-1)*Math.PI);
                double w=(random.NextDouble()*2-1)*1.9;
                var slice=SliceEngine.Compute(Shape4D.Tesseract,.8,state,w);
                Assert.That(slice.Vertices.All(v=>v.IsFinite),Is.True);
                if(slice.Kind==SliceKind.Volume){ValidateClosedSurface(slice);CheckInside4D(slice,state,w,.8);}
                else Assert.That(slice.Triangles,Is.Empty);
            }
        }
        static void CheckInside4D(SliceResult slice,Transform4D state,double query,double extent)
        {
            foreach(var v in slice.Vertices)
            {
                var local=state.InverseTransformPoint(new Vector4D(v.X+state.Position.X,v.Y+state.Position.Y,v.Z+state.Position.Z,query));
                for(int i=0;i<4;i++)Assert.That(Math.Abs(local[i]),Is.LessThanOrEqualTo(extent+1e-8));
            }
        }
        public static double Volume(SliceResult result)
        {
            double volume=0;
            for(int i=0;i<result.Triangles.Count;i+=3)volume+=Vector3D.Dot(result.Vertices[result.Triangles[i]],Vector3D.Cross(result.Vertices[result.Triangles[i+1]],result.Vertices[result.Triangles[i+2]]))/6;
            return volume;
        }
        static void ValidateClosedSurface(SliceResult slice)
        {
            Assert.That(slice.Dimension,Is.EqualTo(3));Assert.That(slice.Triangles.Count%3,Is.EqualTo(0));
            var edges=new Dictionary<long,int>();var used=new HashSet<int>();
            Vector3D center=default;foreach(var p in slice.Vertices)center+=p;center/=slice.Vertices.Count;
            for(int i=0;i<slice.Triangles.Count;i+=3)
            {
                int a=slice.Triangles[i],b=slice.Triangles[i+1],c=slice.Triangles[i+2];
                foreach(int index in new[]{a,b,c}){Assert.That(index,Is.InRange(0,slice.Vertices.Count-1));used.Add(index);}
                var normal=Vector3D.Cross(slice.Vertices[b]-slice.Vertices[a],slice.Vertices[c]-slice.Vertices[a]);
                Assert.That(normal.Length,Is.GreaterThan(1e-25));
                Assert.That(Vector3D.Dot(normal,(slice.Vertices[a]+slice.Vertices[b]+slice.Vertices[c])/3-center),Is.GreaterThan(0),"Outward winding");
                void Edge(int x,int y){long key=((long)Math.Min(x,y)<<32)|(uint)Math.Max(x,y);edges.TryGetValue(key,out int count);edges[key]=count+1;}
                Edge(a,b);Edge(b,c);Edge(c,a);
            }
            Assert.That(edges.Values.All(count=>count==2),Is.True,"Closed manifold edge incidence");
            Assert.That(used.Count,Is.EqualTo(slice.Vertices.Count));
            Assert.That(slice.Vertices.Count-edges.Count+slice.Triangles.Count/3,Is.EqualTo(2),"Euler characteristic");
            Assert.That(Volume(slice),Is.GreaterThan(0));
        }
    }
}
