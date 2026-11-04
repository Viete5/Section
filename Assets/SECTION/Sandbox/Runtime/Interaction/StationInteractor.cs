using UnityEngine;
namespace Section.Sandbox
{
    [DefaultExecutionOrder(-40)]
    public sealed class StationInteractor : MonoBehaviour
    {
        [SerializeField] SandboxPlayerInput input;
        [SerializeField] StandardManipulator raySource;
        float until;
        public string Feedback { get; private set; }="";
        public void Configure(SandboxPlayerInput source,StandardManipulator ray){input=source;raySource=ray;}
        public StationResetButton Target
        {get{return raySource.TryGetViewHit(out var hit)?hit.collider.GetComponentInParent<StationResetButton>():null;}}
        void Update()
        {
            if(!input.HasControl || !input.UsePressed)return;
            var button=Target;if(button==null)return;
            bool done=button.IsBegin?button.Station.TryBegin():button.Station.TryReset();
            Feedback=done?(button.IsBegin?"Испытание начато":"Испытание восстановлено"):button.Station.LastResetMessage;
            until=Time.unscaledTime+2.5f;
        }
        public string GetPrompt()
        {
            if(Time.unscaledTime<until)return Feedback;
            var target=Target;return target==null?"":"F — "+(target.IsBegin?"Begin ":"локальный сброс ")+target.Station.Id;
        }
    }
}
