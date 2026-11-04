using UnityEngine;
namespace Section.Sandbox
{
    [DefaultExecutionOrder(-175)]
    public sealed class EquipmentSlots : MonoBehaviour
    {
        [SerializeField] SandboxPlayerInput input;
        [SerializeField] StandardManipulator standard;
        [SerializeField] WorldShiftDevice shift;
        [SerializeField] DimensionManipulator dimension;
        public void SetDimension(DimensionManipulator device)=>dimension=device;
        public int SelectedSlot { get; private set; }=1;
        public void Configure(SandboxPlayerInput source,StandardManipulator first,WorldShiftDevice third)
        {input=source;standard=first;shift=third;}
        void Start()=>Select(1);
        void Update(){if(input.SelectStandard)Select(1);else if(input.SelectDimension)Select(2);else if(input.SelectWorldShift)Select(3);}
        public void Select(int slot)
        {
            if(slot!=1 && slot!=3 && (slot!=2 || dimension==null))return;
            SelectedSlot=slot;
            if(dimension!=null){if(slot==1)dimension.Release("4D-захват отпущен при выборе прибора 1");dimension.SetEquipped(slot==2);}
            standard.SetEquipped(slot==1);shift.SetEquipped(slot==3);
        }
    }
}
