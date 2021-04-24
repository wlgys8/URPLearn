using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace URPLearn{
    [ExecuteInEditMode]
    public class Water : MonoBehaviour
    {
        // Start is called before the first frame update

        private static HashSet<Water> _visibles = new HashSet<Water>();

        public static IReadOnlyCollection<Water> visibles{
            get{
                return _visibles;
            }
        }

        void OnEnable(){
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            _visibles.Add(this);
        }

        void OnDisable(){
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            _visibles.Remove(this);
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera){
        
            var viewMatrix = camera.worldToCameraMatrix;
            //不知道为什么，第二个参数是false才能正常得到世界坐标
            var projectMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix,false);
            var matrixVP = projectMatrix * viewMatrix;
            var invMatrixVP = matrixVP.inverse;
            var material = GetComponent<Renderer>().sharedMaterial;
            material.SetMatrix("MatrixVP",matrixVP);
            material.SetMatrix("MatrixInvVP",invMatrixVP);
        }
    
    }
}
