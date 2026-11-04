using UnityEngine;

namespace Section.Sandbox
{
    // Authoring ownership, not gameplay state. Unknown objects are never removed by the builder.
    [DisallowMultipleComponent]
    public sealed class SandboxGeneratedObject : MonoBehaviour
    {
        [SerializeField] string key;
        [SerializeField, HideInInspector] string baseline;
        public string Key => key;
        public string Baseline => baseline;
        public void SetKey(string value) => key = value;
        public void RecordBaseline(string value) => baseline = value;
    }
}
