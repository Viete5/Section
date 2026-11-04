using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Section.Geometry4D;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
namespace Section.Sandbox.Editor
{
    public sealed class GeometryDiagnosticsWindow : EditorWindow
    {
        GeometryDiagnostics diagnostics;
        ObjectField binding;
        PopupField<string> source;
        DoubleField slice,angle,dx,dy,dz,dw;
        EnumField plane;
        VisualElement actions;
        Label stats;
        HelpBox message;
        double nextRefresh;
        [MenuItem("SECTION/Sandbox 01/4D Geometry Diagnostics (M3)")]
        public static void Open(){var window=GetWindow<GeometryDiagnosticsWindow>();window.titleContent=new GUIContent("SECTION · 4D Core");window.minSize=new Vector2(430,650);window.Show();}
        void OnEnable()=>EditorApplication.update+=Refresh;
        void OnDisable()=>EditorApplication.update-=Refresh;
        public void CreateGUI()
        {
            var style=AssetDatabase.LoadAssetAtPath<StyleSheet>(SandboxSceneBuilder.AssetRoot+"/Editor/GeometryDiagnostics.uss");
            rootVisualElement.styleSheets.Add(style);
            var scroll=new ScrollView(ScrollViewMode.Vertical);scroll.AddToClassList("geometry-scroll");rootVisualElement.Add(scroll);
            var content=scroll.contentContainer;content.AddToClassList("geometry-panel");
            content.Add(new Label("M3 · 4D Geometry Core"){name="heading"});
            content.Add(new HelpBox("Диагностика в Play Mode. W связан с общим W игрока и проверяется на безопасность по всему переходу. B1/C1 имеют коллайдеры. Захват 4D ещё недоступен. Esc освобождает курсор.",HelpBoxMessageType.Info));
            diagnostics=FindFirstObjectByType<GeometryDiagnostics>();
            binding=new ObjectField("Контроллер"){objectType=typeof(GeometryDiagnostics),allowSceneObjects=true,value=diagnostics};
            binding.RegisterValueChangedCallback(e=>{diagnostics=e.newValue as GeometryDiagnostics;BindChoices();});content.Add(binding);
            source=new PopupField<string>("Источник",new List<string>{"B1 · Hypersphere","C1 · Tesseract"},1){name="source"};content.Add(source);
            actions=new VisualElement();content.Add(actions);
            slice=new DoubleField("Общий W игрока"){name="slice-w"};slice.RegisterValueChangedCallback(e=>Run(()=>diagnostics.SetTestW(e.newValue)));actions.Add(slice);
            plane=new EnumField("Плоскость",RotationPlane.XW){name="plane"};actions.Add(plane);
            angle=new DoubleField("Угол, градусы"){name="angle",value=45};actions.Add(angle);
            var row=new VisualElement();row.AddToClassList("button-row");
            row.Add(new Button(()=>ApplySelectedRotation(false)){text="В мировых осях",name="rotate-world"});
            row.Add(new Button(()=>ApplySelectedRotation(true)){text="В локальных осях",name="rotate-local"});actions.Add(row);
            actions.Add(new Label("Повороты накапливаются вокруг центра тела. Сначала выбирается плоскость, затем применяется угол."));
            dx=new DoubleField("Сдвиг X"){name="delta-x"};dy=new DoubleField("Сдвиг Y"){name="delta-y"};
            dz=new DoubleField("Сдвиг Z"){name="delta-z"};dw=new DoubleField("Сдвиг W тела"){name="delta-w"};
            actions.Add(dx);actions.Add(dy);actions.Add(dz);actions.Add(dw);
            actions.Add(new Button(()=>Run(()=>Selected().SetState(Selected().State.Translated(new Vector4D(dx.value,dy.value,dz.value,dw.value))))){text="Применить сдвиг тела",name="translate"});
            actions.Add(new Button(()=>Run(()=>Selected().ResetTransform())){text="Исходное преобразование выбранного тела",name="reset-selected"});
            actions.Add(new Button(()=>Run(()=>diagnostics.ResetAll())){text="Сброс B1/C1 и тестового W = 0",name="reset-all"});
            message=new HelpBox("",HelpBoxMessageType.Error);message.AddToClassList("hidden");content.Add(message);
            stats=new Label();stats.AddToClassList("statistics");content.Add(stats);
            BindChoices();Refresh();
        }
        GeometrySliceView Selected()
        {
            if(diagnostics==null || diagnostics.Views.Length==0)throw new InvalidOperationException("Сначала создайте Sandbox_01.");
            return diagnostics.Views[Math.Min(source.index,diagnostics.Views.Length-1)];
        }
        void BindChoices()
        {
            if(source==null)return;
            if(diagnostics!=null && diagnostics.Views.Length>0)
                source.choices=diagnostics.Views.Select(v=>v.Id+" · "+v.Shape).ToList();
        }
        public void ApplySelectedRotation(bool local)=>Run(()=>Selected().Rotate((RotationPlane)plane.value,angle.value,local));
        void Run(Action operation)
        {
            if(!EditorApplication.isPlaying || diagnostics==null)return;
            try{operation();message.AddToClassList("hidden");}
            catch(ArgumentException e){message.text=e.Message;message.RemoveFromClassList("hidden");}
            catch(InvalidOperationException e){message.text=e.Message;message.RemoveFromClassList("hidden");}
            nextRefresh=0;Refresh();EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        void Refresh()
        {
            if(actions==null || EditorApplication.timeSinceStartup<nextRefresh)return;
            nextRefresh=EditorApplication.timeSinceStartup+.1;
            if(diagnostics==null)
            {
                diagnostics=FindFirstObjectByType<GeometryDiagnostics>();
                if(diagnostics!=null){binding.SetValueWithoutNotify(diagnostics);BindChoices();}
            }
            actions.SetEnabled(EditorApplication.isPlaying && diagnostics!=null);
            if(diagnostics==null){stats.text="M3-контроллер не найден. Постройте Sandbox_01.";return;}
            slice.SetValueWithoutNotify(diagnostics.TestW);
            var view=Selected();var result=view.CurrentSlice;
            if(result==null){stats.text="Ожидание сечения";return;}
            string F(double n)=>n.ToString("0.#########",CultureInfo.InvariantCulture);
            var state=view.State;string rows="";
            for(int r=0;r<4;r++){for(int c=0;c<4;c++)rows+=state.Rotation[r,c].ToString(" 0.000000;-0.000000",CultureInfo.InvariantCulture)+"  ";rows+="\n";}
            stats.text=view.Id+" / "+result.Kind+" / dimension "+result.Dimension+"\n"+
                "Body XYZ,W = "+F(state.Position.X)+", "+F(state.Position.Y)+", "+F(state.Position.Z)+", "+F(state.Position.W)+"\n"+
                "Query W = "+F(diagnostics.TestW)+"; vertices = "+result.Vertices.Count+"; faces = "+result.Faces.Count+"; triangles = "+result.Triangles.Count/3+"\n"+
                (view.Shape==Shape4D.Hypersphere?"Analytic radius = "+F(result.AnalyticRadius)+"\n":"Bounds size = "+F(result.Maximum.X-result.Minimum.X)+", "+F(result.Maximum.Y-result.Minimum.Y)+", "+F(result.Maximum.Z-result.Minimum.Z)+"\n")+
                "Render = "+view.RenderStatus+"; mesh rebuilds = "+view.MeshBuildCount+"; last calculation/upload = "+view.LastBuildMilliseconds.ToString("0.000",CultureInfo.InvariantCulture)+" ms\n"+
                "Rotation SO(4):\n"+rows+"\nEmpty / degenerate slices retain the same canonical object. Lower-dimensional boundaries have no solid render mesh.";
        }
    }
    [CustomEditor(typeof(GeometrySliceView))]
    public sealed class GeometrySliceInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root=new VisualElement();var view=(GeometrySliceView)target;
            root.Add(new Label(view.Id+" / "+view.Shape+" / extent "+view.Extent));
            root.Add(new HelpBox("Canonical state uses double. Unity Transform and Mesh are derived. Use the M3 window in Play Mode; the world coordinate and collider safety remain authoritative.",HelpBoxMessageType.Info));
            root.Add(new Button(GeometryDiagnosticsWindow.Open){text="Open 4D Geometry Diagnostics"});return root;
        }
    }
}
