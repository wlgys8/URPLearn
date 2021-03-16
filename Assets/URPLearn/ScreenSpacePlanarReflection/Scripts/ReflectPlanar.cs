using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace URPLearn{

    public struct PlanarDescriptor{
        public Vector3 position;
        public Vector3 normal;

        public static bool operator == (PlanarDescriptor p1,PlanarDescriptor p2){
            return  IsNormalEqual(p1.normal,p2.normal) && IsPositionInPlanar(p1.position,p2);
        }

        public static bool operator != (PlanarDescriptor p1,PlanarDescriptor p2){
            return  !IsNormalEqual(p1.normal,p2.normal) || !IsPositionInPlanar(p1.position,p2);
        }

        public override bool Equals(object obj)
        {
            if(obj == null){
                return false;
            }
            if(obj is PlanarDescriptor p){
                return IsNormalEqual(normal,p.normal) && IsPositionInPlanar(p.position,this);
            }else{
                return false;
            }
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + position.GetHashCode();
            hash = hash * 23 + normal.GetHashCode();
            return hash;
        }

        public override string ToString()
        {
            return base.ToString();
        }
        private static bool IsNormalEqual(Vector3 n1,Vector3 n2){
            return 1 - Vector3.Dot(n1,n2) < 0.001f;
        }

        private static bool IsPositionInPlanar(Vector3 checkPos,PlanarDescriptor planar){
            return Vector3.Dot(planar.position - checkPos,planar.normal) < 0.01f;
        }
    }

    public class PlanarRendererGroup{
        public PlanarDescriptor descriptor;
        public HashSet<Renderer> renderers = new HashSet<Renderer>();

        public void Clear(){
            renderers.Clear();
        }
    }


    public class PlanarRendererGroups{

        private List<PlanarRendererGroup> _freePool = new List<PlanarRendererGroup>();

        private List<PlanarRendererGroup> _planarRenderers = new List<PlanarRendererGroup>();

        public void Clear(){
            foreach(var p in _planarRenderers){
                p.Clear();
                _freePool.Add(p);
            }
            _planarRenderers.Clear();
        }

        public IReadOnlyCollection<PlanarRendererGroup> rendererGroups{
            get{
                return _planarRenderers;
            }
        }

        private PlanarRendererGroup AllocateGroup(){
            if(_freePool.Count > 0){
                var result = _freePool[_freePool.Count - 1];
                _freePool.RemoveAt(_freePool.Count - 1);
                return result;
            }
            return new PlanarRendererGroup();
        }

        public void AddRender(Renderer renderer){
            var position = renderer.transform.position;
            var normal = renderer.transform.up;
            var planarDescriptor = new PlanarDescriptor(){
                position = position,
                normal = normal
            };
            foreach(var renderers in _planarRenderers){
                if(renderers.descriptor == planarDescriptor){
                    renderers.renderers.Add(renderer);
                    return;
                }
            }
            {
                var renderers = AllocateGroup();
                renderers.descriptor = planarDescriptor;
                renderers.renderers.Add(renderer);
                _planarRenderers.Add(renderers);
            }
        }
    }

    [ExecuteInEditMode]
    public class ReflectPlanar : MonoBehaviour
    {

        private static HashSet<ReflectPlanar> _planars = new HashSet<ReflectPlanar>();

        public static IReadOnlyCollection<ReflectPlanar> activePlanars{
            get{
                return _planars;
            }
        }

        public static void GetVisiblePlanarGroups(PlanarRendererGroups groups){
            groups.Clear();
            foreach(var p in activePlanars){
                var renderer = p.GetComponent<Renderer>();
                if(renderer.isVisible){
                    groups.AddRender(renderer);
                }
            }
        }
      

        void OnEnable(){
            _planars.Add(this);
        }


        void OnDisable(){
            _planars.Remove(this);
        }
  
    }
}
