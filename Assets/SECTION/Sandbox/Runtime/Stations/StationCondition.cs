using UnityEngine;
namespace Section.Sandbox
{
    public abstract class StationCondition : MonoBehaviour
    {
        public abstract bool IsSatisfied { get; }
        public abstract bool HasContact { get; }
        public abstract string Reading { get; }
        public abstract void ResetCondition();
        public virtual bool TryBegin(out string reason){reason="Для станции нет отдельного запуска";return false;}
    }
}
