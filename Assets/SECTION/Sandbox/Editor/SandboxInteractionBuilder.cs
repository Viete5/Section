using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Section.Sandbox.Editor
{
    internal static class SandboxInteractionBuilder
    {
        const string Root = SandboxSceneBuilder.AssetRoot;
        internal static void Populate(SandboxSceneBuilder.AuthoringContext c, Transform player, SandboxPlayerInput input,
            Transform view, Transform anchor, SandboxLook look)
        {
            var blue = SandboxSceneBuilder.Material("ItemBlue", new Color(.12f, .42f, .72f));
            var light = SandboxSceneBuilder.Material("ItemLight", new Color(.75f, .7f, .38f));
            var heavy = SandboxSceneBuilder.Material("ItemHeavy", new Color(.34f, .38f, .42f));
            var orange = SandboxSceneBuilder.Material("ItemOrange", new Color(.85f, .42f, .16f));
            var metal = SandboxSceneBuilder.Material("Fixture", new Color(.38f, .42f, .46f));
            var green = SandboxSceneBuilder.Material("Plate", new Color(.2f, .48f, .3f));
            var lampMaterial = SandboxSceneBuilder.Material("Indicator", new Color(.3f, .75f, .9f));
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var a1 = SandboxSceneBuilder.Find(scene, "station.A1").transform;
            var a2 = SandboxSceneBuilder.Find(scene, "station.A2").transform;
            var a3 = SandboxSceneBuilder.Find(scene, "station.A3").transform;

            c.Box("a1.table", "A1_Table", a1, new Vector3(-4,.45f,3), new Vector3(1.8f,.9f,1.4f), metal);
            var cube = Item(c, "a1.cube", "A1_BlueCube", a1, new Vector3(-4,1.2f,3), Vector3.one*.6f, 2, blue);
            var plate1 = c.Box("a1.plate", "A1_LoadPlate", a1, new Vector3(-4,.06f,5.5f), new Vector3(1.2f,.12f,1.2f), green);
            var sensor1 = c.Component<LoadPlateSensor>(plate1); sensor1.Configure(.1f);
            Station(c, a1, "A1", new[]{cube}, sensor1, new Vector3(-2.9f,.7f,5.5f), metal, lampMaterial);

            var lightCube = Item(c,"a2.light","A2_LightCube",a2,new Vector3(3.3f,.25f,3),Vector3.one*.5f,1,light);
            var heavyCube = Item(c,"a2.heavy","A2_HeavyCube",a2,new Vector3(4.7f,.35f,3),Vector3.one*.7f,5,heavy);
            var plate2 = c.Box("a2.plate","A2_LoadPlate",a2,new Vector3(4,.06f,5.5f),new Vector3(1.4f,.12f,1.4f),green);
            var sensor2 = c.Component<LoadPlateSensor>(plate2); sensor2.Configure(4);
            Station(c,a2,"A2",new[]{lightCube,heavyCube},sensor2,new Vector3(5.2f,.7f,5.5f),metal,lampMaterial);

            var block = Item(c,"a3.block","A3_RotationBlock",a3,new Vector3(-5.1f,.275f,8.1f),new Vector3(.45f,.55f,1.4f),2,orange);
            var socket = c.Node("a3.socket","A3_Socket",a3,new Vector3(-3.025f,.9f,8.1f));
            var bottom = c.Box("a3.socket.floor","Socket_Floor",a3,new Vector3(-3.025f,.525f,8.1f),new Vector3(1.55f,.1f,.85f),metal);
            c.Box("a3.socket.base","Socket_Base",a3,new Vector3(-3.025f,.2375f,8.1f),new Vector3(1.55f,.475f,.85f),metal);
            c.Box("a3.socket.ceiling","Socket_Ceiling",a3,new Vector3(-3.025f,1.275f,8.1f),new Vector3(1.55f,.1f,.85f),metal);
            c.Box("a3.socket.near","Socket_NearSide",a3,new Vector3(-3.025f,.9f,7.725f),new Vector3(1.55f,.65f,.1f),metal);
            c.Box("a3.socket.far","Socket_FarSide",a3,new Vector3(-3.025f,.9f,8.475f),new Vector3(1.55f,.65f,.1f),metal);
            c.Box("a3.socket.back","Socket_BackStop",a3,new Vector3(-2.225f,.9f,8.1f),new Vector3(.05f,.85f,.85f),metal);
            var socketSensor = c.Component<SocketSensor>(socket); socketSensor.Configure(block,bottom.GetComponent<BoxCollider>());
            Station(c,a3,"A3",new[]{block},socketSensor,new Vector3(-5.8f,.7f,9),metal,lampMaterial);

            string settingsPath=Root+"/Data/ManipulatorSettings.asset";
            var settings=AssetDatabase.LoadAssetAtPath<ManipulatorSettings>(settingsPath);
            if(settings==null) { settings=ScriptableObject.CreateInstance<ManipulatorSettings>();AssetDatabase.CreateAsset(settings,settingsPath); }
            GameObject prefab=DevicePrefab(metal,lampMaterial);
            var model=c.Node("device.model","StandardManipulator_Equipped",anchor,new Vector3(.25f,-.22f,.45f));
            model.localScale=Vector3.one*.75f;
            if(model.Find("StandardManipulator") == null)
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,model);
                instance.name="StandardManipulator"; instance.transform.localPosition=Vector3.zero;
                instance.transform.localRotation=Quaternion.identity;
            }
            var device=c.Component<StandardManipulator>(c.Node("device.controller","StandardManipulator_Controller",player));
            device.Configure(settings,input,view,model.gameObject,model.Find("StandardManipulator/StatusLamp").GetComponent<Renderer>());
            look.SetManipulator(device);
            BuildHud(c,device);
        }
        static PhysicalItem Item(SandboxSceneBuilder.AuthoringContext c,string id,string name,Transform parent,Vector3 position,Vector3 size,float mass,Material material)
        {
            var t=c.Node(id,name,parent,position);
            var collider=c.Component<BoxCollider>(t);collider.size=size;collider.contactOffset=.005f;
            var body=c.Component<Rigidbody>(t);body.mass=mass;body.useGravity=true;body.isKinematic=false;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;body.interpolation=RigidbodyInterpolation.Interpolate;
            body.linearDamping=.1f;body.angularDamping=.15f;body.maxAngularVelocity=3;
            body.solverIterations=12;body.solverVelocityIterations=4;
            var item=c.Component<PhysicalItem>(t);item.Configure(id,position,Quaternion.identity);
            c.Box(id+".visual",name+"_Mesh",t,Vector3.zero,size,material,false);
            return item;
        }
        static void Station(SandboxSceneBuilder.AuthoringContext c,Transform root,string id,PhysicalItem[] items,StationCondition sensor,Vector3 buttonPosition,Material metal,Material lamp)
        {
            string key=id.ToLowerInvariant();
            var station=c.Component<TestStation>(root);
            c.Box(key+".reset.base",id+"_ResetStand",root,new Vector3(buttonPosition.x,buttonPosition.y*.5f,buttonPosition.z),new Vector3(.18f,buttonPosition.y,.18f),metal);
            var button=c.Box(key+".reset",id+"_ResetButton",root,buttonPosition,new Vector3(.28f,.16f,.24f),lamp);
            c.Component<StationResetButton>(button).Configure(station);
            var indicator=c.Box(key+".indicator",id+"_Indicator",root,buttonPosition+new Vector3(0,.27f,0),new Vector3(.18f,.12f,.1f),lamp,false);
            var text=c.Node(key+".readout",id+"_Readout",root,buttonPosition+new Vector3(0,id=="A3"?1.0f:.55f,-.025f));
            text.localRotation=Quaternion.identity;
            var label=c.Component<TextMesh>(text);label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize=40;label.characterSize=.025f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;
            label.color=Color.white;label.text=id+" RESET [F]";
            c.Component<MeshRenderer>(text).sharedMaterial=WorldTextMaterial();
            station.Configure(id,items,sensor,indicator.GetComponent<Renderer>(),label);
        }
        static Material WorldTextMaterial()
        {
            string path=Root+"/Materials/WorldText.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null)return material;
            var shader=Shader.Find("SECTION/WorldText");
            if(shader==null)throw new System.InvalidOperationException("World text shader is unavailable.");
            material=new Material(shader){name="WorldText"};AssetDatabase.CreateAsset(material,path);return material;
        }
        static GameObject DevicePrefab(Material body,Material lamp)
        {
            string path=Root+"/Prefabs/StandardManipulator.prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            Directory.CreateDirectory(Root+"/Prefabs");AssetDatabase.Refresh();
            var root=new GameObject("StandardManipulator");
            try {
                void Part(string name,Vector3 position,Vector3 size,Material material)
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root.transform,false);
                    go.transform.localPosition=position;go.transform.localScale=size;Object.DestroyImmediate(go.GetComponent<Collider>());
                    go.GetComponent<Renderer>().sharedMaterial=material;
                }
                Part("Body",Vector3.zero,new Vector3(.15f,.11f,.28f),body);
                Part("Grip",new Vector3(0,-.1f,-.07f),new Vector3(.065f,.16f,.08f),body);
                Part("LeftProng",new Vector3(-.085f,0,.18f),new Vector3(.035f,.07f,.16f),body);
                Part("RightProng",new Vector3(.085f,0,.18f),new Vector3(.035f,.07f,.16f),body);
                Part("StatusLamp",new Vector3(0,.063f,.02f),new Vector3(.07f,.025f,.12f),lamp);
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            } finally { Object.DestroyImmediate(root); }
        }
        static void BuildHud(SandboxSceneBuilder.AuthoringContext c,StandardManipulator device)
        {
            string path=Root+"/Data/InteractionPanel.asset";
            var settings=AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if(settings==null) {settings=ScriptableObject.CreateInstance<PanelSettings>();settings.scaleMode=PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution=new Vector2Int(1440,900);AssetDatabase.CreateAsset(settings,path);}
            var root=SandboxSceneBuilder.Find(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"root").transform;
            var hud=c.Node("interaction.hud","InteractionHUD",root);
            var document=c.Component<UIDocument>(hud);document.panelSettings=settings;
            document.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root+"/Runtime/Presentation/InteractionHud.uxml");
            c.Component<InteractionHud>(hud).Configure(device);
        }
    }
}
