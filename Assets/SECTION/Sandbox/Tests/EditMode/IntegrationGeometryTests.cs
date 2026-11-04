using System;
using System.Linq;
using NUnit.Framework;
using Section.Geometry4D;
using Section.Sandbox.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Section.Sandbox.Tests
{
    public sealed class IntegrationGeometryTests
    {
        [Test] public void D1VisibleLintelClosesEveryCapsulePathAtTheGatePlane()
        {
            var scene=EditorSceneManager.OpenScene(SandboxSceneBuilder.ScenePath,OpenSceneMode.Single);
            Assert.That(SandboxLayoutValidator.Validate(scene),Is.Empty);
            var lintel=SandboxSceneBuilder.Find(scene,"d1.lintel").GetComponent<BoxCollider>();Assert.That(lintel.bounds.min.y,Is.EqualTo(3.2f).Within(1e-5));
            // A capsule crossing the plane must have |X|<=.85, feet Y in [0,1.4].
            // Its axial segment is at worst .15 from sphere centre Y=1.6, hence sqrt(.85²+.15²)<R+.35.
            Assert.That(Math.Sqrt(.85*.85+.15*.15),Is.LessThan(1.55+.35));
            for(double x=-.85;x<=.851;x+=.17)for(double y=0;y<=1.401;y+=.14)
            {
                var capsule=new Capsule3D(new Vector3D(x,y+.35,33),new Vector3D(x,y+1.45,33),.33);
                Assert.That(SpatialSweep.IntersectsSphereInterval(capsule,new Vector4D(0,1.6,33,0),1.55,0,0),Is.True);
                Assert.That(SpatialSweep.IntersectsSphereInterval(capsule,new Vector4D(0,1.6,33,0),1.55,2,2),Is.False);
            }
        }
        [Test] public void MovingWallChecksAStationaryCapsuleAndHonoursItsBoundedWExtent()
        {
            Assert.That(new[]{0d,1,2,3,4,5}.Select(MovingWallsCondition.Opening),Is.EqualTo(new[]{.975,.975,.375,.375,.975,.975}).Within(1e-10));
            var motion=new BoxMotion4D(new Transform4D(new Vector4D(-.975,1.5,35.65,0),Matrix4D.Identity),new Vector4D(.175,1.5,.85,2.5),new Vector4D(.6,0,0,0));
            ContinuousBoxCollision4D.Obstacle Capsule(double w)=>new ContinuousBoxCollision4D.Obstacle{Shape=new SliceCapsuleShape4D(new Capsule3D(new Vector3D(0,.35,35.65),new Vector3D(0,1.45,35.65),.33),w)};
            Assert.That(ContinuousBoxCollision4D.FreePrefix(motion,new[]{Capsule(0)},out _,out _),Is.InRange(.7,.9));
            Assert.That(ContinuousBoxCollision4D.FreePrefix(motion,new[]{Capsule(3)},out _,out _),Is.EqualTo(1));
        }
        [Test] public void CompoundUnionHasTrueNarrowAndWideSlicesAndOffsetSweepBounds()
        {
            var pose=new Transform4D(new Vector4D(4,1.25,38,0),Matrix4D.Identity);
            var narrow=CompoundBox4D.Slice(pose,0);var wide=CompoundBox4D.Slice(pose,1);
            Assert.That(narrow.Maximum.X-narrow.Minimum.X,Is.EqualTo(.4).Within(1e-9));Assert.That(wide.Maximum.X-wide.Minimum.X,Is.EqualTo(2).Within(1e-9));
            Assert.That(CompoundBox4D.Slice(pose,1.25001).Kind,Is.EqualTo(SliceKind.Empty));
            double Volume(SliceResult slice){double sum=0;for(int i=0;i<slice.Triangles.Count;i+=3){var a=slice.Vertices[slice.Triangles[i]];var b=slice.Vertices[slice.Triangles[i+1]];var c=slice.Vertices[slice.Triangles[i+2]];sum+=Vector3D.Dot(a,Vector3D.Cross(b,c))/6;}return Math.Abs(sum);}
            Assert.That(Volume(narrow),Is.EqualTo(.064).Within(1e-8));Assert.That(Volume(wide),Is.EqualTo(.32).Within(1e-8));
            foreach(RotationPlane plane in Enum.GetValues(typeof(RotationPlane)))
            {
                var motion=new BoxMotion4D(pose,CompoundBox4D.Half(1),new Vector4D(.2,.3,.4,.5),plane,1.3,false,CompoundBox4D.Center(1));
                var axis=new Vector4D(.2,.6,-.3,.8);motion.ProjectionInterval(axis,0,1,false,out double min,out double max);
                for(int i=0;i<=30;i++)for(int mask=0;mask<16;mask++)
                {
                    var h=motion.Half;var corner=new Vector4D((mask&1)==0?-h.X:h.X,(mask&2)==0?-h.Y:h.Y,(mask&4)==0?-h.Z:h.Z,(mask&8)==0?-h.W:h.W);
                    var state=motion.ShapeAt(i/30d);double d=Vector4D.Dot(axis,state.Position+state.Rotation*corner);Assert.That(d,Is.InRange(min-1e-9,max+1e-9));
                }
                var rotated=pose.RotatedAboutCenter(plane,.7).RotatedAboutCenter(plane,-.7);var restored=CompoundBox4D.Slice(rotated,1);
                Assert.That(restored.Maximum.X,Is.EqualTo(wide.Maximum.X).Within(1e-8));Assert.That(restored.Triangles.Count%3,Is.Zero);
            }
        }
    }
}
