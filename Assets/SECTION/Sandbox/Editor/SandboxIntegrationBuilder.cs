using System.Linq;
using Section.Geometry4D;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Section.Sandbox.Editor
{
    internal static class SandboxIntegrationBuilder
    {
        internal static void Populate(SandboxSceneBuilder.AuthoringContext c,Transform player,SandboxLook look)
        {
            var scene=SceneManager.GetActiveScene();var world=SandboxSceneBuilder.Find(scene,"world.slice").GetComponent<WorldSliceState>();
            var solver=SandboxSceneBuilder.Find(scene,"dimension.physics").GetComponent<FourDimensionalPhysics>();
            var standard=SandboxSceneBuilder.Find(scene,"device.controller").GetComponent<StandardManipulator>();
            var slots=SandboxSceneBuilder.Find(scene,"equipment.slots").GetComponent<EquipmentSlots>();
            var metal=SandboxSceneBuilder.Material("Fixture",new Color(.38f,.42f,.46f));
            var orange=SandboxSceneBuilder.Material("ItemOrange",new Color(.85f,.42f,.16f));
            var lamp=SandboxSceneBuilder.Material("Indicator",new Color(.3f,.75f,.9f));
            var d1=SandboxSceneBuilder.Find(scene,"station.D1").transform;var d2=SandboxSceneBuilder.Find(scene,"station.D2").transform;var d3=SandboxSceneBuilder.Find(scene,"station.D3").transform;
            c.Box("d1.wall.left","D1_Wall_Left",d1,new Vector3(-4.1f,2.5f,33),new Vector3(5.8f,5,.3f),metal);
            c.Box("d1.wall.right","D1_Wall_Right",d1,new Vector3(4.1f,2.5f,33),new Vector3(5.8f,5,.3f),metal);
            c.Box("d1.lintel","D1_Visible_Lintel",d1,new Vector3(0,4.1f,33),new Vector3(2.4f,1.8f,.3f),metal);
            var gate=c.Component<GeometrySliceView>(c.Node("geometry.D1","D1_Hypersphere",d1,new Vector3(0,1.6f,33)));gate.Configure("D1",Shape4D.Hypersphere,1.55,new Vector4D(0,1.6,33,0));
            gate.GetComponent<MeshRenderer>().sharedMaterial=orange;c.Component<SliceColliderView>(gate.transform);
            foreach(int sign in new[]{-1,1})c.Box("d2.guide."+sign,"D2_Guide_"+sign,d2,new Vector3(sign*1.4f,1.5f,35.65f),new Vector3(.3f,3,1.7f),metal);
            GeometrySliceView Wall(int sign)
            {
                var source=c.Component<GeometrySliceView>(c.Node("geometry.D2."+sign,"D2_MovingWall_"+sign,d2,new Vector3(sign*.975f,1.5f,35.65f)));
                source.ConfigurePrism("D2."+sign,new Vector4D(sign*.975,1.5,35.65,0),new Vector4D(.175,1.5,.85,2.5),default);
                source.GetComponent<MeshRenderer>().sharedMaterial=metal;c.Component<SliceColliderView>(source.transform);return source;
            }
            var left=Wall(-1);var right=Wall(1);
            var sourceD3=c.Component<GeometrySliceView>(c.Node("geometry.D3","D3_Compound4DBody",d3,new Vector3(4,1.25f,37)));
            sourceD3.ConfigureCompound("D3",new Vector4D(4,1.25,37,0));sourceD3.GetComponent<MeshRenderer>().sharedMaterial=orange;c.Component<SliceColliderView>(sourceD3.transform);
            var body=c.Component<FourDimensionalBody>(sourceD3.transform);body.Configure(solver);
            for(int p=0;p<2;p++){var proxy=c.Node("projection.D3."+p,"D3_OrdinaryProjection_"+p,body.transform);proxy.gameObject.layer=2;c.Component<OrdinaryContactProjection>(proxy).Configure(body,p);}
            // Existing station zone specifies fixture centres; the approved initial body extends to Z=36.8.
            c.Box("d3.initial.support","D3_InitialSupport",d3,new Vector3(3.5f,.525f,37),new Vector3(1.6f,1.05f,.5f),metal);
            var receiver=c.Box("d3.receiver","D3_Receiver",d3,new Vector3(4,.525f,38),new Vector3(2.6f,1.05f,.8f),metal).GetComponent<BoxCollider>();
            var a=c.Box("d3.contact.a","D3_Contact_A",d3,new Vector3(3.2f,1.25f,38),Vector3.one*.08f,lamp,false);
            var b=c.Box("d3.contact.b","D3_Contact_B",d3,new Vector3(4.8f,1.25f,38),Vector3.one*.08f,lamp,false);
            world.Configure(world.Settings,world.Player,world.Sources.Concat(new[]{gate,left,right,sourceD3}).Distinct().ToArray(),world.ConventionalItems);
            var gateCondition=c.Component<SpatialGateCondition>(d1);gateCondition.Configure(world);
            SandboxWorldShiftBuilder.Station(c,d1,"D1",gateCondition,new Vector3(1.8f,.7f,31.2f),new[]{gate},false,world,standard,look,metal,lamp);
            var walls=c.Component<MovingWallsCondition>(d2);
            var wallStation=SandboxWorldShiftBuilder.Station(c,d2,"D2",walls,new Vector3(-3,.7f,35.65f),new[]{left,right},true,world,standard,look,metal,lamp);
            wallStation.GetComponent<GeometryResetHandler>().SetSafeFeet(new Vector3(-2.6f,0,34.1f));walls.Configure(world,left,right,wallStation);
            walls.SetPhaseLamps(new[]{-1,1}.Select(sign=>c.Box("d2.phase."+sign,"D2_PhaseSignal_"+sign,d2,new Vector3(sign*1.4f,3.12f,34.72f),new Vector3(.3f,.15f,.15f),lamp,false).GetComponent<Renderer>()).ToArray());
            var connector=c.Component<IntegrationConnectorCondition>(d3);connector.Configure(body,receiver,a,b,slots);
            SandboxWorldShiftBuilder.Station(c,d3,"D3",connector,new Vector3(5.2f,.7f,39.1f),new[]{sourceD3},false,world,standard,look,metal,lamp);
        }
    }
}
