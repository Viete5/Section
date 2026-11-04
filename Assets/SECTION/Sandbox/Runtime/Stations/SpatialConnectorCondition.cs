using System;
using Section.Geometry4D;
using UnityEngine;
namespace Section.Sandbox
{
    public sealed class SpatialConnectorCondition : StationCondition
    {
        [SerializeField] FourDimensionalBody body;
        [SerializeField] Transform first,second;
        float stable;
        public void Configure(FourDimensionalBody target,Transform a,Transform b){body=target;first=a;second=b;}
        public bool Contact(Vector3 point)
        {
            var slice=body.Source.CurrentSlice;
            if(slice==null || slice.Kind!=SliceKind.Volume)return false;
            var vertices=new Vector4D[slice.Vertices.Count];var pos=body.Source.State.Position;
            for(int i=0;i<vertices.Length;i++){var v=slice.Vertices[i];vertices[i]=new Vector4D(v.X+pos.X,v.Y+pos.Y,v.Z+pos.Z,0);}
            var probe=new SphereShape4D(new Vector4D(point.x,point.y,point.z,0),0);
            return ConvexDistance4D.Query(new HullShape4D(vertices),probe).Distance<=.1;
        }
        public bool FirstContact=>Contact(first.position);
        public bool SecondContact=>Contact(second.position);
        public bool Aligned
        {get{var axis=body.Source.State.Rotation*new Vector4D(0,0,0,1);return axis.XYZ.Length>1e-12 && Math.Abs(axis.X)/axis.XYZ.Length>=Math.Cos(10*Math.PI/180);}}
        public override bool HasContact=>FirstContact || SecondContact;
        public override bool IsSatisfied=>FirstContact && SecondContact && Aligned && stable>=.2f;
        public override string Reading=>"CONTACT "+(FirstContact?"A ON":"A OFF")+" / "+(SecondContact?"B ON":"B OFF")+"\nSLICE "+body.Source.CurrentSlice.Kind;
        void FixedUpdate()=>stable=FirstContact && SecondContact && Aligned?stable+Time.fixedDeltaTime:0;
        public override void ResetCondition()=>stable=0;
    }
}
