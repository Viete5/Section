using System;
using Section.Geometry4D;
using UnityEngine;
namespace Section.Sandbox
{
    public sealed class PassagePlacementCondition : StationCondition
    {
        [SerializeField] FourDimensionalBody body;
        [SerializeField] BoxCollider pad;
        public bool TraversedAperture { get; private set; }
        public void Configure(FourDimensionalBody target,BoxCollider receiver){body=target;pad=receiver;}
        void OnEnable(){if(body!=null)body.MotionAccepted+=Moved;}
        void OnDisable(){if(body!=null)body.MotionAccepted-=Moved;}
        void Moved(BoxMotion4D motion,double fraction)
        {
            if(motion.Translation.Z<=0 || motion.Start.Position.Z>25 || motion.At(fraction).Position.Z<25)return;
            double t=(25-motion.Start.Position.Z)/motion.Translation.Z;var crossing=motion.At(t);
            // Exchange Z and W by a proper ZW quarter rotation: slice at new W=old Z=25.
            // Its XY bounds are the true intersection of the FULL body with the wall plane, over all W.
            var permutation=new Transform4D(default,Matrix4D.Rotation(RotationPlane.ZW,Math.PI*.5));
            var transformed=permutation.Compose(crossing);
            var section=ConvexPrism4D.Slice(body.Half,default,transformed,25);
            if(section.Vertices.Count==0)return;
            TraversedAperture=section.Minimum.X+crossing.Position.X>=4-.002 && section.Maximum.X+crossing.Position.X<=5+.002 &&
                section.Minimum.Y+crossing.Position.Y>=.75-.002 && section.Maximum.Y+crossing.Position.Y<=1.45+.002;
        }
        public override bool HasContact=>body.SupportingCollider==pad;
        public override bool IsSatisfied=>TraversedAperture && HasContact && body.IsStable &&
            Math.Abs(body.Source.State.Position.X-4.5)<.5 && Math.Abs(body.Source.State.Position.Z-27)<.5;
        public override string Reading=>(TraversedAperture?"APERTURE CROSSED":"USE THE REAL WINDOW")+"\n"+(HasContact?"PAD CONTACT":"PLACE ON PAD");
        public override void ResetCondition()=>TraversedAperture=false;
    }
}
