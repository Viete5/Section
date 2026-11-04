using System;
using System.Collections.Generic;
using Section.Geometry4D;
using UnityEngine;
namespace Section.Sandbox
{
    [DefaultExecutionOrder(-80)]
    public sealed class MovingWallsCondition : StationCondition
    {
        [SerializeField] WorldSliceState world;
        [SerializeField] GeometrySliceView left,right;
        [SerializeField] TestStation station;
        [SerializeField] Renderer[] phaseLamps=Array.Empty<Renderer>();
        MaterialPropertyBlock signal;
        double clock;bool entered,completed,pendingReset;float nextRetry;
        public int FailureCount { get; private set; }
        public double Phase=>clock%5;
        public bool PendingReset=>pendingReset;
        public string BlockReason { get; private set; }="";
        public static double Opening(double phase){phase%=5;return phase<1?.975:phase<2?.975-.6*(phase-1):phase<3?.375:phase<4?.375+.6*(phase-3):.975;}
        public void Configure(WorldSliceState state,GeometrySliceView a,GeometrySliceView b,TestStation owner){world=state;left=a;right=b;station=owner;}
        public void SetPhaseLamps(Renderer[] lamps)=>phaseLamps=lamps;
        void Update()
        {
            signal??=new MaterialPropertyBlock();
            Color color=pendingReset?new Color(1,.15f,.05f):Phase>=2 && Phase<3?new Color(1,.15f,.05f):Phase>=1 && Phase<2 || Phase>=.75 && Phase<1?new Color(1,.65f,.08f):Phase>=3 && Phase<4?new Color(.15f,.7f,1):new Color(.2f,1,.4f);
            signal.SetColor("_BaseColor",color);foreach(var lamp in phaseLamps)lamp.SetPropertyBlock(signal);
        }
        public override bool HasContact=>entered || pendingReset;
        public override bool IsSatisfied=>completed;
        public override string Reading=>pendingReset?"CONTACT — RESET BLOCKED: "+station.LastResetMessage:
            "PHASE "+Phase.ToString("0.00")+" / "+(Phase<1?"OPEN / CLOSING NEXT":Phase<2?"CLOSING":Phase<3?"CLOSED":Phase<4?"OPENING":"OPEN")+"\nFAIL/RETURN "+FailureCount+" "+BlockReason;
        void FixedUpdate()
        {
            if(pendingReset){if(Time.time>=nextRetry){nextRetry=Time.time+.5f;station.TryReset();}return;}
            double dt=Time.fixedDeltaTime;
            // Split at every piecewise-linear phase boundary, including multiple boundaries in a slow frame.
            while(dt>1e-9)
            {
                double next=Math.Floor(clock+1e-8)+1,step=Math.Min(dt,next-clock);double target=clock+step;
                if(!MovePair(Opening(target)))return;clock=target;dt-=step;
            }
            var feet=world.Player.transform.position;
            if(feet.z<34.45f && Math.Abs(feet.x)<.75f)entered=true;
            if(entered && feet.z>34.8f && feet.z<36.5f && Math.Abs(feet.x)>.8f)entered=false;
            if(entered && feet.z>36.85f && Math.Abs(feet.x)<.75f)completed=true;
        }
        bool MovePair(double x)
        {
            var obstacles=new List<ContinuousBoxCollision4D.Obstacle>();
            obstacles.Add(new ContinuousBoxCollision4D.Obstacle{Shape=new SliceCapsuleShape4D(world.PlayerVolume(world.Player.transform.position),world.W),Id="Player"});
            foreach(var item in world.ConventionalItems)if(item!=null)obstacles.Add(new ContinuousBoxCollision4D.Obstacle{Shape=FourDimensionalPhysics.OrdinaryBox(item.Shape),ProjectXYZ=true,Id=item.Id});
            foreach(var source in world.Sources)
            {
                if(source==left || source==right || source.Id=="D1")continue;
                var actor=source.GetComponent<FourDimensionalBody>();if(actor==null)continue;
                for(int p=0;p<actor.PartCount;p++)obstacles.Add(new ContinuousBoxCollision4D.Obstacle{Shape=new BoxShape4D(new Transform4D(source.State.Position+source.State.Rotation*actor.PartCenter(p),source.State.Rotation),actor.PartHalf(p)),Id=actor.Id});
            }
            foreach(var source in new[]{left,right})
            {
                double goal=source==left?-x:x;var motion=new BoxMotion4D(source.State,source.PrismHalfSize,new Vector4D(goal-source.State.Position.X,0,0,0));
                if(ContinuousBoxCollision4D.FreePrefix(motion,obstacles,out var blocker,out _)<1)
                {
                    BlockReason="CONTACT "+blocker.Id;
                    if(blocker.Id=="Player")
                    {FailureCount++;pendingReset=true;station.MarkFailed();nextRetry=Time.time+.5f;station.TryReset();}
                    return false; // Foreign matter stops the physical mechanism; it is never displaced by a reset.
                }
            }
            world.CommitBodyPose(left,new Transform4D(new Vector4D(-x,1.5,35.65,0),Matrix4D.Identity));
            world.CommitBodyPose(right,new Transform4D(new Vector4D(x,1.5,35.65,0),Matrix4D.Identity));return true;
        }
        public override void ResetCondition(){clock=0;entered=completed=pendingReset=false;BlockReason="";}
    }
}
