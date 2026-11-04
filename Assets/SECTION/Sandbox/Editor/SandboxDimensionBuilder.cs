using System.Linq;
using Section.Geometry4D;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Section.Sandbox.Editor
{
    internal static class SandboxDimensionBuilder
    {
        internal static void Populate(SandboxSceneBuilder.AuthoringContext c,Transform player,SandboxPlayerInput input,Transform camera,Transform anchor,SandboxLook look)
        {
            var scene=SceneManager.GetActiveScene();
            var root=SandboxSceneBuilder.Find(scene,"root").transform;
            var world=SandboxSceneBuilder.Find(scene,"world.slice").GetComponent<WorldSliceState>();
            var solver=c.Component<FourDimensionalPhysics>(c.Node("dimension.physics","FourDimensionalPhysics",root));solver.Configure(world);
            var metal=SandboxSceneBuilder.Material("Fixture",new Color(.38f,.42f,.46f));
            var lamp=SandboxSceneBuilder.Material("Indicator",new Color(.3f,.75f,.9f));
            var orange=SandboxSceneBuilder.Material("ItemOrange",new Color(.85f,.42f,.16f));
            var purple=SandboxSceneBuilder.Material("Tesseract",new Color(.63f,.43f,.72f));
            var c1=SandboxSceneBuilder.Find(scene,"station.C1").transform;
            var c2=SandboxSceneBuilder.Find(scene,"station.C2").transform;
            var c3=SandboxSceneBuilder.Find(scene,"station.C3").transform;
            var cube=SandboxSceneBuilder.Find(scene,"geometry.C1").GetComponent<GeometrySliceView>();
            var cubeBody=c.Component<FourDimensionalBody>(cube.transform);cubeBody.Configure(solver);
            c.Box("c1.pedestal","C1_Pedestal",c1,new Vector3(-4,.5f,23.4f),new Vector3(2.2f,1,2.2f),metal);
            FourDimensionalBody Body(string id,Transform parent,Vector4D center,Vector4D half)
            {
                var node=c.Node("geometry."+id,id+"_4DBody",parent,SliceMeshWriter.ToVector3(center.XYZ));
                var source=c.Component<GeometrySliceView>(node);node.GetComponent<MeshRenderer>().sharedMaterial=orange;
                source.ConfigurePrism(id,center,half,default);c.Component<SliceColliderView>(node);
                var body=c.Component<FourDimensionalBody>(node);body.Configure(solver);return body;
            }
            var passage=Body("C2",c2,new Vector4D(4.5,1,23.3,0),new Vector4D(.8,.25,.8,.15));
            c.Box("c2.table","C2_InitialPedestal",c2,new Vector3(4.5f,.375f,23.3f),new Vector3(1.4f,.75f,1),metal);
            c.Box("c2.wall.left","C2_Wall_Left",c2,new Vector3(3,1.5f,25),new Vector3(2,3,.3f),metal);
            c.Box("c2.wall.right","C2_Wall_Right",c2,new Vector3(5.75f,1.5f,25),new Vector3(1.5f,3,.3f),metal);
            c.Box("c2.wall.sill","C2_Window_Sill",c2,new Vector3(4.5f,.375f,25),new Vector3(1,.75f,.3f),metal);
            c.Box("c2.wall.header","C2_Window_Header",c2,new Vector3(4.5f,2.225f,25),new Vector3(1,1.55f,.3f),metal);
            var pad=c.Box("c2.pad","C2_ReceiverPad",c2,new Vector3(4.5f,.1f,27),new Vector3(1.4f,.2f,1.2f),purple);
            var connector=Body("C3",c3,new Vector4D(-4,1.2,26.4,.7),new Vector4D(.15,.15,.15,1));
            c.Box("c3.table","C3_SupportTable",c3,new Vector3(-4,.525f,26.7f),new Vector3(2.6f,1.05f,1.7f),metal);
            var contactA=c.Box("c3.contact.a","C3_Contact_A",c3,new Vector3(-4.8f,1.2f,27),Vector3.one*.08f,lamp,false);
            var contactB=c.Box("c3.contact.b","C3_Contact_B",c3,new Vector3(-3.2f,1.2f,27),Vector3.one*.08f,lamp,false);
            var sources=world.Sources.Concat(new[]{passage.Source,connector.Source}).Distinct().ToArray();
            world.Configure(world.Settings,player.GetComponent<SandboxMotor>(),sources,Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None));
            foreach(var body in new[]{cubeBody,passage,connector})
            {
                var projection=c.Node("projection."+body.Id,body.Id+"_OrdinaryContactProjection",body.transform);
                projection.gameObject.layer=2; // Built-in Ignore Raycast, no project layer/configuration changes.
                c.Component<OrdinaryContactProjection>(projection).Configure(body);
            }
            var standard=SandboxSceneBuilder.Find(scene,"device.controller").GetComponent<StandardManipulator>();
            string configPath=SandboxSceneBuilder.AssetRoot+"/Data/DimensionManipulatorSettings.asset";
            var settings=AssetDatabase.LoadAssetAtPath<DimensionManipulatorSettings>(configPath);
            if(settings==null){settings=ScriptableObject.CreateInstance<DimensionManipulatorSettings>();AssetDatabase.CreateAsset(settings,configPath);}
            var prefab=DevicePrefab(metal,purple,lamp);
            var model=c.Node("device.dimension.model","DimensionManipulator_Equipped",anchor,new Vector3(.25f,-.22f,.45f));model.localScale=Vector3.one*.75f;
            if(model.Find("DimensionManipulator")==null)
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,model);instance.name="DimensionManipulator";
                instance.transform.localPosition=Vector3.zero;instance.transform.localRotation=Quaternion.identity;
            }
            model.gameObject.SetActive(false);
            var dimension=c.Component<DimensionManipulator>(c.Node("device.dimension.controller","DimensionManipulator_Controller",player));
            dimension.Configure(settings,input,camera,model.gameObject,model.Find("DimensionManipulator/StatusLamp").GetComponent<Renderer>(),standard);
            SandboxSceneBuilder.Find(scene,"equipment.slots").GetComponent<EquipmentSlots>().SetDimension(dimension);
            SandboxSceneBuilder.Find(scene,"interaction.hud").GetComponent<InteractionHud>().SetDimension(dimension);look.SetDimension(dimension);
            var experiment=c.Component<DimensionExperimentCondition>(c1);experiment.Configure(cubeBody);
            var placement=c.Component<PassagePlacementCondition>(c2);placement.Configure(passage,pad.GetComponent<BoxCollider>());
            var contacts=c.Component<SpatialConnectorCondition>(c3);contacts.Configure(connector,contactA,contactB);
            SandboxWorldShiftBuilder.Station(c,c1,"C1",experiment,new Vector3(-2.6f,.7f,22.8f),new[]{cube},false,world,standard,look,metal,lamp);
            SandboxWorldShiftBuilder.Station(c,c2,"C2",placement,new Vector3(6,.7f,23),new[]{passage.Source},false,world,standard,look,metal,lamp);
            SandboxWorldShiftBuilder.Station(c,c3,"C3",contacts,new Vector3(-2.6f,.7f,28.5f),new[]{connector.Source},false,world,standard,look,metal,lamp);
        }
        static GameObject DevicePrefab(Material metal,Material purple,Material lamp)
        {
            string path=SandboxSceneBuilder.AssetRoot+"/Prefabs/DimensionManipulator.prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var root=new GameObject("DimensionManipulator");
            try
            {
                void Part(string name,Vector3 p,Vector3 size,Material material)
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root.transform,false);
                    go.transform.localPosition=p;go.transform.localScale=size;Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;
                }
                Part("Body",Vector3.zero,new Vector3(.17f,.13f,.27f),metal);
                Part("Grip",new Vector3(0,-.11f,-.06f),new Vector3(.065f,.17f,.08f),metal);
                Part("UpperFrame",new Vector3(0,.105f,.18f),new Vector3(.28f,.035f,.2f),purple);
                Part("LowerFrame",new Vector3(0,-.08f,.18f),new Vector3(.28f,.035f,.2f),purple);
                Part("LeftFrame",new Vector3(-.125f,.015f,.18f),new Vector3(.035f,.18f,.2f),metal);
                Part("RightFrame",new Vector3(.125f,.015f,.18f),new Vector3(.035f,.18f,.2f),metal);
                Part("StatusLamp",new Vector3(0,.08f,0),new Vector3(.1f,.025f,.12f),lamp);
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
