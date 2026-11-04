using System;
using Section.Geometry4D;
using UnityEngine;
namespace Section.Sandbox
{
    // M3 inspection facade; M4 queries delegate to the authoritative player/world coordinate.
    public sealed class GeometryDiagnostics : MonoBehaviour
    {
        [SerializeField] GeometrySliceView[] views=Array.Empty<GeometrySliceView>();
        [SerializeField] double testW;
        [SerializeField] WorldSliceState world;
        public GeometrySliceView[] Views=>views;
        public double TestW=>Application.isPlaying && world!=null?world.W:testW;
        public void BindWorld(WorldSliceState owner)=>world=owner;
        public string ConfigurationSignature=>testW.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+
            string.Join(",",Array.ConvertAll(views,v=>v==null?"null":v.Id));
        public void Configure(GeometrySliceView[] sources){views=sources;SetTestW(0);}
        public void SetTestW(double value)
        {
            Numeric.RequireFinite(value,nameof(value));
            if(Application.isPlaying && world!=null){world.TrySetW(value);return;}
            foreach(var view in views)if(view==null)throw new MissingReferenceException("Geometry diagnostic source is missing.");
            testW=value;foreach(var view in views)view.SetSlice(value);
        }
        public void ResetAll()
        {
            if(Application.isPlaying && world!=null)
            {if(!world.TryRestore(views,true,null,out string reason))throw new InvalidOperationException(reason);return;}
            foreach(var view in views)view.ResetTransform();SetTestW(0);
        }
        void Start()=>SetTestW(testW);
    }
}
