using UnityEngine;
namespace Section.Sandbox
{
    [CreateAssetMenu(menuName="SECTION/Dimension Manipulator Settings")]
    public sealed class DimensionManipulatorSettings : ScriptableObject
    {
        public float acquisitionRange=2.8f,minimumDistance=1,maximumDistance=3,distancePerNotch=.18f;
        public float translationSpeed=6,rotationDegreesPerSecond=120,rotationDegreesPerPixel=.3f,wUnitsPerSecond=1,wPerNotch=.1f;
    }
}
