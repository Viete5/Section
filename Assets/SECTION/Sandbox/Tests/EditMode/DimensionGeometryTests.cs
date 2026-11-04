using System;
using System.Linq;
using NUnit.Framework;
using Section.Geometry4D;
namespace Section.Sandbox.Tests
{
    public sealed class DimensionGeometryTests
    {
        static ContinuousBoxCollision4D.Obstacle Wall(Vector4D center,Vector4D half)=>new ContinuousBoxCollision4D.Obstacle{
            Shape=new BoxShape4D(new Transform4D(center,Matrix4D.Identity),half),ProjectXYZ=true,Id="wall"};
        [Test]
        public void FourDimensionalDistanceDistinguishesXYZOverlapFromRealWContact()
        {
            var box=new BoxShape4D(Transform4D.Identity,new Vector4D(1,1,1,.2));
            Assert.That(ConvexDistance4D.Query(box,new SphereShape4D(new Vector4D(3,0,0,0),.5)).Distance,Is.EqualTo(1.5).Within(1e-7));
            Assert.That(ConvexDistance4D.Query(box,new SphereShape4D(new Vector4D(0,0,0,3),.5)).Distance,Is.EqualTo(2.3).Within(1e-7));
            Assert.That(ConvexDistance4D.Query(box,new SphereShape4D(new Vector4D(.8,0,0,0),.5)).Distance,Is.LessThan(1e-7));
            var other=new BoxShape4D(new Transform4D(new Vector4D(3,4,0,0),Matrix4D.Identity),new Vector4D(1,1,1,.2));
            Assert.That(ConvexDistance4D.Query(box,other).Distance,Is.EqualTo(Math.Sqrt(5)).Within(1e-7));
            Assert.That(ConvexDistance4D.Query(other,box).Distance,Is.EqualTo(Math.Sqrt(5)).Within(1e-7));
        }
        [Test]
        public void ThinIntermediateWallBlocksTranslationEvenWhenBothEndpointsAreFree()
        {
            var motion=new BoxMotion4D(new Transform4D(new Vector4D(-2,0,0,2),Matrix4D.Identity),new Vector4D(.2,.2,.2,.1),new Vector4D(4,0,0,0));
            var wall=Wall(default,new Vector4D(.01,2,2,0));
            double prefix=ContinuousBoxCollision4D.FreePrefix(motion,new[]{wall},out var obstacle,out _);
            Assert.That(prefix,Is.EqualTo(.4475).Within(2e-5));Assert.That(obstacle,Is.SameAs(wall));
            Assert.That(ConvexDistance4D.Query(new ProjectedShape4D(new BoxShape4D(motion.At(1),motion.Half)),wall.Shape).Distance,Is.GreaterThan(1));
        }
        [Test]
        public void RotationChecksTheOccupiedMiddleEvenWithIdenticalFreeEndShapes()
        {
            var motion=new BoxMotion4D(Transform4D.Identity,new Vector4D(2,.2,.2,.1),default,RotationPlane.XY,Math.PI);
            var obstacle=Wall(new Vector4D(0,1.3,0,0),new Vector4D(.15,.15,.15,0));
            double prefix=ContinuousBoxCollision4D.FreePrefix(motion,new[]{obstacle},out _,out _);
            Assert.That(prefix,Is.InRange(.01,.49));
            Assert.That(ConvexDistance4D.Query(new ProjectedShape4D(new BoxShape4D(motion.At(0),motion.Half)),obstacle.Shape).Distance,Is.GreaterThan(.9));
            Assert.That(ConvexDistance4D.Query(new ProjectedShape4D(new BoxShape4D(motion.At(1),motion.Half)),obstacle.Shape).Distance,Is.GreaterThan(.9));
        }
        [Test]
        public void TangentSupportAllowsSlidingAndXWButBlocksAnExpandingYWRotation()
        {
            var pose=new Transform4D(new Vector4D(0,1,0,0),Matrix4D.Identity);var half=new Vector4D(1,1,1,.1);
            var floor=Wall(new Vector4D(0,-.5,0,0),new Vector4D(20,.5,20,0));
            Assert.That(ContinuousBoxCollision4D.FreePrefix(new BoxMotion4D(pose,half,new Vector4D(4,0,0,0)),new[]{floor},out _,out _),Is.EqualTo(1));
            Assert.That(ContinuousBoxCollision4D.FreePrefix(new BoxMotion4D(pose,half,default,RotationPlane.XW,Math.PI*.5),new[]{floor},out _,out _),Is.EqualTo(1));
            Assert.That(ContinuousBoxCollision4D.FreePrefix(new BoxMotion4D(pose,half,default,RotationPlane.YW,.3),new[]{floor},out _,out _),Is.LessThan(.01));
        }
        [Test]
        public void IntervalSupportContainsEveryIntermediatePointForAllSixPlanesAndBothFrames()
        {
            var random=new Random(501);var pose=Transform4D.Identity.RotatedAboutCenter(RotationPlane.YW,.43).RotatedAboutCenter(RotationPlane.XZ,.61);
            foreach(RotationPlane plane in Enum.GetValues(typeof(RotationPlane)))foreach(bool local in new[]{false,true})
                for(int trial=0;trial<8;trial++)
                {
                    var n=new Vector4D(random.NextDouble()-.5,random.NextDouble()-.5,random.NextDouble()-.5,random.NextDouble()-.5);n/=n.Length;
                    var motion=new BoxMotion4D(pose,new Vector4D(.8,.25,1.2,.15),new Vector4D(.2,-.1,.3,.7),plane,(random.NextDouble()-.5)*10,local);
                    motion.ProjectionInterval(n,.1,.9,false,out double lo,out double hi);
                    for(int step=0;step<=100;step++)
                    {
                        var shape=new BoxShape4D(motion.At(.1+.8*step/100),motion.Half);
                        Assert.That(Vector4D.Dot(shape.Support(n),n),Is.LessThanOrEqualTo(hi+1e-9));
                        Assert.That(Vector4D.Dot(shape.Support(-n),n),Is.GreaterThanOrEqualTo(lo-1e-9));
                    }
                }
        }
        [Test]
        public void GeneralRotationGoalsDecomposeIntoReversibleGivensSteps()
        {
            var start=Transform4D.Identity.RotatedAboutCenter(RotationPlane.XW,.7).RotatedAboutCenter(RotationPlane.YZ,-.4);
            var goal=start.RotatedAboutCenter(RotationPlane.ZW,1.9).RotatedAboutCenter(RotationPlane.XY,-2.2).RotatedAboutCenter(RotationPlane.YW,.8);
            var result=start;
            foreach(var step in RotationDecomposition4D.Between(start.Rotation,goal.Rotation))result=result.RotatedAboutCenter(step.Plane,step.Angle);
            for(int r=0;r<4;r++)for(int c=0;c<4;c++)Assert.That(result.Rotation[r,c],Is.EqualTo(goal.Rotation[r,c]).Within(1e-10));
        }
        [Test]
        public void FullClippedBodiesHaveRealFourDimensionalVertices()
        {
            var planes=ConvexPrism4D.Planes(new Vector4D(.9,.125,.575,.7),default,new Transform4D(new Vector4D(4.25,-.125,17.375,1.1),Matrix4D.Identity),
                new[]{new Halfspace4D(new Vector4D(0,0,-1,-2.5),1.575)});
            var vertices=LinearSystem4D.Vertices(planes);Assert.That(vertices.Length,Is.GreaterThanOrEqualTo(16));
            foreach(var point in vertices)foreach(var plane in planes)Assert.That(Vector4D.Dot(plane.Normal,point),Is.LessThanOrEqualTo(plane.Offset+1e-8));
        }
        [Test]
        public void C2CannotFitInAnyThreeDimensionalOrientationButItsTrueXWProjectionFits()
        {
            Assert.That(1.6,Is.GreaterThan(Math.Sqrt(1+.7*.7)),"Inscribed XZ disk diameter exceeds the window diagonal.");
            var half=new Vector4D(.8,.25,.8,.15);var permutation=new Transform4D(default,Matrix4D.Rotation(RotationPlane.ZW,Math.PI*.5));var random=new Random(502);
            for(int i=0;i<400;i++)
            {
                var pose=new Transform4D(new Vector4D(4.5,1.1,25,0),Matrix4D.Identity).RotatedAboutCenter(RotationPlane.XY,random.NextDouble()*6.3)
                    .RotatedAboutCenter(RotationPlane.XZ,random.NextDouble()*6.3).RotatedAboutCenter(RotationPlane.YZ,random.NextDouble()*6.3);
                var slice=ConvexPrism4D.Slice(half,default,permutation.Compose(pose),25);
                Assert.That(slice.Maximum.X-slice.Minimum.X>1 || slice.Maximum.Y-slice.Minimum.Y>.7,Is.True);
            }
            var correct=new Transform4D(new Vector4D(4.5,1.1,23.3,4),Matrix4D.Rotation(RotationPlane.XW,Math.PI*.5));
            var walls=new[]{Wall(new Vector4D(3,1.5,25,0),new Vector4D(1,1.5,.15,0)),Wall(new Vector4D(5.75,1.5,25,0),new Vector4D(.75,1.5,.15,0)),
                Wall(new Vector4D(4.5,.375,25,0),new Vector4D(.5,.375,.15,0)),Wall(new Vector4D(4.5,2.225,25,0),new Vector4D(.5,.775,.15,0))};
            Assert.That(ContinuousBoxCollision4D.FreePrefix(new BoxMotion4D(correct,half,new Vector4D(0,0,3.7,0)),walls,out _,out _),Is.EqualTo(1));
            foreach(double degrees in new[]{65d,75d,90d,105d,115d})
            {
                var pose=new Transform4D(correct.Position,Matrix4D.Rotation(RotationPlane.XW,degrees*Math.PI/180));
                Assert.That(ContinuousBoxCollision4D.FreePrefix(new BoxMotion4D(pose,half,new Vector4D(0,0,3.7,0)),walls,out _,out _),Is.EqualTo(1),"Physical angular tolerance: "+degrees);
            }
            var wide=new Transform4D(correct.Position,Matrix4D.Identity);
            Assert.That(ConvexPrism4D.Slice(half,default,wide,0).Kind,Is.EqualTo(SliceKind.Empty));
            Assert.That(ContinuousBoxCollision4D.FreePrefix(new BoxMotion4D(wide,half,new Vector4D(0,0,3.7,0)),walls,out _,out _),Is.LessThan(1),"Invisibility must not bypass ordinary matter.");
        }
        [Test]
        public void C3NeedsARealWCorrectionAndItsActualSectionReachesBothContacts()
        {
            var half=new Vector4D(.15,.15,.15,1);var pose=new Transform4D(new Vector4D(-4,1.2,27,.7),Matrix4D.Rotation(RotationPlane.XW,Math.PI*.5));
            Assert.That(ConvexPrism4D.Slice(half,default,pose,0).Kind,Is.EqualTo(SliceKind.Empty));
            pose=pose.Translated(new Vector4D(0,0,0,-.7));var slice=ConvexPrism4D.Slice(half,default,pose,0);
            Assert.That(slice.Kind,Is.EqualTo(SliceKind.Volume));Assert.That(slice.Minimum.X+pose.Position.X,Is.LessThan(-4.8));Assert.That(slice.Maximum.X+pose.Position.X,Is.GreaterThan(-3.2));
        }
    }
}
