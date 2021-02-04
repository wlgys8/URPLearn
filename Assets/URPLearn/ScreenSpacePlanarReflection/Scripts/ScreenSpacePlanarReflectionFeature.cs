using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{
    public class ScreenSpacePlanarReflectionFeature : ScriptableRendererFeature
    {
        [SerializeField]
        private Material _material;

        [SerializeField]
        private ComputeShader _computeShader;

        private SSPRPlanarRenderPass _pass;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if(renderingData.cameraData.renderType != CameraRenderType.Base){
                return;
            }
            if(_material == null || _computeShader == null){
                return;
            }
            _pass.Setup(_material,_computeShader);
            _pass.ConfigureTarget(renderer.cameraColorTarget,renderer.cameraDepth);
            renderer.EnqueuePass(_pass);
        }

        public override void Create()
        {
            _pass = new SSPRPlanarRenderPass();
            
        }

        public class SSPRPlanarRenderPass : ScriptableRenderPass
        {
            
            private const string CommandBufferTag = "SSPR-Reflection";

            private Material _material;

            private SSPRTexGenerator _ssprTexGenerator = new SSPRTexGenerator();

            public SSPRPlanarRenderPass(){
                this.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            }

            public void Setup(Material material,ComputeShader cp){
                _material = material;
                _ssprTexGenerator.Setup(cp);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get(CommandBufferTag);
                try{
                    _ssprTexGenerator.Render(cmd,ref renderingData);
                    cmd.SetRenderTarget(this.colorAttachment,this.depthAttachment);
                    foreach(var planar in ReflectPlanar.activePlanars){
                        var rd = planar.GetComponent<Renderer>();
                        cmd.DrawRenderer(rd,_material);
                    }
                    _ssprTexGenerator.ReleaseTemporary(cmd);
                    context.ExecuteCommandBuffer(cmd);
                }finally{
                    CommandBufferPool.Release(cmd);
                }
            }
        }



    }
}
