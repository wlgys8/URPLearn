using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{
    public class PostProcessingRenderContext
    {

        private static class TempRTManager{
            
            private static Dictionary<RenderTargetIdentifier,RenderTargetHandle> _usingTempRTs = new Dictionary<RenderTargetIdentifier,RenderTargetHandle>();
            private static int _tempIndex;
            public static RenderTargetIdentifier Request(CommandBuffer cmd,RenderTextureDescriptor renderTextureDescriptor,FilterMode filterMode = FilterMode.Point){
                var handle = new RenderTargetHandle();
                handle.Init("_POST_PROCESSING_TEMP_" + _tempIndex ++);
                cmd.GetTemporaryRT(handle.id,renderTextureDescriptor,filterMode);
                _usingTempRTs.Add(handle.Identifier(),handle);
                return handle.Identifier();
            }

            public static void Release(CommandBuffer command, RenderTargetIdentifier identifier){
                if(_usingTempRTs.ContainsKey(identifier)){
                    var handle = _usingTempRTs[identifier];
                    _usingTempRTs.Remove(identifier);
                    command.ReleaseTemporaryRT(handle.id);
                }
            }

            public static void EndFrame(){
                _tempIndex = 0;
                if(_usingTempRTs.Count > 0){
                    Debug.LogWarning("there are some temporary rendertexture not released in frame.");
                }
            }
        }
        
        private RenderTargetIdentifier _sourceRTI;

        private RenderTargetIdentifier _activeRTI;

        private RenderingData _renderingData;
        private RenderTargetHandle _tempRTHandle;

        internal void Prepare(ref RenderingData renderingData,RenderTargetIdentifier sourceRTI){
            _sourceRTI = sourceRTI;
            _renderingData = renderingData;
            _activeRTI = _sourceRTI;
        }

        public RenderTextureDescriptor sourceRenderTextureDescriptor{
            get{
                return _renderingData.cameraData.cameraTargetDescriptor;
            }
        }

        public RenderTargetIdentifier GetTemporaryRT(CommandBuffer cmd){
            var id = TempRTManager.Request(cmd,sourceRenderTextureDescriptor);
            return id;
        }

        public RenderTargetIdentifier GetTemporaryRT(CommandBuffer cmd,RenderTextureDescriptor descriptor,FilterMode filter = FilterMode.Point){
            var id = TempRTManager.Request(cmd,descriptor,filter);
            return id;
        }

        public void ReleaseTemporaryRT(CommandBuffer command, RenderTargetIdentifier id){
            TempRTManager.Release(command,id);
        }

        private void SetActiveRT(CommandBuffer command, RenderTargetIdentifier renderTargetIdentifier){
            if(_activeRTI != _sourceRTI){
                TempRTManager.Release(command,_activeRTI);
            }
            _activeRTI = renderTargetIdentifier;
        }

        public void BlitAndSwap(CommandBuffer cmd,Material material,int pass = 0){
            var from = _activeRTI;
            RenderTargetIdentifier to;
            if(_activeRTI == _sourceRTI){
                to = TempRTManager.Request(cmd,sourceRenderTextureDescriptor);
            }else{
                to = _sourceRTI;
            }
            if(material == null){
                cmd.Blit(from,to);
            }else{
                cmd.Blit(from,to,material,pass);
            }
            SetActiveRT(cmd,to);
        }


        public RenderTargetIdentifier activeRenderTarget{
            get{
                return _activeRTI;
            }
        }
  

        public void BlitBackToSource(CommandBuffer cmd){
            if(_activeRTI == _sourceRTI){
                return;
            }
            cmd.Blit(_activeRTI,_sourceRTI);
            TempRTManager.Release(cmd,_activeRTI);
            _activeRTI = _sourceRTI;
        }

  
        internal void Release(CommandBuffer command){
            TempRTManager.EndFrame();
        }
        
    }
}
