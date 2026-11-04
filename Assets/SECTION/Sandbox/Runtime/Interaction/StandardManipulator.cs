using UnityEngine;

namespace Section.Sandbox
{
    [DefaultExecutionOrder(-50)]
    public sealed class StandardManipulator : MonoBehaviour
    {
        [SerializeField] ManipulatorSettings settings;
        [SerializeField] SandboxPlayerInput input;
        [SerializeField] Transform view;
        [SerializeField] GameObject equippedModel;
        [SerializeField] Renderer statusLamp;
        readonly RaycastHit[] hits = new RaycastHit[48];
        readonly Collider[] overlaps = new Collider[48];
        MaterialPropertyBlock lampProperties;
        CharacterController playerCollider;
        PhysicalItem held;
        Vector3 grabLocal;
        Quaternion desiredRotation;
        float holdDistance;
        float blockedTime;
        float messageUntil;
        public PhysicalItem Held => held;
        public ManipulatorSettings Settings => settings;
        public bool IsEquipped { get; private set; } = true;
        public void SetEquipped(bool selected)
        {if(!selected)Release("Захват отпущен при смене прибора");IsEquipped=selected;equippedModel.SetActive(selected);}
        public string Feedback { get; private set; } = "Прибор готов";
        public float HoldDistance => holdDistance;
        public Vector3 GrabPointLocal => grabLocal;
        public float LastPositionError { get; private set; }
        public string LastOccluder { get; private set; } = "";
        public bool IsRotating => IsEquipped && held != null && input.HasControl && input.RotateHeld;
        public bool TryGetViewHit(out RaycastHit hit) => TryGetViewHit(settings.acquisitionRange,out hit);
        public bool TryGetViewHit(float range,out RaycastHit hit)=>FirstExternalHit(view.position,view.forward,range,out hit);
        bool FirstExternalHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            hit = default;
            if (count == hits.Length) return false;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                if (hits[i].collider != playerCollider && hits[i].distance < nearest)
                { nearest = hits[i].distance; hit = hits[i]; }
            return hit.collider != null;
        }
        public void Configure(ManipulatorSettings config, SandboxPlayerInput source, Transform cameraView, GameObject model, Renderer lamp)
        { settings = config; input = source; view = cameraView; equippedModel = model; statusLamp = lamp; }
        void Awake()
        {
            if (settings == null || input == null || view == null || equippedModel == null || statusLamp == null)
                throw new MissingReferenceException("StandardManipulator is not configured.");
            lampProperties = new MaterialPropertyBlock();
            playerCollider = input.GetComponent<CharacterController>();
        }
        void Update()
        {
            if (input.SelectStandard) { IsEquipped = true; equippedModel.SetActive(true); }
            if (!IsEquipped || !input.HasControl) return;
            if (input.InteractPressed)
            {
                if (held != null) Release("Предмет отпущен");
                else TryCaptureFromView();
            }
            if (held != null)
            {
                holdDistance = Mathf.Clamp(holdDistance + input.ScrollNotches * settings.distancePerNotch,
                    settings.minimumDistance, settings.maximumDistance);
                if (input.RotateHeld)
                    desiredRotation = Quaternion.AngleAxis(input.Look.x * settings.rotationSensitivity, Vector3.up) *
                        Quaternion.AngleAxis(-input.Look.y * settings.rotationSensitivity, view.right) * desiredRotation;
            }
        }
        public bool TryCaptureFromView()
        {
            if (!IsEquipped || held != null || !TryGetViewHit(out var hit)) { SetFeedback("Нет доступного предмета"); return false; }
            var item = hit.collider.GetComponentInParent<PhysicalItem>();
            if (item == null || !item.CanManipulate || item.Body.mass > settings.maximumMass || !item.TryClaim(this))
            { SetFeedback("Захват недоступен"); return false; }
            held = item; grabLocal = item.transform.InverseTransformPoint(hit.point);
            desiredRotation = item.Body.rotation;
            holdDistance = Mathf.Clamp(hit.distance, settings.minimumDistance, settings.maximumDistance);
            blockedTime = 0; SetFeedback("Захват установлен"); return true;
        }
        void FixedUpdate()
        {
            if (held == null) return;
            if (!held.CanManipulate || held.Body.isKinematic || !held.Body.detectCollisions || held.Body.mass > settings.maximumMass)
            { Release("Захват потерян: предмет недоступен"); return; }
            var body = held.Body;
            Vector3 wanted = view.position + view.forward * holdDistance - desiredRotation * Vector3.Scale(grabLocal, held.transform.lossyScale);
            float error = Vector3.Distance(body.position, wanted);
            bool occluded = IsFullyOccluded(out string occluder);
            LastPositionError = error; LastOccluder = occluder;
            blockedTime = error > settings.maximumError || occluded ? blockedTime + Time.fixedDeltaTime : 0;
            if (blockedTime > settings.obstructionGrace || Vector3.Distance(view.position, body.position) > settings.maximumDistance + 2)
            { Release("Захват отпущен: препятствие или превышена дальность"); return; }
            Vector3 safe = LimitTranslation(wanted);
            Vector3 force = (safe - body.position) * settings.positionSpring - body.linearVelocity * settings.positionDamping - Physics.gravity;
            body.AddForce(Vector3.ClampMagnitude(force * body.mass, settings.maximumForce), ForceMode.Force);
            Quaternion target = Quaternion.RotateTowards(body.rotation, desiredRotation, 120 * Time.fixedDeltaTime);
            target = RotationFits(target) ? desiredRotation : body.rotation;
            Quaternion difference = target * Quaternion.Inverse(body.rotation);
            difference.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180) angle -= 360;
            Vector3 angular = axis * (angle * Mathf.Deg2Rad * settings.angularSpring) - body.angularVelocity * settings.angularDamping;
            body.AddTorque(Vector3.ClampMagnitude(angular, settings.maximumAngularAcceleration), ForceMode.Acceleration);
        }
        bool IsFullyOccluded(out string occluder)
        {
            occluder = "";
            bool Blocked(Vector3 point)
            {
                Vector3 ray = point - view.position;
                return FirstExternalHit(view.position, ray.normalized, ray.magnitude, out var hit) && hit.collider != held.Shape;
            }
            if (!Blocked(held.Body.position)) return false;
            // A socket roof may hide the centre while its front face remains visible.
            // Acquisition still uses only the crosshair; these samples only preserve an existing claim.
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 local = held.Shape.center + Vector3.Scale(held.Shape.size * .49f, new Vector3(x,y,z));
                        if (!Blocked(held.Body.position + held.Body.rotation * Vector3.Scale(local,held.transform.lossyScale))) return false;
                    }
            Vector3 direction=held.Body.position-view.position;
            if (FirstExternalHit(view.position,direction.normalized,direction.magnitude,out var obstacle)) occluder=obstacle.collider.name;
            return true;
        }
        Vector3 LimitTranslation(Vector3 wanted)
        {
            Vector3 position = held.Body.position;
            Vector3 remaining = wanted - position;
            // Preserve tangential movement at a contact instead of pinning the object to the first hit.
            // The Rigidbody still moves only through forces; the solver and CCD resolve actual contacts.
            for (int pass = 0; pass < 2; pass++)
            {
                float distance = remaining.magnitude;
                if (distance < .0001f) break;
                Vector3 direction = remaining / distance;
                int count = Physics.BoxCastNonAlloc(position, HalfExtents(), direction, hits,
                    held.Body.rotation, distance + .015f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count == hits.Length) return position;
                float allowed = distance;
                Vector3 normal = Vector3.zero;
                for (int i = 0; i < count; i++)
                    if (hits[i].collider != held.Shape && Vector3.Dot(direction, hits[i].normal) < -.001f && hits[i].distance - .015f < allowed)
                    { allowed = Mathf.Max(0, hits[i].distance - .015f); normal = hits[i].normal; }
                position += direction * allowed;
                if (normal == Vector3.zero) break;
                remaining = Vector3.ProjectOnPlane(wanted - position, normal);
            }
            return position;
        }
        Vector3 HalfExtents() => Vector3.Scale(held.Shape.size * .5f, held.transform.lossyScale) - Vector3.one * .002f;
        bool RotationFits(Quaternion rotation)
        {
            int count = Physics.OverlapBoxNonAlloc(held.Body.position, HalfExtents(), overlaps, rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                if (overlaps[i] == held.Shape) continue;
                PhysicsContactUtility.GetPose(overlaps[i],out var otherPosition,out var otherRotation);
                if (Physics.ComputePenetration(held.Shape, held.Body.position, rotation,
                    overlaps[i], otherPosition, otherRotation, out _, out float depth) && depth > .004f)
                    return false;
            }
            return true;
        }
        public void Release(string reason = "Предмет отпущен")
        {
            if (held == null) return;
            var item = held; held = null;
            item.ClearClaim(this);
            // Release removes the servo's motion; there is no throwing impulse or throw input.
            if (!item.Body.isKinematic)
            { item.Body.linearVelocity = Vector3.zero; item.Body.angularVelocity = Vector3.zero; item.Body.WakeUp(); }
            blockedTime = 0; SetFeedback(reason);
        }
        void SetFeedback(string message) { Feedback = message; messageUntil = Time.unscaledTime + 2.5f; }
        public string GetPrompt()
        {
            if (!input.HasControl) return "ЛКМ — вернуться к управлению";
            if (Time.unscaledTime < messageUntil) return Feedback;
            if (held != null) return "E — отпустить   •   ПКМ + мышь — вращать   •   Колесо — расстояние";
            if (TryGetViewHit(out var hit))
            {
                var button = hit.collider.GetComponentInParent<StationResetButton>();
                if (button != null) return "F — локальный сброс " + button.Station.Id;
                if (hit.collider.GetComponentInParent<PhysicalItem>() != null) return "E — захватить предмет";
            }
            return "1 — обычный манипулятор   •   E — захват";
        }
        void LateUpdate()
        {
            Color color = held != null ? new Color(.2f, 1, .65f) : new Color(.25f, .75f, 1);
            if (blockedTime > 0) color = new Color(1, .4f, .1f);
            lampProperties.SetColor("_BaseColor", color); statusLamp.SetPropertyBlock(lampProperties);
        }
        void OnDisable() => Release("Прибор отключён");
    }
}
