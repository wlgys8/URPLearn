using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{
    public class PostProcessingFeature : ScriptableRendererFeature
    {
        private PostProcessingPass _pass;

        [SerializeField]
        private List<PostProcessingEffect> _effects = new List<PostProcessingEffect>();

        /// <summary>
        /// 加入自定义的Pass
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if(renderingData.cameraData.renderType != CameraRenderType.Base){
                return;
            }
            _pass.ConfigureTarget(renderer.cameraColorTarget);
            renderer.EnqueuePass(_pass);
        }

        /// <summary>
        /// 初始化调用
        /// </summary>
        public override void Create()
        {
            _pass = new PostProcessingPass();
            _pass.Setup(_effects);
        }
    }


    /// <summary>
    /// 自定义的后处理Pass
    /// </summary>
    public class PostProcessingPass : ScriptableRenderPass
    {

        private const string CommandBufferTag = "CustomPostProcessing";

        private List<PostProcessingEffect> _effects;

        private PostProcessingRenderContext _postContext = new PostProcessingRenderContext();

        public PostProcessingPass(){
            //把自定义的后处理放在透明物体渲染结束后。
            this.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public void Setup(List<PostProcessingEffect> effects){
            _effects = effects;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get(CommandBufferTag);
            try{
                // cmd.Clear();
                // 调用渲染函数
                Render(cmd, ref renderingData,context);
                // 执行命令缓冲区
                // context.ExecuteCommandBuffer(cmd);
            }finally{
                // 释放命令缓存
                CommandBufferPool.Release(cmd);
            }
        }


        void Render(CommandBuffer cmd, ref RenderingData renderingData ,ScriptableRenderContext context)
        {       

            var cameraDes = renderingData.cameraData.cameraTargetDescriptor;
            var colorAttachment = this.colorAttachment;
            try{
                _postContext.Prepare(ref renderingData,colorAttachment);
                foreach(var e in _effects){
                    if(e && e.active){
                        e.Render(cmd,ref renderingData,_postContext);
                    }
                }
                _postContext.BlitBackToSource(cmd);
                context.ExecuteCommandBuffer(cmd);
            }finally{
                _postContext.Release(cmd);
            }
        }  
    }

}
