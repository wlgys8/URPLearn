using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace  URPLearn
{
    public class CopyColorWithAlphaFeature : ScriptableRendererFeature
    {   
        [SerializeField]
        private Material _material;

        private CopyColorWithAlphaPass _pass;

        RenderTargetHandle m_OpaqueColor;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData){
            if(renderingData.cameraData.renderType != CameraRenderType.Base){
                return;
            }
            if(_pass == null){
                if(_material){
                    _pass = new CopyColorWithAlphaPass(RenderPassEvent.AfterRenderingSkybox,_material);
                }else{
                    return;
                }
            }
            _pass.Setup(renderer.cameraColorTarget,m_OpaqueColor,Downsampling._4xBox);
            renderer.EnqueuePass(_pass);
        }

        private void EnsureMaterialInEditor(){
            #if UNITY_EDITOR
            if(!_material){
                _material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/URPLearn/CustomCopyColorPass/Materials/CopyColorMat.mat");
            }
            #endif
        }

        public override void Create()
        {
            this.EnsureMaterialInEditor();
            m_OpaqueColor.Init("_CameraOpaqueTexture");
            if(_material){
                _pass = new CopyColorWithAlphaPass(RenderPassEvent.AfterRenderingSkybox,_material);
            }
        }
    }


    /// <summary>
    /// Copy the given color buffer to the given destination color buffer.
    ///
    /// You can use this pass to copy a color buffer to the destination,
    /// so you can use it later in rendering. For example, you can copy
    /// the opaque texture to use it for distortion effects.
    /// </summary>
    public class CopyColorWithAlphaPass : ScriptableRenderPass
    {
        int m_SampleOffsetShaderHandle;
        Material m_SamplingMaterial;
        Downsampling m_DownsamplingMethod;

        private RenderTargetIdentifier source { get; set; }
        private RenderTargetHandle destination { get; set; }
        const string m_ProfilerTag = "Copy Color";

        /// <summary>
        /// Create the CopyColorPass
        /// </summary>
        public CopyColorWithAlphaPass(RenderPassEvent evt, Material samplingMaterial)
        {
            m_SamplingMaterial = samplingMaterial;
            m_SampleOffsetShaderHandle = Shader.PropertyToID("_SampleOffset");
            renderPassEvent = evt;
            m_DownsamplingMethod = Downsampling.None;
        }

        /// <summary>
        /// Configure the pass with the source and destination to execute on.
        /// </summary>
        /// <param name="source">Source Render Target</param>
        /// <param name="destination">Destination Render Target</param>
        public void Setup(RenderTargetIdentifier source, RenderTargetHandle destination, Downsampling downsampling)
        {
            this.source = source;
            this.destination = destination;
            m_DownsamplingMethod = downsampling;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescripor)
        {
            RenderTextureDescriptor descriptor = cameraTextureDescripor;
            descriptor.msaaSamples = 1;
            descriptor.depthBufferBits = 0;
            if (m_DownsamplingMethod == Downsampling._2xBilinear)
            {
                descriptor.width /= 2;
                descriptor.height /= 2;
            }
            else if (m_DownsamplingMethod == Downsampling._4xBox || m_DownsamplingMethod == Downsampling._4xBilinear)
            {
                descriptor.width /= 4;
                descriptor.height /= 4;
            }

            cmd.GetTemporaryRT(destination.id, descriptor, m_DownsamplingMethod == Downsampling.None ? FilterMode.Point : FilterMode.Bilinear);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_SamplingMaterial == null)
            {
                Debug.LogErrorFormat("Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.", m_SamplingMaterial, GetType().Name);
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            RenderTargetIdentifier opaqueColorRT = destination.Identifier();

            switch (m_DownsamplingMethod)
            {
                case Downsampling.None:
                    Blit(cmd, source, opaqueColorRT);
                    break;
                case Downsampling._2xBilinear:
                    Blit(cmd, source, opaqueColorRT);
                    break;
                case Downsampling._4xBox:
                    m_SamplingMaterial.SetFloat(m_SampleOffsetShaderHandle, 2);
                    Blit(cmd, source, opaqueColorRT, m_SamplingMaterial);
                    break;
                case Downsampling._4xBilinear:
                    Blit(cmd, source, opaqueColorRT);
                    break;
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <inheritdoc/>
        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                return;

            if (destination != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(destination.id);
                destination = RenderTargetHandle.CameraTarget;
            }
        }
    }
}

