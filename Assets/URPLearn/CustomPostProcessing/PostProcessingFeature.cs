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
            _pass.Setup(_effects);
            _pass.ConfigureTarget(renderer.cameraColorTarget);
            renderer.EnqueuePass(_pass);
        }

        /// <summary>
        /// 初始化调用
        /// </summary>
        public override void Create()
        {
            _pass = new PostProcessingPass();
        }
    }


    /// <summary>
    /// 自定义的后处理Pass
    /// </summary>
    public class PostProcessingPass : ScriptableRenderPass
    {

        private const string CommandBufferTag = "CustomPostProcessing";

        private List<PostProcessingEffect> _effects;
        private RenderTargetHandle _tempRT;
        private RenderTargetIdentifier _pingRTI;
        private RenderTargetIdentifier _pongRTI;

        private bool _isTempRTHold = false;

        public PostProcessingPass(){
            //把自定义的后处理放在透明物体渲染结束后。
            this.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            _tempRT.Init("POST_PROCESSING_TEMP_RT");
        }

        public void Setup(List<PostProcessingEffect> effects){
            _effects = effects;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get(CommandBufferTag);
            try{
                cmd.Clear();
                // 调用渲染函数
                Render(cmd, ref renderingData,context);
                // 执行命令缓冲区
                context.ExecuteCommandBuffer(cmd);
            }finally{
                // 释放命令缓存
                CommandBufferPool.Release(cmd);
            }
        }

        private void SwitchPingPong(){
            var temp = _pingRTI;
            _pingRTI = _pongRTI;
            _pongRTI = temp;
        }

        private RenderTargetIdentifier EnsurePongRTI(CommandBuffer cmd,RenderTextureDescriptor des){
            if(_isTempRTHold){
                return _pongRTI;
            }
            _isTempRTHold = true;
            cmd.GetTemporaryRT(_tempRT.id,des);
            _pongRTI = _tempRT.Identifier();
            return _pongRTI;
        }

        private void ReleaseAllTempRT(CommandBuffer cmd){
            if(_isTempRTHold){
                _isTempRTHold = false;
                cmd.ReleaseTemporaryRT(_tempRT.id);
            }
        }



        void Render(CommandBuffer cmd, ref RenderingData renderingData ,ScriptableRenderContext context)
        {       

            var cameraDes = renderingData.cameraData.cameraTargetDescriptor;
            var colorAttachment = this.colorAttachment;
            // renderingData.cameraData.requiresDepthTexture = true;

            _pingRTI = colorAttachment;
            try{
                var index = 0;
                int switchCount = 0;
                foreach(var e in _effects){
                    if(e && e.active){
                        var pongRTI = EnsurePongRTI(cmd,cameraDes);
                        if(e.Render(cmd,ref renderingData,_pingRTI,pongRTI)){
                            SwitchPingPong();
                            switchCount ++;
                        }
                        index ++;
                    }
                }
                if(switchCount % 2 == 1){
                    cmd.Blit(_pingRTI,colorAttachment);
                }
                context.ExecuteCommandBuffer(cmd);
            }finally{
                ReleaseAllTempRT(cmd);
            }
        }  
    }

}
