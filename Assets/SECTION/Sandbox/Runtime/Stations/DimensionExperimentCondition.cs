using UnityEngine;
namespace Section.Sandbox
{
    public sealed class DimensionExperimentCondition : StationCondition
    {
        [SerializeField] FourDimensionalBody body;
        public void Configure(FourDimensionalBody target)=>body=target;
        public override bool IsSatisfied=>body.Source.CurrentSlice!=null;
        public override bool HasContact=>body.IsHeld;
        public override string Reading=>body.Source.CurrentSlice.Kind+" / "+(body.IsHeld?"CAPTURED":"FREE")+"\nW BODY "+body.Source.State.Position.W.ToString("0.000");
        public override void ResetCondition(){}
    }
}
