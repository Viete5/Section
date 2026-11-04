using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Section.Geometry4D;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
namespace Section.Sandbox.Tests
{
    public sealed class GeometryVisualizationTests
    {
        GeometryDiagnostics diagnostics;
        GeometrySliceView sphere,cube;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Dev/Sandbox_01.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor authoring scene test.");
#endif
            diagnostics=UnityEngine.Object.FindFirstObjectByType<GeometryDiagnostics>();
            sphere=diagnostics.Views.Single(v=>v.Id=="B1");cube=diagnostics.Views.Single(v=>v.Id=="C1");
            cube.GetComponent<FourDimensionalBody>().enabled=false; // M3 tests intentionally include free, unsupported visual configurations.
            yield return null;yield return null;
        }
        [UnityTest]
        public IEnumerator DefaultDemosUseApprovedZonesAndIdleDoesNotRebuildMeshes()
        {
            Assert.That(sphere.transform.position,Is.EqualTo(new Vector3(-4,1.9f,13.2f)));
            Assert.That(cube.transform.position,Is.EqualTo(new Vector3(-4,1.8f,23.4f)));
            Assert.That(sphere.Extent,Is.EqualTo(1.6));Assert.That(cube.Extent,Is.EqualTo(.8));
            Assert.That(sphere.CurrentSlice.AnalyticRadius,Is.EqualTo(1.6));Assert.That(cube.CurrentSlice.Faces.Count,Is.EqualTo(6));
            Assert.That(sphere.GetComponent<SliceColliderView>().ActiveCollider,Is.Not.Null);Assert.That(cube.GetComponent<SliceColliderView>().ActiveCollider,Is.Not.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None).Length,Is.EqualTo(4),"No extra M2 interaction targets.");
            int a=sphere.MeshBuildCount,b=cube.MeshBuildCount;
            for(int i=0;i<20;i++)yield return null;
            diagnostics.SetTestW(0);
            Assert.That(sphere.MeshBuildCount,Is.EqualTo(a));Assert.That(cube.MeshBuildCount,Is.EqualTo(b));
            ValidateMesh(sphere);ValidateMesh(cube);
        }
        [UnityTest]
        public IEnumerator DiagnosticWUpdatesBothSourcesAndEmptySlicesRetainIdentity()
        {
            int meshA=sphere.GeneratedMesh.GetInstanceID(),meshB=cube.GeneratedMesh.GetInstanceID();
            Transform4D original=cube.State;
            diagnostics.SetTestW(.8);yield return null;
            Assert.That(sphere.SliceW,Is.EqualTo(.8));Assert.That(cube.SliceW,Is.EqualTo(.8));
            Assert.That(sphere.CurrentSlice.AnalyticRadius,Is.EqualTo(1.385640646055102).Within(1e-12));
            Assert.That(cube.CurrentSlice.Kind,Is.EqualTo(SliceKind.Volume),"The closed axis-aligned box includes its W facet.");
            diagnostics.SetTestW(2);yield return null;
            foreach(var view in diagnostics.Views)
            {
                Assert.That(view.CurrentSlice.Kind,Is.EqualTo(SliceKind.Empty));Assert.That(view.GeneratedMesh.vertexCount,Is.EqualTo(0));
                Assert.That(view.GetComponent<MeshRenderer>().enabled,Is.False);Assert.That(view.gameObject.activeInHierarchy,Is.True);
            }
            Assert.That((cube.State.Position-original.Position).Length,Is.EqualTo(0));
            diagnostics.SetTestW(0);yield return null;
            Assert.That(sphere.GeneratedMesh.GetInstanceID(),Is.EqualTo(meshA));Assert.That(cube.GeneratedMesh.GetInstanceID(),Is.EqualTo(meshB));
            ValidateMesh(sphere);ValidateMesh(cube);
        }
        [UnityTest]
        public IEnumerator RotationChangesTheActualMeshAndResetRestoresTheCanonicalState()
        {
            cube.Rotate(RotationPlane.XW,45);yield return null;
            Assert.That(cube.GeneratedMesh.bounds.size.x,Is.EqualTo((float)(1.6*Math.Sqrt(2))).Within(2e-6f));
            cube.Rotate(RotationPlane.YW,28);cube.Rotate(RotationPlane.ZW,17);diagnostics.SetTestW(.2);yield return null;
            Assert.That(cube.CurrentSlice.Faces.Count,Is.GreaterThan(6));ValidateMesh(cube);
            var state=cube.State;cube.SetState(state.Translated(new Vector4D(.3,.2,-.4,.5)));diagnostics.SetTestW(.7);yield return null;
            Assert.That(cube.transform.position.x,Is.EqualTo(-3.7f).Within(1e-6f));ValidateMesh(cube);
            diagnostics.ResetAll();yield return null;
            Assert.That(diagnostics.TestW,Is.EqualTo(0));Assert.That(cube.CurrentSlice.Vertices.Count,Is.EqualTo(8));
            Assert.That(cube.transform.position,Is.EqualTo(new Vector3(-4,1.8f,23.4f)));
            Assert.That(cube.State.Rotation[0,0],Is.EqualTo(1));ValidateMesh(cube);
        }
        [UnityTest]
        public IEnumerator DegenerateAndNearBoundaryResultsNeverUploadInvalidSolids()
        {
            diagnostics.SetTestW(1.6);yield return null;
            Assert.That(sphere.CurrentSlice.Kind,Is.EqualTo(SliceKind.Degenerate));Assert.That(sphere.CurrentSlice.Dimension,Is.EqualTo(0));
            Assert.That(sphere.GeneratedMesh.vertexCount,Is.EqualTo(0));
            cube.Rotate(RotationPlane.XW,45);diagnostics.SetTestW(.8*Math.Sqrt(2));yield return null;
            Assert.That(cube.CurrentSlice.Kind,Is.EqualTo(SliceKind.Degenerate));Assert.That(cube.CurrentSlice.Dimension,Is.EqualTo(2));
            Assert.That(cube.GeneratedMesh.vertexCount,Is.EqualTo(0));
            diagnostics.SetTestW(.8*Math.Sqrt(2)-1e-5);yield return null;
            Assert.That(cube.CurrentSlice.Kind,Is.EqualTo(SliceKind.Volume));ValidateMesh(cube);
            Assert.Throws<ArgumentOutOfRangeException>(()=>diagnostics.SetTestW(double.NaN));
            Assert.That(diagnostics.TestW,Is.EqualTo(.8*Math.Sqrt(2)-1e-5));
        }
        [UnityTest]
        public IEnumerator SubFloatSectionsKeepTheirDoubleGeometryWithoutUploadingCollapsedTriangles()
        {
            cube.Rotate(RotationPlane.XW,35);cube.Rotate(RotationPlane.YW,28);cube.Rotate(RotationPlane.ZW,17);
            double support=0;for(int i=0;i<4;i++)support+=.8*Math.Abs(cube.State.Rotation[3,i]);
            diagnostics.SetTestW(support-5e-9);yield return null;
            Assert.That(cube.CurrentSlice.Kind,Is.EqualTo(SliceKind.Volume));
            if(cube.RenderStatus==SliceRenderStatus.Ready)ValidateMesh(cube);
            else{Assert.That(cube.RenderStatus,Is.EqualTo(SliceRenderStatus.BelowFloatPrecision));Assert.That(cube.GeneratedMesh.vertexCount,Is.EqualTo(0));}
            diagnostics.ResetAll();yield return null;Assert.That(cube.RenderStatus,Is.EqualTo(SliceRenderStatus.Ready));
        }
        [UnityTest]
        public IEnumerator SequentialChangesPublishTheSameResultUsedByTheMeshWithoutMeshObjectGrowth()
        {
            // The Editor may retain an unrelated mesh with the same name during scene/test transitions.
            // Measure growth across this operation, and separately check the two actual live owners.
            int baselineMeshes=Resources.FindObjectsOfTypeAll<Mesh>().Count(m=>m.name=="B1_ComputedSlice"||m.name=="C1_ComputedSlice");
            int notifications=0;
            cube.SliceChanged+=view=>
            {
                notifications++;Assert.That(view.CurrentSlice,Is.Not.Null);
                Assert.That(view.GeneratedMesh.vertexCount==0,Is.EqualTo(view.RenderStatus!=SliceRenderStatus.Ready));
            };
            int originalMesh=cube.GeneratedMesh.GetInstanceID();
            for(int i=0;i<60;i++)
            {
                cube.Rotate((RotationPlane)(i%6),2.1);
                diagnostics.SetTestW(Math.Sin(i*.2)*1.5);
                if(cube.CurrentSlice.Kind==SliceKind.Volume)ValidateMesh(cube);
            }
            yield return null;
            Assert.That(notifications,Is.GreaterThan(60));Assert.That(cube.GeneratedMesh.GetInstanceID(),Is.EqualTo(originalMesh));
            sphere.enabled=false;yield return null;sphere.enabled=true;yield return null;
            Assert.That(sphere.GeneratedMesh,Is.Not.Null);
            var meshes=Resources.FindObjectsOfTypeAll<Mesh>().Where(m=>m.name=="B1_ComputedSlice"||m.name=="C1_ComputedSlice").ToArray();
            Assert.That(meshes.Length,Is.LessThanOrEqualTo(baselineMeshes));
            Assert.That(sphere.GeneratedMesh,Is.Not.SameAs(cube.GeneratedMesh));
        }
        static void ValidateMesh(GeometrySliceView view)
        {
            var mesh=view.GeneratedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;var normals=mesh.normals;
            Assert.That(vertices.Length,Is.GreaterThan(3));Assert.That(normals.Length,Is.EqualTo(vertices.Length));
            foreach(var v in vertices)Assert.That(float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z),Is.True);
            foreach(var n in normals)Assert.That(n.magnitude,Is.EqualTo(1).Within(2e-5f));
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                Assert.That(a,Is.InRange(0,vertices.Length-1));Assert.That(b,Is.InRange(0,vertices.Length-1));Assert.That(c,Is.InRange(0,vertices.Length-1));
                var cross=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                Assert.That(cross.sqrMagnitude,Is.GreaterThan(0));Assert.That(Vector3.Dot(cross,normals[a]),Is.GreaterThan(0));
            }
            Assert.That(mesh.bounds.size.x,Is.GreaterThan(0));Assert.That(mesh.bounds.size.y,Is.GreaterThan(0));Assert.That(mesh.bounds.size.z,Is.GreaterThan(0));
        }
    }
}
