using Section.Geometry4D;
using UnityEngine;
namespace Section.Sandbox
{
    public sealed class SlicePresenceSensor : MonoBehaviour
    {
        [SerializeField] GeometrySliceView source;
        [SerializeField] Transform probe;
        [SerializeField] double minimumRadius=.4;
        MaterialPropertyBlock properties;
        Renderer indicator;
        public GeometrySliceView Source=>source;
        public bool IsActive
        {
            get
            {
                if(source.CurrentSlice==null || source.CurrentSlice.Kind!=SliceKind.Volume || source.CurrentSlice.AnalyticRadius+1e-7<minimumRadius)return false;
                var collider=source.GetComponent<SliceColliderView>().ActiveCollider;
                return collider!=null && (collider.ClosestPoint(probe.position)-probe.position).sqrMagnitude<=1e-10f;
            }
        }
        public void Configure(GeometrySliceView view,Transform point){source=view;probe=point;}
        void Awake(){properties=new MaterialPropertyBlock();indicator=GetComponent<Renderer>();}
        void LateUpdate()
        {if(indicator==null)return;properties.SetColor("_BaseColor",IsActive?new Color(.15f,1,.4f):new Color(1,.45f,.15f));indicator.SetPropertyBlock(properties);}
    }
}
