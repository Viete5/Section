using System;
using Section.Geometry4D;
using UnityEngine;
namespace Section.Sandbox
{
    [DefaultExecutionOrder(-45)]
    public sealed class DimensionManipulator : MonoBehaviour
    {
        [SerializeField] DimensionManipulatorSettings settings;
        [SerializeField] SandboxPlayerInput input;
        [SerializeField] Transform view;
        [SerializeField] GameObject model;
        [SerializeField] Renderer lamp;
        [SerializeField] StandardManipulator raySource;
        FourDimensionalBody held;
        Vector3 directionLocal;
        double pendingAngle,pendingW;
        bool fourDimensionalGroup;
        int planeIndex;
        float distance;
        MaterialPropertyBlock properties;
        public FourDimensionalBody Held=>held;
        public bool IsEquipped { get; private set; }
        public bool IsRotating=>IsEquipped && held!=null && input.HasControl && input.RotateHeld;
        public RotationPlane Plane=>(RotationPlane)(planeIndex+(fourDimensionalGroup?3:0));
        public float HoldDistance=>distance;
        public DimensionManipulatorSettings Settings=>settings;
        public string Feedback { get; private set; }="Прибор 2 готов";
        public void Configure(DimensionManipulatorSettings config,SandboxPlayerInput source,Transform camera,GameObject representation,Renderer status,StandardManipulator ray)
        {settings=config;input=source;view=camera;model=representation;lamp=status;raySource=ray;}
        void Awake()=>properties=new MaterialPropertyBlock();
        public void SetEquipped(bool selected)
        {IsEquipped=selected;model.SetActive(selected);if(!selected){pendingAngle=pendingW=0;}}
        public bool TryCapture()
        {
            if(held!=null || !raySource.TryGetViewHit(settings.acquisitionRange,out var hit))return false;
            var body=hit.collider.GetComponentInParent<FourDimensionalBody>();
            if(body==null || !body.TryClaim(this)){Feedback="Нет доступного 4D-предмета";return false;}
            held=body;var offset=SliceMeshWriter.ToVector3(body.Source.State.Position.XYZ)-view.position;
            distance=Mathf.Clamp(offset.magnitude,settings.minimumDistance,settings.maximumDistance);
            directionLocal=view.InverseTransformDirection(offset.normalized);pendingAngle=pendingW=0;Feedback="4D-захват установлен";return true;
        }
        void Update()
        {
            if(!IsEquipped || !input.HasControl)return;
            if(input.InteractPressed){if(held!=null)Release();else TryCapture();}
            if(input.NextPlane){planeIndex=(planeIndex+1)%3;pendingAngle=0;}
            if(input.TogglePlaneGroup){fourDimensionalGroup=!fourDimensionalGroup;pendingAngle=0;}
            if(held==null)return;
            if(input.ModifyObjectW)pendingW+=input.ScrollNotches*settings.wPerNotch;
            else distance=Mathf.Clamp(distance+input.ScrollNotches*settings.distancePerNotch,settings.minimumDistance,settings.maximumDistance);
            if(input.RotateHeld)pendingAngle+=input.Look.x*settings.rotationDegreesPerPixel*Math.PI/180;
            var source=held.Source;Vector3 target=view.position+view.TransformDirection(directionLocal)*distance;
            Vector3 delta=Vector3.ClampMagnitude(target-SliceMeshWriter.ToVector3(source.State.Position.XYZ),settings.translationSpeed*Time.deltaTime);
            var move=held.Physics.Move(held,new BoxMotion4D(source.State,held.Half,new Vector4D(delta.x,delta.y,delta.z,0)));
            Feedback=move.Reason;
            if(move.Fraction<1)
            {
                // Preserve admissible tangential motion. Each leg has its own continuous collision proof.
                var remaining=delta*(float)(1-move.Fraction);
                foreach(var leg in new[]{new Vector4D(remaining.x,0,0,0),new Vector4D(0,remaining.y,0,0),new Vector4D(0,0,remaining.z,0)})
                    if(leg.Length>1e-8)held.Physics.Move(held,new BoxMotion4D(source.State,held.Half,leg));
            }
            double wStep=Math.Sign(pendingW)*Math.Min(Math.Abs(pendingW),settings.wUnitsPerSecond*Time.deltaTime);
            if(Math.Abs(wStep)>1e-10)
            {
                var result=held.Physics.Move(held,new BoxMotion4D(source.State,held.Half,new Vector4D(0,0,0,wStep)));
                pendingW-=wStep*result.Fraction;if(result.Fraction<1){pendingW=0;Feedback=result.Reason;}
            }
            double turn=Math.Sign(pendingAngle)*Math.Min(Math.Abs(pendingAngle),settings.rotationDegreesPerSecond*Math.PI/180*Time.deltaTime);
            if(Math.Abs(turn)>1e-10)
            {
                var result=held.Physics.Move(held,new BoxMotion4D(source.State,held.Half,default,Plane,turn));
                pendingAngle-=turn*result.Fraction;if(result.Fraction<1){pendingAngle=0;Feedback=result.Reason;}
            }
        }
        public void Release(string reason="4D-предмет отпущен")
        {if(held==null)return;var body=held;held=null;body.Release(this);pendingAngle=pendingW=0;Feedback=reason;}
        public string GetPrompt()
        {
            if(held==null)return "2 — Dimension Manipulator   •   E — захват 4D   •   R / T — плоскость / группа";
            var state=held.Source.State;string visibility=held.Source.CurrentSlice?.Kind==SliceKind.Volume?"ВИДИМ":"НЕВИДИМ — ЗАХВАТ СОХРАНЁН";
            return held.Id+" / "+visibility+" / "+Plane+" / W тела "+state.Position.W.ToString("0.000")+
                "\nE — отпустить · ПКМ + мышь X — вращать · R — плоскость · T — 3D/4D · колесо — дистанция · Shift+колесо — W тела"+
                (Feedback.Length>0?"\n"+Feedback:"");
        }
        void LateUpdate()
        {
            Color color=held==null?new Color(.65f,.5f,1):held.Source.CurrentSlice?.Kind==SliceKind.Volume?new Color(.2f,1,.7f):new Color(1,.7f,.15f);
            if(Feedback.Length>0 && Feedback.Contains("препятствие"))color=new Color(1,.3f,.1f);
            properties.SetColor("_BaseColor",color);lamp.SetPropertyBlock(properties);
        }
        void OnDisable()=>Release("Прибор 2 отключён");
    }
}
