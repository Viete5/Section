using UnityEngine;
namespace Section.Sandbox
{
    public enum StationState { Ready, Active, Passed, Failed, Resetting }

    [DefaultExecutionOrder(100)]
    public sealed class TestStation : MonoBehaviour
    {
        [SerializeField] string stationId;
        [SerializeField] PhysicalItem[] items;
        [SerializeField] StationCondition condition;
        [SerializeField] Renderer indicator;
        [SerializeField] TextMesh readout;
        [SerializeField] GeometryResetHandler geometryReset;
        readonly Collider[] blockers = new Collider[64];
        MaterialPropertyBlock properties;
        float nextReadout;
        Material textMaterial;
        public string Id => stationId;
        public PhysicalItem[] Items => items;
        public StationCondition Condition => condition;
        public StationState State { get; private set; } = StationState.Ready;
        public string LastResetMessage { get; private set; } = "";
        public bool CurrentSuccess => condition.IsSatisfied;
        public void Configure(string id, PhysicalItem[] objects, StationCondition sensor, Renderer lamp, TextMesh text)
        { stationId = id; items = objects; condition = sensor; indicator = lamp; readout = text; }
        public void SetGeometryReset(GeometryResetHandler handler)=>geometryReset=handler;
        public void MarkFailed(){if(State!=StationState.Passed)State=StationState.Failed;}
        public void ReportSafeReturn(){LastResetMessage+=" / Игрок на берегу; устраните причину и повторите Reset";}
        public bool TryBegin()
        {if(!condition.TryBegin(out string reason)){LastResetMessage=reason;return false;}State=StationState.Active;LastResetMessage="";return true;}
        void Awake()
        {
            if (string.IsNullOrEmpty(stationId) || items == null || condition == null || indicator == null || readout == null)
                throw new MissingReferenceException("Station is not configured: " + name);
            properties = new MaterialPropertyBlock();
            textMaterial = new Material(readout.GetComponent<Renderer>().sharedMaterial);
            textMaterial.mainTexture = readout.font.material.mainTexture;
            readout.GetComponent<Renderer>().sharedMaterial = textMaterial;
            Font.textureRebuilt += UpdateFontTexture;
        }
        void FixedUpdate()
        {
            if (condition.IsSatisfied) State = StationState.Passed;
            else if (State == StationState.Ready && condition.HasContact) State = StationState.Active;
            else if (State == StationState.Ready)
                foreach (var item in items)
                    if (item.IsHeld || Vector3.Distance(item.Body.position, item.InitialPosition) > .04f) { State = StationState.Active; break; }
        }
        void UpdateFontTexture(Font font) { if (font == readout.font && textMaterial != null) textMaterial.mainTexture = font.material.mainTexture; }
        void OnDestroy() { Font.textureRebuilt -= UpdateFontTexture; if (textMaterial != null) Destroy(textMaterial); }
        bool Owns(Collider collider)
        {
            foreach (var item in items) if (collider == item.Shape) return true;
            return false;
        }
        public bool TryReset()
        {
            // Preflight all spawn volumes before changing a claim, sensor, pose, or station state.
            foreach (var item in items)
            {
                Vector3 half = Vector3.Scale(item.Shape.size * .5f, item.transform.lossyScale);
                int count = Physics.OverlapBoxNonAlloc(item.InitialPosition, half, blockers, item.InitialRotation,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore);
                if (count == blockers.Length) { LastResetMessage = Id + ": место восстановления занято"; return false; }
                for (int i = 0; i < count; i++)
                {
                    var other = blockers[i];
                    if (Owns(other)) continue;
                    PhysicsContactUtility.GetPose(other,out var otherPosition,out var otherRotation);
                    if (Physics.ComputePenetration(item.Shape, item.InitialPosition, item.InitialRotation, other,
                        otherPosition, otherRotation, out _, out float depth) && depth > .004f)
                    { LastResetMessage = Id + ": уберите препятствие — " + other.name; return false; }
                }
            }
            if(geometryReset!=null && !geometryReset.TryRestore(out string resetReason))
            {LastResetMessage=Id+": "+resetReason;return false;}
            State = StationState.Resetting;
            foreach (var item in items) item.RestoreInitial();
            Physics.SyncTransforms();
            condition.ResetCondition();
            LastResetMessage = ""; State = StationState.Ready;
            return true;
        }
        void Update()
        {
            Color color = CurrentSuccess ? new Color(.15f, 1, .4f) : LastResetMessage.Length > 0 ? new Color(1, .35f, .1f) : new Color(.9f, .65f, .2f);
            properties.SetColor("_BaseColor", color); indicator.SetPropertyBlock(properties);
            if (Time.unscaledTime < nextReadout) return;
            nextReadout = Time.unscaledTime + .15f;
            readout.text = Id + " RESET [F]\n" + condition.Reading + "\n" +
                (LastResetMessage.Length > 0 ? "RESET BLOCKED" : CurrentSuccess ? "LIVE OK / PASSED" : State == StationState.Passed ? "LIVE OFF / PASSED" : State.ToString());
        }
    }
}
