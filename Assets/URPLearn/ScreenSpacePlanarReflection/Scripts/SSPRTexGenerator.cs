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
        private RenderTargetHandle _reflectionTextureHandle;

        private BlurBlitter _blurBlitter = new BlurBlitter();

        private int _kernalPass1;
        private int _kernalPass2;

        private int _kernelClear;


        public SSPRTexGenerator(){
            _reflectionTextureHandle.Init("_ReflectionTex");
        }

        public void Setup(ComputeShader cp){
            _computeShader = cp;
            _kernelClear = _computeShader.FindKernel("Clear");
            _kernalPass1 = _computeShader.FindKernel("DrawReflectionTex1");
            _kernalPass2 = _computeShader.FindKernel("DrawReflectionTex2");
        }


       public void Render(CommandBuffer cmd, ref RenderingData renderingData)
        {

            if(!_computeShader){
                return;
            }
      
            if(ReflectPlanar.activePlanars.Count == 0){
                return;
            }
            var planar = ReflectPlanar.activePlanars.First();

            var sourceRenderTextureDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            var rtWidth = sourceRenderTextureDescriptor.width;
            var rtHeight = sourceRenderTextureDescriptor.height;
            var reflectionTexDes = sourceRenderTextureDescriptor;
            reflectionTexDes.enableRandomWrite = true;
            
            cmd.GetTemporaryRT(_reflectionTextureHandle.id,reflectionTexDes);

            RenderTargetIdentifier reflectionRTI = _reflectionTextureHandle.Identifier();

            
            ///==== Compute Shader Begin ===== ///


            var cameraData = renderingData.cameraData;
            var viewMatrix = cameraData.camera.worldToCameraMatrix;
            //不知道为什么，第二个参数是false才能正常得到世界坐标
            var projectMatrix = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(),false);
            var matrixVP = projectMatrix * viewMatrix;

            var threadGroupX = reflectionTexDes.width / 8;
            var threadGroupY = reflectionTexDes.height / 8;

            var cameraColorTex = new RenderTargetIdentifier("_CameraColorTexture");

            var depthTexture = new RenderTargetIdentifier("_CameraDepthTexture");

            
            cmd.SetComputeVectorParam(_computeShader,"_MainTex_TexelSize",new Vector4(1.0f / rtWidth,1.0f /rtHeight,rtWidth,rtHeight));
            cmd.SetComputeMatrixParam(_computeShader,"_MatrixVP",matrixVP);
            cmd.SetComputeMatrixParam(_computeShader,"_MatrixInvVP", viewMatrix.inverse * projectMatrix.inverse);
            cmd.SetComputeVectorParam(_computeShader,"_PlanarPosition",planar.transform.position);
            cmd.SetComputeVectorParam(_computeShader,"_PlanarNormal",planar.transform.up);

            //clear the reflection texture
            cmd.SetComputeTextureParam(_computeShader,_kernelClear,"Result",reflectionRTI);
            cmd.DispatchCompute(_computeShader,_kernelClear,threadGroupX,threadGroupY,1);

            cmd.SetComputeTextureParam(_computeShader,_kernalPass1,"Result",reflectionRTI);
            cmd.SetComputeTextureParam(_computeShader,_kernalPass1,"_MainTex",cameraColorTex);

            cmd.DispatchCompute(_computeShader,_kernalPass1,threadGroupX,threadGroupY,1);

            cmd.SetComputeTextureParam(_computeShader,_kernalPass2,"Result",reflectionRTI);
            cmd.SetComputeTextureParam(_computeShader,_kernalPass2,"_MainTex",cameraColorTex);

            cmd.DispatchCompute(_computeShader,_kernalPass2,threadGroupX,threadGroupY,1);
    
            // ====== blur begin ===== ///
            _blurBlitter.SetSource(reflectionRTI,reflectionTexDes);
            _blurBlitter.blurType = BlurType.BoxBilinear;
            _blurBlitter.iteratorCount = 1;
            _blurBlitter.downSample = 1;
            _blurBlitter.Render(cmd);

            cmd.SetGlobalTexture("_ReflectionTex",reflectionRTI);
        }      

        public void ReleaseTemporary(CommandBuffer cmd){
            cmd.ReleaseTemporaryRT(_reflectionTextureHandle.id);
        }
    }
}
