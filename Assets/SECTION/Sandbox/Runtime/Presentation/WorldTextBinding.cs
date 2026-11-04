using UnityEngine;
namespace Section.Sandbox
{
    [RequireComponent(typeof(TextMesh),typeof(MeshRenderer))]
    public sealed class WorldTextBinding : MonoBehaviour
    {
        TextMesh label;Material material;
        void Awake()
        {
            label=GetComponent<TextMesh>();material=new Material(GetComponent<MeshRenderer>().sharedMaterial);
            GetComponent<MeshRenderer>().sharedMaterial=material;Refresh(label.font);Font.textureRebuilt+=Refresh;
        }
        void Refresh(Font font){if(font==label.font && material!=null)material.mainTexture=font.material.mainTexture;}
        void OnDestroy(){Font.textureRebuilt-=Refresh;if(material!=null)Destroy(material);}
    }
}
