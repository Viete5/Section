using System;
using Section.Geometry4D;
using UnityEngine;
namespace Section.Sandbox
{
    public sealed class IntegrationConnectorCondition : StationCondition
    {
        [SerializeField] FourDimensionalBody body;
        [SerializeField] BoxCollider receiver;
        [SerializeField] Transform first,second;
        [SerializeField] EquipmentSlots equipment;
        float stable;
        public void Configure(FourDimensionalBody target,BoxCollider support,Transform a,Transform b,EquipmentSlots slots){body=target;receiver=support;first=a;second=b;equipment=slots;}
        public bool Contact(Vector3 point)
        {
            var parts=CompoundBox4D.Parts(body.Source.State,body.Source.World.W);
            for(int p=0;p<parts.Length;p++)
            {
                if(parts[p].Kind!=SliceKind.Volume)continue;
                var pos=CompoundBox4D.Pose(body.Source.State,p).Position;var vertices=new Vector4D[parts[p].Vertices.Count];
                for(int i=0;i<vertices.Length;i++){var v=parts[p].Vertices[i];vertices[i]=new Vector4D(v.X+pos.X,v.Y+pos.Y,v.Z+pos.Z,0);}
                if(ConvexDistance4D.Query(new HullShape4D(vertices),new SphereShape4D(new Vector4D(point.x,point.y,point.z,0),0)).Distance<=.08)return true;
            }return false;
        }
        public bool FirstContact=>Contact(first.position);
        public bool SecondContact=>Contact(second.position);
        bool PhysicalConnection=>!body.IsHeld && body.SupportingCollider==receiver && body.IsStable && FirstContact && SecondContact;
        public override bool HasContact=>FirstContact || SecondContact;
        public override bool IsSatisfied=>PhysicalConnection && stable>=.2f;
        public override string Reading=>"CONTACT A "+FirstContact+" / B "+SecondContact+"\n"+(IsSatisfied?"ALL SYSTEMS OPERATIONAL":body.IsHeld?"RELEASE ON RECEIVER → DEVICE 3 → SHIFT":equipment.SelectedSlot!=3?"SELECT DEVICE 3 / WORLD SHIFT":"PLACE / RELEASE / WORLD SHIFT");
        void FixedUpdate()=>stable=PhysicalConnection?stable+Time.fixedDeltaTime:0;
        public override void ResetCondition()=>stable=0;
    }
}
