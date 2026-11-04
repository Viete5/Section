using UnityEngine;
namespace Section.Sandbox
{
    public sealed class SpatialPlatformCondition : StationCondition
    {
        [SerializeField] WorldSliceState world;
        [SerializeField] PlayerSupportTracker support;
        [SerializeField] GeometrySliceView first,second;
        [SerializeField] TestStation station;
        bool touchedFirst,touchedSecond;
        float nextRetry;
        public int FallCount { get; private set; }
        public bool TouchedFirst=>touchedFirst;
        public bool TouchedSecond=>touchedSecond;
        public override bool HasContact=>touchedFirst;
        public override bool IsSatisfied=>touchedSecond && support.Current!=null && world.Player.transform.position.z>=18 &&
            world.Player.transform.position.x>=2.5f && world.Player.transform.position.x<=6 && world.Player.transform.position.y>-.1f;
        public override string Reading=>IsSatisfied?"FAR BANK / LIVE OK":touchedSecond?"P2 / EXIT TO FAR BANK":touchedFirst?"P1 / SHIFT DURING JUMP":"P1 → SHIFT + JUMP → P2";
        public void Configure(WorldSliceState state,PlayerSupportTracker tracker,GeometrySliceView p1,GeometrySliceView p2,TestStation owner)
        {world=state;support=tracker;first=p1;second=p2;station=owner;}
        void FixedUpdate()
        {
            var feet=world.Player.transform.position;
            bool atPit=feet.x>2.4f && feet.x<6.1f && feet.z>12.8f && feet.z<18.2f;
            if(atPit && feet.y < -1.9f && Time.time>=nextRetry)
            {
                nextRetry=Time.time+.5f;FallCount++;station.MarkFailed();
                if(!station.TryReset() && station.GetComponent<GeometryResetHandler>().TryReturnToBankKeepingW())
                {ResetCondition();station.ReportSafeReturn();}
                return;
            }
            var contact=support.Current;
            if(contact==null)return;
            if(contact.GetComponent<GeometrySliceView>()==first)touchedFirst=true;
            if(touchedFirst && contact.GetComponent<GeometrySliceView>()==second)touchedSecond=true;
            // Leaving the pit by its ordinary side bypass ends a crossing attempt, not physical movement.
            if(touchedFirst && (feet.x<2.5f || feet.x>6) && feet.z>13 && feet.z<18)
            {touchedFirst=touchedSecond=false;}
        }
        public override void ResetCondition(){touchedFirst=touchedSecond=false;nextRetry=Time.time+.5f;}
    }
}
