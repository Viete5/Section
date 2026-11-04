using System;
using UnityEngine;
namespace Section.Sandbox
{
    [DefaultExecutionOrder(-110)]
    public sealed class WorldShiftDevice : MonoBehaviour
    {
        [SerializeField] SandboxPlayerInput input;
        [SerializeField] WorldSliceState world;
        [SerializeField] GameObject model;
        [SerializeField] Renderer lamp;
        MaterialPropertyBlock properties;
        double pendingWheel;
        public bool IsEquipped { get; private set; }
        public WorldSliceState World=>world;
        public GameObject EquippedModel=>model;
        public void Configure(SandboxPlayerInput source,WorldSliceState state,GameObject representation,Renderer status)
        {input=source;world=state;model=representation;lamp=status;}
        void Awake()=>properties=new MaterialPropertyBlock();
        public void SetEquipped(bool selected){IsEquipped=selected;model.SetActive(selected);pendingWheel=0;}
        void Update()
        {
            if(!IsEquipped || !input.HasControl)return;
            pendingWheel+=input.ScrollNotches*world.Settings.wheelStep;
            double step=world.Settings.unitsPerSecond*Time.deltaTime;
            double wheel=Math.Sign(pendingWheel)*Math.Min(Math.Abs(pendingWheel),step);
            pendingWheel-=wheel;
            double delta=input.ShiftAxis!=0?input.ShiftAxis*step:wheel;
            if(Math.Abs(delta)>1e-12 && !world.TrySetW(world.W+delta))pendingWheel=0;
        }
        void LateUpdate()
        {
            properties.SetColor("_BaseColor",world.LastBlockedReason.Length>0?new Color(1,.35f,.1f):new Color(.2f,.9f,.7f));
            lamp.SetPropertyBlock(properties);
        }
        public string GetPrompt()=>"3 — World Shift   •   Q / E — W− / W+   •   Колесо — шаг W\nW = "+world.W.ToString("+0.000;-0.000;0.000")+
            "   |   "+world.Settings.unitsPerSecond.ToString("0.##")+" W/с"+(world.LastBlockedReason.Length>0?"   |   "+world.LastBlockedReason:"");
    }
}
