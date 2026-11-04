using UnityEngine;
namespace Section.Sandbox
{
    public sealed class StationResetButton : MonoBehaviour
    {
        [SerializeField] TestStation station;
        [SerializeField] bool begin;
        public TestStation Station => station;
        public bool IsBegin=>begin;
        public void Configure(TestStation owner,bool start=false){station=owner;begin=start;}
    }
}
