using UnityEngine;
namespace Section.Sandbox
{
    public sealed class GeometryValidationCondition : StationCondition
    {
        [SerializeField] GeometrySliceView view;
        [SerializeField] WorldSliceState world;
        public void Configure(GeometrySliceView source,WorldSliceState state){view=source;world=state;}
        public override bool IsSatisfied=>view.CurrentSlice!=null && view.SliceW==world.W &&
            (view.RenderStatus==SliceRenderStatus.Ready)==(view.GetComponent<SliceColliderView>().ActiveCollider!=null);
        public override bool HasContact=>view.CurrentSlice!=null && view.CurrentSlice.Kind==Section.Geometry4D.SliceKind.Volume;
        public override string Reading=>"W "+world.W.ToString("0.000")+"  R "+view.CurrentSlice.AnalyticRadius.ToString("0.000000")+
            "\nMESH R "+(view.GeneratedMesh.vertexCount>0?view.GeneratedMesh.bounds.extents.x:0).ToString("0.000000")+
            " / "+view.CurrentSlice.Kind;
        public override void ResetCondition(){}
    }
}
