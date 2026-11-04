using System;
using System.Linq;
using NUnit.Framework;
using Section.Geometry4D;
using Section.Sandbox.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Section.Sandbox.Tests
{
    public sealed class WorldSliceMathTests
    {
        [Test]
        public void M4FixturesKeepApprovedBParametersAndStableStationIdentities()
        {
            var scene=EditorSceneManager.OpenScene(SandboxSceneBuilder.ScenePath);
            var world=UnityEngine.Object.FindFirstObjectByType<WorldSliceState>();
            Assert.That(world.Sources.Where(s=>s.Id!="C2" && s.Id!="C3").Select(s=>s.Id),Is.EquivalentTo(new[]{"B1","C1","B2.P1","B2.P2","B3.U","B3.V"}));
            Assert.That(UnityEngine.Object.FindObjectsByType<TestStation>(FindObjectsSortMode.None).Where(s=>!s.Id.StartsWith("C")).Select(s=>s.Id),
                Is.EquivalentTo(new[]{"A1","A2","A3","B1","B2","B3"}));
            var b1=world.Sources.Single(s=>s.Id=="B1");
            Assert.That(b1.State.Position.X,Is.EqualTo(-4));Assert.That(b1.State.Position.Y,Is.EqualTo(1.9));Assert.That(b1.State.Position.Z,Is.EqualTo(13.2));Assert.That(b1.Extent,Is.EqualTo(1.6));
            var pedestal=SandboxSceneBuilder.Find(scene,"b1.pedestal").GetComponent<BoxCollider>();
            Assert.That(pedestal.bounds.center,Is.EqualTo(new Vector3(-4,.2f,13.2f)));Assert.That(pedestal.bounds.size,Is.EqualTo(new Vector3(2,.4f,2)));
            foreach(var source in world.Sources.Where(s=>s.Id.StartsWith("B3")))
            {Assert.That(source.Extent,Is.EqualTo(.7));Assert.That(Math.Abs(source.State.Position.W),Is.EqualTo(.45));Assert.That(source.State.Position.Y,Is.EqualTo(1.2));Assert.That(source.State.Position.Z,Is.EqualTo(17));}
            Assert.That(world.Settings.minimumW,Is.EqualTo(-2.5));Assert.That(world.Settings.maximumW,Is.EqualTo(2.5));Assert.That(world.Settings.unitsPerSecond,Is.EqualTo(1));
        }
        [Test]
        public void ClippedFourDimensionalBoxProducesTheLandingFromPlanesIncludingAfterRotation()
        {
            var pose=new Transform4D(new Vector4D(4.25,-.125,17.375,1.1),Matrix4D.Identity);
            var half=new Vector4D(.9,.125,.575,.7);var clips=new[]{new Halfspace4D(new Vector4D(0,0,-1,-2.5),1.575)};
            foreach(double w in new[]{.4,.5,.55,.7,1.0,1.8})
            {
                var slice=ConvexPrism4D.Slice(half,default,pose,w,clips);
                Assert.That(slice.Kind,Is.EqualTo(SliceKind.Volume));
                Assert.That(slice.Minimum.Z+pose.Position.Z,Is.EqualTo(Math.Max(16.8,18.55-2.5*w)).Within(1e-10));
                Assert.That(slice.Maximum.Z+pose.Position.Z,Is.EqualTo(17.95).Within(1e-10));
            }
            var rotated=pose.RotatedAboutCenter(RotationPlane.YW,.37).RotatedAboutCenter(RotationPlane.XW,.28);
            var result=ConvexPrism4D.Slice(half,default,rotated,1.1,clips);
            Assert.That(result.Kind,Is.EqualTo(SliceKind.Volume));
            foreach(var vertex in result.Vertices)
            {
                var point=new Vector4D(vertex.X+pose.Position.X,vertex.Y+pose.Position.Y,vertex.Z+pose.Position.Z,1.1);
                foreach(var plane in ConvexPrism4D.Planes(half,default,rotated,clips))
                    Assert.That(Vector4D.Dot(plane.Normal,point),Is.LessThanOrEqualTo(plane.Offset+1e-9));
            }
        }
        [Test]
        public void SphereSweepFindsBlockedInteriorBetweenTwoEmptyEndpointsAndIsSymmetric()
        {
            var capsule=new Capsule3D(new Vector3D(.8,0,0),new Vector3D(.8,1,0),.3);
            var center=new Vector4D(0,.5,0,0);
            Assert.That(SpatialSweep.IntersectsSphereInterval(capsule,center,1,-2,-2),Is.False);
            Assert.That(SpatialSweep.IntersectsSphereInterval(capsule,center,1,2,2),Is.False);
            Assert.That(SpatialSweep.IntersectsSphereInterval(capsule,center,1,-2,2),Is.True);
            Assert.That(SpatialSweep.IntersectsSphereInterval(capsule,center,1,2,-2),Is.True);
            Assert.That(SpatialSweep.IntersectsSphereInterval(new Capsule3D(new Vector3D(1.5,0,0),new Vector3D(1.5,1,0),.3),center,1,-2,2),Is.False);
        }
        [Test]
        public void PolytopeProjectionCannotMissAnArbitrarilyThinIntermediateSlab()
        {
            var pose=new Transform4D(new Vector4D(0,0,0,.1234567),Matrix4D.Rotation(RotationPlane.XW,.3));
            var half=new Vector4D(.4,.4,.4,1e-7);
            var planes=ConvexPrism4D.Planes(half,default,pose);
            var capsule=new Capsule3D(new Vector3D(0,-.2,0),new Vector3D(0,.2,0),.1);
            Assert.That(SpatialSweep.IntersectsPolytopeInterval(capsule,planes,-2,-2),Is.False);
            Assert.That(SpatialSweep.IntersectsPolytopeInterval(capsule,planes,2,2),Is.False);
            Assert.That(SpatialSweep.IntersectsPolytopeInterval(capsule,planes,-2,2),Is.True);
            Assert.That(SpatialSweep.IntersectsPolytopeInterval(capsule,planes,2,-2),Is.True);
            Assert.That(SpatialSweep.IntersectsPolytopeInterval(new Capsule3D(new Vector3D(1,0,0),new Vector3D(1,1,0),.1),planes,-2,2),Is.False);
        }
        [Test]
        public void ProjectedCapsuleDistanceDoesNotTreatABoxCornerAsAnExpandedPlaneBox()
        {
            var planes=ConvexPrism4D.Planes(new Vector4D(1,1,1,1),default,Transform4D.Identity);
            var capsule=new Capsule3D(new Vector3D(1.2,1.2,0),new Vector3D(1.2,1.2,.1),.25);
            Assert.That(SpatialSweep.IntersectsPolytopeInterval(capsule,planes,-.5,.5),Is.False);
            Assert.That(SpatialSweep.IntersectsPolytopeInterval(new Capsule3D(capsule.A,capsule.B,.3),planes,-.5,.5),Is.True);
        }
        [Test]
        public void ShearedPrismSlicesFollowTheirCanonicalPlanesThroughTransforms()
        {
            var half=new Vector4D(.9,.125,.2,.325);var shear=new Vector3D(0,0,-1.25);
            var pose=new Transform4D(new Vector4D(4.25,-.125,13.65625,.275),Matrix4D.Identity);
            foreach(double w in new[]{-.05,0,.4,.6})
            {
                var slice=ConvexPrism4D.Slice(half,shear,pose,w);
                Assert.That(slice.Kind,Is.EqualTo(SliceKind.Volume));Assert.That(slice.Vertices.Count,Is.EqualTo(8));
                Assert.That((slice.Maximum.Z+slice.Minimum.Z)*.5+pose.Position.Z,Is.EqualTo(14-1.25*w).Within(1e-10));
                foreach(var v in slice.Vertices)
                    foreach(var plane in ConvexPrism4D.Planes(half,shear,pose))
                        Assert.That(Vector4D.Dot(plane.Normal,new Vector4D(v.X+pose.Position.X,v.Y+pose.Position.Y,v.Z+pose.Position.Z,w)),Is.LessThanOrEqualTo(plane.Offset+1e-9));
            }
            Assert.That(ConvexPrism4D.Slice(half,shear,pose,.60001).Kind,Is.EqualTo(SliceKind.Empty));
            var rotated=pose.RotatedAboutCenter(RotationPlane.YW,.37).Translated(new Vector4D(.2,.1,-.3,.4));
            var result=ConvexPrism4D.Slice(half,shear,rotated,.675);
            Assert.That(result.Kind,Is.EqualTo(SliceKind.Volume));Assert.That(result.Faces.Count,Is.GreaterThanOrEqualTo(6));
        }
        [Test]
        public void ActualB2GeometryHasNoConstantWBallisticCrossingAndHasAnInFlightShiftRoute()
        {
            EditorSceneManager.OpenScene(SandboxSceneBuilder.ScenePath);
            var world=UnityEngine.Object.FindFirstObjectByType<WorldSliceState>();
            var p1=world.Sources.Single(s=>s.Id=="B2.P1");var p2=world.Sources.Single(s=>s.Id=="B2.P2");
            double maximumRange=4*2*Math.Sqrt(2*.6/9.81),contactReach=.35+.02;
            for(int i=0;i<=220;i++)
            {
                double w=-.05+i*.005;
                var a=p1.ComputeSliceAt(w,p1.State);var b=p2.ComputeSliceAt(w,p2.State);
                if(a.Kind==SliceKind.Volume)
                    Assert.That(18-contactReach-(p1.State.Position.Z+a.Maximum.Z+contactReach),Is.GreaterThan(maximumRange));
                if(b.Kind==SliceKind.Volume)
                    Assert.That(p2.State.Position.Z+b.Minimum.Z-contactReach-(13+contactReach),Is.GreaterThan(maximumRange));
                if(a.Kind==SliceKind.Volume && b.Kind==SliceKind.Volume)
                    Assert.That(p2.State.Position.Z+b.Minimum.Z-contactReach-(p1.State.Position.Z+a.Maximum.Z+contactReach),Is.GreaterThan(maximumRange));
            }
            double flight=2*Math.Sqrt(2*.6/9.81);
            var landing=p2.ComputeSliceAt(flight,p2.State);double feetZ=14.42+4*flight;
            Assert.That(landing.Kind,Is.EqualTo(SliceKind.Volume));
            Assert.That(feetZ,Is.InRange(p2.State.Position.Z+landing.Minimum.Z-.3,p2.State.Position.Z+landing.Maximum.Z+.3));
        }
        [Test]
        public void B3HasTheSpecifiedNonzeroOverlapFromActualSphereParameters()
        {
            double delta=Math.Sqrt(.7*.7-.4*.4),low=.45-delta,high=-.45+delta;
            Assert.That(low,Is.EqualTo(-.1244562646538).Within(1e-12));Assert.That(high-low,Is.GreaterThan(.248));
            foreach(double w in new[]{low+1e-6,0,high-1e-6})
                foreach(double center in new[]{-.45,.45})
                    Assert.That(HypersphereSlicer.Slice(.7,new Transform4D(new Vector4D(0,0,0,center),Matrix4D.Identity),w,1,SliceTolerance.Default).AnalyticRadius,Is.GreaterThanOrEqualTo(.4));
        }
    }
}
