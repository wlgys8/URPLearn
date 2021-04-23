using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Linq;

namespace URPLearn{
    public class SSPRTexGenerator
    {

        private ComputeShader _computeShader;
        private BlurBlitter _blurBlitter = new BlurBlitter();

        private int _kernelClear;
        private int _kernalPass1;
        private int _kernalPass2;

        private int _reflectionTexID;

        private bool _enableBlur = true;

        /// <summary>
        /// 在生成反射贴图的时候，是否剔除掉无穷远的像素(例如天空盒)
        /// </summary>
        private bool _excludeBackground = false;
        private bool _isCSLoadTried = false;

        public SSPRTexGenerator(string reflectionTexName = "_ReflectionTex"){
            _reflectionTexID = Shader.PropertyToID(reflectionTexName);
        }

        public void BindCS(ComputeShader cp){
            _computeShader = cp;
            this.UpdateKernelIndex();
        }

        private void UpdateKernelIndex(){
            _kernelClear = _computeShader.FindKernel("Clear");
            _kernalPass1 = _computeShader.FindKernel("DrawReflectionTex1");
            _kernalPass2 = _computeShader.FindKernel("DrawReflectionTex2");
            if(_excludeBackground){
                _kernalPass1 +=2;
                _kernalPass2 +=2;
            }
        }

        public bool excludeBackground{
            get{
                return _excludeBackground;
            }set{
                _excludeBackground = value;
                if(_computeShader){
                    this.UpdateKernelIndex();
                }
            }
        }

        public bool enableBlur{
            get{
                return _enableBlur;
            }set{
                _enableBlur = value;
            }
        }

        private void TryLoadCSIfNot(){
            if(_computeShader){
                return;
            }
            if(_isCSLoadTried){
                return;
            }
            _isCSLoadTried = true;
            #if UNITY_EDITOR
            _computeShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/URPLearn/ScreenSpacePlanarReflection/Shaders/ReflectionTexCompute.compute");
            #endif
            if(!_computeShader){
                Debug.LogWarning("missing compute shader");
                return;
            }
            this.BindCS(_computeShader);
        }

       public void Render(CommandBuffer cmd, ref RenderingData renderingData,ref PlanarDescriptor planarDescriptor)
        {

            this.TryLoadCSIfNot();

            if(!_computeShader){
                return;
            }

            var reflectionTexDes = renderingData.cameraData.cameraTargetDescriptor;
            reflectionTexDes.enableRandomWrite = true;
            reflectionTexDes.msaaSamples = 1;
            cmd.GetTemporaryRT(_reflectionTexID,reflectionTexDes);

            var rtWidth = reflectionTexDes.width;
            var rtHeight = reflectionTexDes.height;

            ///==== Compute Shader Begin ===== ///

            var cameraData = renderingData.cameraData;
            var viewMatrix = cameraData.camera.worldToCameraMatrix;
            //不知道为什么，第二个参数是false才能正常得到世界坐标
            var projectMatrix = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(),false);
            var matrixVP = projectMatrix * viewMatrix;
            var invMatrixVP = matrixVP.inverse;

            var threadGroupX = reflectionTexDes.width / 8;
            var threadGroupY = reflectionTexDes.height / 8;

            RenderTargetIdentifier cameraColorTex = ShaderProperties.CameraColorTexture;
            
            cmd.SetComputeVectorParam(_computeShader,ShaderProperties.MainTexelSize,new Vector4(1.0f / rtWidth,1.0f /rtHeight,rtWidth,rtHeight));
            cmd.SetComputeMatrixParam(_computeShader,ShaderProperties.MatrixVP,matrixVP);
            cmd.SetComputeMatrixParam(_computeShader,ShaderProperties.MatrixInvVP, invMatrixVP);
            cmd.SetComputeVectorParam(_computeShader,ShaderProperties.PlanarPosition,planarDescriptor.position);
            cmd.SetComputeVectorParam(_computeShader,ShaderProperties.PlanarNormal,planarDescriptor.normal);

            //clear the reflection texture
            cmd.SetComputeTextureParam(_computeShader,_kernelClear,ShaderProperties.Result,_reflectionTexID);
            cmd.DispatchCompute(_computeShader,_kernelClear,threadGroupX,threadGroupY,1);
            
            cmd.SetComputeTextureParam(_computeShader,_kernalPass1,ShaderProperties.CameraColorTexture,cameraColorTex);
            cmd.SetComputeTextureParam(_computeShader,_kernalPass1,ShaderProperties.Result,_reflectionTexID);
            cmd.DispatchCompute(_computeShader,_kernalPass1,threadGroupX,threadGroupY,1);

            cmd.SetComputeTextureParam(_computeShader,_kernalPass2,ShaderProperties.CameraColorTexture,cameraColorTex);
            cmd.SetComputeTextureParam(_computeShader,_kernalPass2,ShaderProperties.Result,_reflectionTexID);
            cmd.DispatchCompute(_computeShader,_kernalPass2,threadGroupX,threadGroupY,1);
            // ====== blur begin ===== ///
            if(_enableBlur){
                _blurBlitter.SetSource(_reflectionTexID,reflectionTexDes);
                _blurBlitter.blurType = BlurType.BoxBilinear;
                _blurBlitter.iteratorCount = 1;
                _blurBlitter.downSample = 1;
                _blurBlitter.Render(cmd);
            }

            cmd.SetGlobalTexture(_reflectionTexID,_reflectionTexID);
        }      

        public void ReleaseTemporary(CommandBuffer cmd){
            cmd.ReleaseTemporaryRT(_reflectionTexID);
        }

        private static class ShaderProperties{

            public static readonly int Result = Shader.PropertyToID("_Result");
            public static readonly int CameraColorTexture = Shader.PropertyToID("_CameraColorTexture");
            public static readonly int PlanarPosition = Shader.PropertyToID("_PlanarPosition");
            public static readonly int PlanarNormal = Shader.PropertyToID("_PlanarNormal");
            public static readonly int MatrixVP = Shader.PropertyToID("_MatrixVP");
            public static readonly int MatrixInvVP = Shader.PropertyToID("_MatrixInvVP");
            public static readonly int MainTexelSize = Shader.PropertyToID("_MainTex_TexelSize");
        }
    }
}
