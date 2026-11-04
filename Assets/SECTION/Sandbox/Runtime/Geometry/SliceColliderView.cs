using System;
using Section.Geometry4D;
using UnityEngine;
namespace Section.Sandbox
{
    [ExecuteAlways,RequireComponent(typeof(GeometrySliceView),typeof(SphereCollider),typeof(MeshCollider))]
    public sealed class SliceColliderView : MonoBehaviour
    {
        GeometrySliceView source;
        SphereCollider sphere;
        MeshCollider polytope;
        Mesh collisionMesh;
        Mesh spareMesh;
        readonly MeshCollider[] compound=new MeshCollider[2];
        readonly Mesh[] compoundMeshes=new Mesh[4];
        int compoundBuffer;
        public Mesh GeneratedColliderMesh=>collisionMesh;
        public Collider ActiveCollider=>compound[0]!=null && compound[0].enabled?compound[0]:compound[1]!=null && compound[1].enabled?compound[1]:sphere!=null && sphere.enabled?sphere:
            polytope!=null && polytope.enabled && polytope.sharedMesh!=null?polytope:null;
        public System.Collections.Generic.IEnumerable<Collider> ActiveColliders
        {get{foreach(var c in compound)if(c!=null && c.enabled)yield return c;if(sphere!=null && sphere.enabled)yield return sphere;if(polytope!=null && polytope.enabled && polytope.sharedMesh!=null)yield return polytope;}}
        public int Revision { get; private set; }
        void OnEnable()
        {
            source=GetComponent<GeometrySliceView>();sphere=GetComponent<SphereCollider>();polytope=GetComponent<MeshCollider>();
            sphere.enabled=false;polytope.enabled=false;polytope.sharedMesh=null;
            sphere.contactOffset=.005f;polytope.contactOffset=.005f;polytope.convex=true;
            source.SliceChanged+=Synchronize;Synchronize(source);
        }
        void Synchronize(GeometrySliceView view)
        {
            var slice=view.CurrentSlice;
            sphere.enabled=false;polytope.enabled=false;polytope.sharedMesh=null;
            foreach(var c in compound)if(c!=null){c.enabled=false;c.sharedMesh=null;}
            Revision=view.MeshBuildCount;
            if(slice==null || view.RenderStatus!=SliceRenderStatus.Ready)return;
            if(view.Shape==Shape4D.CompoundBox)
            {
                var parts=CompoundBox4D.Parts(view.State,view.SliceW);compoundBuffer=1-compoundBuffer;
                for(int p=0;p<2;p++)
                {
                    if(compound[p]==null){var node=new GameObject("DerivedSlicePart_"+p){hideFlags=HideFlags.DontSave};node.transform.SetParent(transform,false);compound[p]=node.AddComponent<MeshCollider>();compound[p].convex=true;compound[p].contactOffset=.005f;}
                    if(parts[p].Kind!=SliceKind.Volume)continue;int index=p*2+compoundBuffer;
                    if(compoundMeshes[index]==null)compoundMeshes[index]=new Mesh{name=view.Id+"_part_"+p,hideFlags=HideFlags.DontSave};
                    SliceMeshWriter.Write(compoundMeshes[index],parts[p]);Physics.BakeMesh(compoundMeshes[index].GetInstanceID(),true,compound[p].cookingOptions);
                    compound[p].transform.localPosition=SliceMeshWriter.ToVector3((view.State.Rotation*CompoundBox4D.Center(p)).XYZ);compound[p].sharedMesh=compoundMeshes[index];compound[p].enabled=true;
                }return;
            }
            if(view.Shape==Shape4D.Hypersphere)
            {
                sphere.center=Vector3.zero;sphere.radius=(float)slice.AnalyticRadius;sphere.enabled=sphere.radius>0;
                return;
            }
            if(slice.Triangles.Count/3>255)throw new InvalidOperationException("Convex collider exceeds the supported M4 triangle budget: "+view.Id);
            // Physics must not retain a Mesh while the renderer clears/rebuilds it.
            // Reuse an independently owned collider buffer derived from the SAME immutable slice result.
            var previous=collisionMesh;collisionMesh=spareMesh;spareMesh=previous;
            if(collisionMesh==null)collisionMesh=new Mesh{name=view.Id+"_ColliderSlice",hideFlags=HideFlags.DontSave};
            if(SliceMeshWriter.Write(collisionMesh,slice)!=SliceRenderStatus.Ready)
                throw new InvalidOperationException("Solid collider mesh could not be represented: "+view.Id);
            Physics.BakeMesh(collisionMesh.GetInstanceID(),true,polytope.cookingOptions);
            polytope.sharedMesh=collisionMesh;
            int assignedBeforeEnable=polytope.sharedMesh==null?0:polytope.sharedMesh.GetInstanceID();
            polytope.enabled=true;
            if(polytope.sharedMesh!=collisionMesh)throw new InvalidOperationException("Collider did not accept the current slice mesh: "+view.Id+
                " wanted="+collisionMesh.GetInstanceID()+" beforeEnable="+assignedBeforeEnable+" afterEnable="+(polytope.sharedMesh==null?0:polytope.sharedMesh.GetInstanceID())+
                " vertices="+collisionMesh.vertexCount+" bounds="+collisionMesh.bounds);
        }
        void OnDisable()
        {
            if(source!=null)source.SliceChanged-=Synchronize;
            foreach(var c in compound)if(c!=null){c.enabled=false;if(Application.isPlaying)Destroy(c.gameObject);else DestroyImmediate(c.gameObject);}
            for(int i=0;i<compoundMeshes.Length;i++)if(compoundMeshes[i]!=null){if(Application.isPlaying)Destroy(compoundMeshes[i]);else DestroyImmediate(compoundMeshes[i]);compoundMeshes[i]=null;}
            if(sphere!=null)sphere.enabled=false;
            if(polytope!=null){polytope.enabled=false;polytope.sharedMesh=null;}
            if(collisionMesh!=null){if(Application.isPlaying)Destroy(collisionMesh);else DestroyImmediate(collisionMesh);collisionMesh=null;}
            if(spareMesh!=null){if(Application.isPlaying)Destroy(spareMesh);else DestroyImmediate(spareMesh);spareMesh=null;}
        }
    }
}
