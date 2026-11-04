using UnityEngine;
using UnityEngine.UIElements;
namespace Section.Sandbox
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class InteractionHud : MonoBehaviour
    {
        [SerializeField] StandardManipulator manipulator;
        [SerializeField] WorldShiftDevice shift;
        [SerializeField] StationInteractor interactor;
        [SerializeField] SandboxPlayerInput input;
        [SerializeField] DimensionManipulator dimension;
        public void SetDimension(DimensionManipulator device)=>dimension=device;
        Label hint;
        Label diagnostics;
        bool showDiagnostics;
        public void Configure(StandardManipulator device) => manipulator = device;
        public void ConfigureWorldShift(WorldShiftDevice device,StationInteractor stationInput,SandboxPlayerInput source)
        {shift=device;interactor=stationInput;input=source;}
        void Start()
        {
            if (manipulator == null) throw new MissingReferenceException("Interaction HUD has no device.");
            hint = GetComponent<UIDocument>().rootVisualElement.Q<Label>("hint");
            if (hint == null) throw new MissingReferenceException("Interaction HUD hint element is missing.");
            diagnostics=GetComponent<UIDocument>().rootVisualElement.Q<Label>("diagnostics");
        }
        void Update()
        {
            if(hint==null)return;
            string action=interactor==null?"":interactor.GetPrompt();
            hint.text=dimension!=null && dimension.IsEquipped?dimension.GetPrompt():shift!=null && shift.IsEquipped?shift.GetPrompt():manipulator.GetPrompt();
            if(dimension!=null && !dimension.IsEquipped && dimension.Held!=null)hint.text+="\n4D-захват зафиксирован: "+dimension.Held.Id;
            if(action.Length>0)hint.text=action+"\n"+hint.text;
            if(input!=null && input.ToggleDiagnostics)showDiagnostics=!showDiagnostics;
            if(diagnostics==null || shift==null)return;
            diagnostics.EnableInClassList("hidden",!showDiagnostics);
            if(!showDiagnostics)return;
            var world=shift.World;int colliders=0;foreach(var source in world.Sources)if(source.GetComponent<SliceColliderView>().ActiveCollider!=null)colliders++;
            diagnostics.text="XYZ "+world.Player.transform.position.ToString("F2")+"   W "+world.W.ToString("F4")+
                "   SLOT "+(dimension!=null && dimension.IsEquipped?2:shift.IsEquipped?3:1)+"\n4D SOURCES "+world.Sources.Length+" / COLLIDERS "+colliders+
                "   SWEEP "+world.LastSweepMilliseconds.ToString("F3")+" ms\n"+world.LastBlockedReason+
                "\nLAST RESET "+world.LastResetTime.ToString("F2")+" s   FRAME "+Time.unscaledDeltaTime.ToString("F4")+" s";
        }
    }
}
