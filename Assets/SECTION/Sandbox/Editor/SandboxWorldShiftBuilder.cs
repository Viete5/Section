using System;
using System.Linq;
using Section.Geometry4D;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Section.Sandbox.Editor
{
    internal static class SandboxWorldShiftBuilder
    {
        internal static void Populate(SandboxSceneBuilder.AuthoringContext c,Transform player,SandboxPlayerInput input,Transform view,Transform anchor,SandboxLook look)
        {
            Scene scene=SceneManager.GetActiveScene();
            var metal=SandboxSceneBuilder.Material("Fixture",new Color(.38f,.42f,.46f));
            var lamp=SandboxSceneBuilder.Material("Indicator",new Color(.3f,.75f,.9f));
            var platformMaterial=SandboxSceneBuilder.Material("SlicePlatform",new Color(.35f,.65f,.55f));
            var sphereMaterial=SandboxSceneBuilder.Material("Hypersphere",new Color(.22f,.55f,.72f));
            var stationB1=SandboxSceneBuilder.Find(scene,"station.B1").transform;
            var stationB2=SandboxSceneBuilder.Find(scene,"station.B2").transform;
            var stationB3=SandboxSceneBuilder.Find(scene,"station.B3").transform;
            var b1=SandboxSceneBuilder.Find(scene,"geometry.B1").GetComponent<GeometrySliceView>();
            var c1=SandboxSceneBuilder.Find(scene,"geometry.C1").GetComponent<GeometrySliceView>();
            GeometrySliceView Prism(string id,double centerW,double centerZ,Vector4D half,Vector3D shear,GeometrySliceView.CanonicalPlane[] clips=null)
            {
                var node=c.Node("geometry."+id,id+"_4DPrism",stationB2,new Vector3(4.25f,-.125f,(float)centerZ));
                var source=c.Component<GeometrySliceView>(node);node.GetComponent<MeshRenderer>().sharedMaterial=platformMaterial;
                source.ConfigurePrism(id,new Vector4D(4.25,-.125,centerZ,centerW),half,shear,clips);
                return source;
            }
            // P1 is a sheared prism. P2 is a fixed 4D box clipped by Z+2.5W >= 18.55.
            // Its actual section grows toward the landing; no animation, snapping or artificial jump gate.
            var p1=Prism("B2.P1",.25,13.6875,new Vector4D(.9,.125,.2,.3),new Vector3D(0,0,-1.25));
            var p2=Prism("B2.P2",1.1,17.375,new Vector4D(.9,.125,.575,.7),default,
                new[]{new GeometrySliceView.CanonicalPlane{z=-1,w=-2.5,offset=1.575}});
            GeometrySliceView Sphere(string id,Vector4D center)
            {
                var node=c.Node("geometry."+id,id+"_Hypersphere",stationB3,SliceMeshWriter.ToVector3(center.XYZ));
                var source=c.Component<GeometrySliceView>(node);node.GetComponent<MeshRenderer>().sharedMaterial=sphereMaterial;
                source.Configure(id,Shape4D.Hypersphere,.7,center);return source;
            }
            var u=Sphere("B3.U",new Vector4D(-5,1.2,17,-.45));
            var v=Sphere("B3.V",new Vector4D(-3,1.2,17,.45));
            var sources=new[]{b1,c1,p1,p2,u,v};
            foreach(var source in sources)c.Component<SliceColliderView>(source.transform);
            string settingsPath=SandboxSceneBuilder.AssetRoot+"/Data/WorldShiftSettings.asset";
            var settings=AssetDatabase.LoadAssetAtPath<WorldShiftSettings>(settingsPath);
            if(settings==null){settings=ScriptableObject.CreateInstance<WorldShiftSettings>();AssetDatabase.CreateAsset(settings,settingsPath);}
            var root=SandboxSceneBuilder.Find(scene,"root").transform;
            var world=c.Component<WorldSliceState>(c.Node("world.slice","WorldSliceState",root));
            world.Configure(settings,player.GetComponent<SandboxMotor>(),sources,UnityEngine.Object.FindObjectsByType<PhysicalItem>(FindObjectsSortMode.None));
            SandboxSceneBuilder.Find(scene,"geometry.diagnostics").GetComponent<GeometryDiagnostics>().BindWorld(world);
            var standard=SandboxSceneBuilder.Find(scene,"device.controller").GetComponent<StandardManipulator>();
            var prefab=DevicePrefab(metal,lamp);
            var model=c.Node("device.shift.model","WorldShiftDevice_Equipped",anchor,new Vector3(.25f,-.22f,.45f));
            model.localScale=Vector3.one*.75f;
            if(model.Find("WorldShiftDevice")==null)
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,model);instance.name="WorldShiftDevice";
                instance.transform.localPosition=Vector3.zero;instance.transform.localRotation=Quaternion.identity;
            }
            model.gameObject.SetActive(false);
            var device=c.Component<WorldShiftDevice>(c.Node("device.shift.controller","WorldShiftDevice_Controller",player));
            device.Configure(input,world,model.gameObject,model.Find("WorldShiftDevice/StatusLamp").GetComponent<Renderer>());
            c.Component<EquipmentSlots>(c.Node("equipment.slots","EquipmentSlots",player)).Configure(input,standard,device);
            var interactor=c.Component<StationInteractor>(c.Node("station.interactor","StationInteractor",player));interactor.Configure(input,standard);
            SandboxSceneBuilder.Find(scene,"interaction.hud").GetComponent<InteractionHud>().ConfigureWorldShift(device,interactor,input);
            var support=c.Component<PlayerSupportTracker>(player);

            c.Box("b1.pedestal","B1_Pedestal",stationB1,new Vector3(-4,.2f,13.2f),new Vector3(2,.4f,2),metal);
            var conditionB1=c.Component<GeometryValidationCondition>(stationB1);conditionB1.Configure(b1,world);
            Station(c,stationB1,"B1",conditionB1,new Vector3(-1.9f,.7f,13),new[]{b1},false,world,standard,look,metal,lamp);
            var conditionB2=c.Component<SpatialPlatformCondition>(stationB2);
            var ownerB2=Station(c,stationB2,"B2",conditionB2,new Vector3(3,.7f,12),new[]{p1,p2},true,world,standard,look,metal,lamp);
            conditionB2.Configure(world,support,p1,p2,ownerB2);
            SlicePresenceSensor Sensor(string id,GeometrySliceView source,Vector3 point)
            {
                var probe=c.Box(id+".probe",id+"_OpticalProbe",stationB3,point,Vector3.one*.06f,lamp,false);
                var sensor=c.Component<SlicePresenceSensor>(probe);sensor.Configure(source,probe);return sensor;
            }
            var sensorU=Sensor("b3.u",u,new Vector3(-4.6f,1.2f,17));
            var sensorV=Sensor("b3.v",v,new Vector3(-2.6f,1.2f,17));
            var conditionB3=c.Component<DualSliceCondition>(stationB3);conditionB3.Configure(world,sensorU,sensorV);
            var ownerB3=Station(c,stationB3,"B3",conditionB3,new Vector3(-1.75f,.7f,18.6f),new[]{u,v},false,world,standard,look,metal,lamp);
            var begin=c.Box("b3.begin","Begin_B3",stationB3,new Vector3(-1.75f,.7f,17),new Vector3(.28f,.16f,.24f),lamp);
            c.Component<StationResetButton>(begin).Configure(ownerB3,true);
            var beginLabel=Label(c,"b3.begin.label",stationB3,new Vector3(-1.75f,1.1f,17),"BEGIN B3 [F]\nSTART W = -0.8");
            c.Component<WorldTextBinding>(beginLabel.transform);
        }
        internal static TestStation Station(SandboxSceneBuilder.AuthoringContext c,Transform root,string id,StationCondition condition,Vector3 position,
            GeometrySliceView[] owned,bool restorePlayer,WorldSliceState world,StandardManipulator device,SandboxLook look,Material metal,Material lamp)
        {
            string key=id.ToLowerInvariant();
            var station=c.Component<TestStation>(root);
            c.Box(key+".reset.base",id+"_ResetStand",root,new Vector3(position.x,position.y*.5f,position.z),new Vector3(.18f,position.y,.18f),metal);
            var button=c.Box(key+".reset",id+"_ResetButton",root,position,new Vector3(.28f,.16f,.24f),lamp);
            c.Component<StationResetButton>(button).Configure(station);
            var indicator=c.Box(key+".indicator",id+"_Indicator",root,position+Vector3.up*.27f,new Vector3(.18f,.12f,.1f),lamp,false);
            var text=Label(c,key+".readout",root,position+new Vector3(0,.8f,-.025f),id+" RESET [F]");
            station.Configure(id,Array.Empty<PhysicalItem>(),condition,indicator.GetComponent<Renderer>(),text);
            var reset=c.Component<GeometryResetHandler>(root);reset.Configure(world,owned,restorePlayer,device,look);station.SetGeometryReset(reset);
            return station;
        }
        internal static TextMesh Label(SandboxSceneBuilder.AuthoringContext c,string key,Transform root,Vector3 position,string caption)
        {
            var node=c.Node(key,key,root,position);var text=c.Component<TextMesh>(node);
            text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=40;text.characterSize=.025f;
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=Color.white;text.text=caption;
            c.Component<MeshRenderer>(node).sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(SandboxSceneBuilder.AssetRoot+"/Materials/WorldText.mat");return text;
        }
        static GameObject DevicePrefab(Material metal,Material lamp)
        {
            string path=SandboxSceneBuilder.AssetRoot+"/Prefabs/WorldShiftDevice.prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var root=new GameObject("WorldShiftDevice");
            try
            {
                void Part(string name,Vector3 position,Vector3 size,Material material)
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root.transform,false);
                    go.transform.localPosition=position;go.transform.localScale=size;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                    go.GetComponent<Renderer>().sharedMaterial=material;
                }
                Part("Body",Vector3.zero,new Vector3(.19f,.12f,.24f),metal);
                Part("Grip",new Vector3(0,-.1f,-.06f),new Vector3(.07f,.16f,.08f),metal);
                Part("LeftRail",new Vector3(-.13f,.02f,.08f),new Vector3(.04f,.16f,.26f),metal);
                Part("RightRail",new Vector3(.13f,.02f,.08f),new Vector3(.04f,.16f,.26f),metal);
                Part("Crossbar",new Vector3(0,.1f,.2f),new Vector3(.28f,.035f,.04f),metal);
                Part("StatusLamp",new Vector3(0,.075f,.02f),new Vector3(.13f,.025f,.12f),lamp);
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
