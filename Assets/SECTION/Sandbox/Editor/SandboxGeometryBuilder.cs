using Section.Geometry4D;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Section.Sandbox.Editor
{
    internal static class SandboxGeometryBuilder
    {
        internal static void Populate(SandboxSceneBuilder.AuthoringContext context)
        {
            Scene scene=SceneManager.GetActiveScene();
            var sphereMaterial=SandboxSceneBuilder.Material("Hypersphere",new Color(.22f,.55f,.72f));
            var cubeMaterial=SandboxSceneBuilder.Material("Tesseract",new Color(.63f,.43f,.72f));
            GeometrySliceView Add(string id,Shape4D shape,double extent,Vector4D center,Material material)
            {
                var station=SandboxSceneBuilder.Find(scene,"station."+id).transform;
                var node=context.Node("geometry."+id,id+"_GeometryDiagnostic",station,SliceMeshWriter.ToVector3(center.XYZ));
                var view=context.Component<GeometrySliceView>(node);
                node.GetComponent<MeshRenderer>().sharedMaterial=material;
                view.Configure(id,shape,extent,center);return view;
            }
            var sphere=Add("B1",Shape4D.Hypersphere,1.6,new Vector4D(-4,1.9,13.2,0),sphereMaterial);
            var cube=Add("C1",Shape4D.Tesseract,.8,new Vector4D(-4,1.8,23.4,0),cubeMaterial);
            var root=SandboxSceneBuilder.Find(scene,"developer.diagnostics").transform;
            var diagnostics=context.Component<GeometryDiagnostics>(context.Node("geometry.diagnostics","Geometry4D_Diagnostics",root));
            diagnostics.Configure(new[]{sphere,cube});
        }
    }
}
