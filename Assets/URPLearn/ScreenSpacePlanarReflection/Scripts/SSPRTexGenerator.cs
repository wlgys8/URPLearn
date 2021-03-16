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

        private int _kernalPass1;
        private int _kernalPass2;

        private int _kernelClear;

        private int _reflectionTexID = Shader.PropertyToID("_ReflectionTex");

        public SSPRTexGenerator(){
        }

        public void Setup(ComputeShader cp){
            _computeShader = cp;
            _kernelClear = _computeShader.FindKernel("Clear");
            _kernalPass1 = _computeShader.FindKernel("DrawReflectionTex1");
            _kernalPass2 = _computeShader.FindKernel("DrawReflectionTex2");
        }


       public void Render(CommandBuffer cmd, ref RenderingData renderingData,ref PlanarDescriptor planarDescriptor)
        {

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
            _blurBlitter.SetSource(_reflectionTexID,reflectionTexDes);
            _blurBlitter.blurType = BlurType.BoxBilinear;
            _blurBlitter.iteratorCount = 1;
            _blurBlitter.downSample = 1;
            _blurBlitter.Render(cmd);

            cmd.SetGlobalTexture(ShaderProperties.ReflectionTexture,_reflectionTexID);
        }      

        public void ReleaseTemporary(CommandBuffer cmd){
            cmd.ReleaseTemporaryRT(_reflectionTexID);
        }

        private static class ShaderProperties{

            public static readonly int Result = Shader.PropertyToID("_Result");
            public static readonly int CameraColorTexture = Shader.PropertyToID("_CameraColorTexture");
            public static readonly int ReflectionTexture = Shader.PropertyToID("_ReflectionTex");
            public static readonly int PlanarPosition = Shader.PropertyToID("_PlanarPosition");
            public static readonly int PlanarNormal = Shader.PropertyToID("_PlanarNormal");
            public static readonly int MatrixVP = Shader.PropertyToID("_MatrixVP");
            public static readonly int MatrixInvVP = Shader.PropertyToID("_MatrixInvVP");
            public static readonly int MainTexelSize = Shader.PropertyToID("_MainTex_TexelSize");
        }
    }
}
