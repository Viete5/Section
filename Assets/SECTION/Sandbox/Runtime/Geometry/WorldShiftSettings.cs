using UnityEngine;
namespace Section.Sandbox
{
    [CreateAssetMenu(menuName="SECTION/World Shift Settings")]
    public sealed class WorldShiftSettings : ScriptableObject
    {
        public double minimumW=-2.5,maximumW=2.5;
        [Min(.01f)] public double unitsPerSecond=1;
        [Min(.01f)] public double wheelStep=.1;
        public void Validate()
        {if(!(minimumW<maximumW && unitsPerSecond>0 && wheelStep>0) ||
            !Section.Geometry4D.Numeric.IsFinite(minimumW+maximumW+unitsPerSecond+wheelStep))throw new System.InvalidOperationException("Invalid World Shift configuration.");}
    }
}
