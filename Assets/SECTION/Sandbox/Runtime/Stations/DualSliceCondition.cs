using UnityEngine;
namespace Section.Sandbox
{
    public sealed class DualSliceCondition : StationCondition
    {
        [SerializeField] WorldSliceState world;
        [SerializeField] SlicePresenceSensor first,second;
        float stable;
        public bool Armed { get; private set; }
        public bool LiveBoth=>first.IsActive && second.IsActive;
        public override bool HasContact=>first.IsActive || second.IsActive;
        public override bool IsSatisfied=>Armed && LiveBoth && stable>=.2f;
        public override string Reading=>"U "+(first.IsActive?"ON":"OFF")+" / V "+(second.IsActive?"ON":"OFF")+
            "\nW "+world.W.ToString("0.000")+(Armed?" / RUNNING":" / BEGIN [F]");
        public void Configure(WorldSliceState state,SlicePresenceSensor u,SlicePresenceSensor v){world=state;first=u;second=v;}
        public override bool TryBegin(out string reason)
        {
            if(!world.Player.Controller.isGrounded || world.Player.Input.Move.sqrMagnitude>.001f || Mathf.Abs(world.Player.VerticalVelocity)>2.1f)
            {reason="Begin B3: остановитесь на безопасном полу";return false;}
            if(!world.TrySetWExactly(-.8,out reason))return false;
            stable=0;Armed=true;return true;
        }
        void FixedUpdate()=>stable=Armed && LiveBoth?stable+Time.fixedDeltaTime:0;
        public override void ResetCondition(){Armed=false;stable=0;}
    }
}
