using System;
using UnityEngine;
namespace Section.Sandbox
{
    public sealed class SpatialGateCondition : StationCondition
    {
        [SerializeField] WorldSliceState world;
        bool entered,crossed;
        public void Configure(WorldSliceState state)=>world=state;
        public override bool HasContact=>entered;
        public override bool IsSatisfied=>crossed && world.Player.transform.position.z>34.65f && Math.Abs(world.W)<.05;
        public override string Reading=>crossed?"RETURN TO W = 0 BEYOND GATE":"SHIFT W → CROSS GATE → RETURN W";
        void FixedUpdate()
        {
            var p=world.Player.transform.position;
            if(p.z<31.7f)entered=true;
            if(entered && p.z>33.4f && Math.Abs(p.x)<.9f && Math.Abs(world.W)>1.55)crossed=true;
        }
        public override void ResetCondition(){entered=crossed=false;}
    }
}
