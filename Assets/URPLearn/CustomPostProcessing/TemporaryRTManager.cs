using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{
    public class TemporaryRTManager
    {
        private Dictionary<RenderTargetIdentifier,RenderTargetHandle> _usingTempRTs = new Dictionary<RenderTargetIdentifier,RenderTargetHandle>();
        private int _tempIndex;

        private List<RenderTargetHandle> _freeRTHandlers = new List<RenderTargetHandle>();

        public RenderTargetIdentifier Request(CommandBuffer cmd,RenderTextureDescriptor renderTextureDescriptor,FilterMode filterMode = FilterMode.Point){
            RenderTargetHandle handle;
            if(_freeRTHandlers.Count == 0){
                handle = new RenderTargetHandle();
                handle.Init($"_TemporaryRT_{this.GetHashCode()}_{_tempIndex ++}");
            }else{
                handle = _freeRTHandlers[_freeRTHandlers.Count - 1];
                _freeRTHandlers.RemoveAt(_freeRTHandlers.Count - 1);
            }
            cmd.GetTemporaryRT(handle.id,renderTextureDescriptor,filterMode);
            _usingTempRTs.Add(handle.Identifier(),handle);
            return handle.Identifier();
        }

        public void Release(CommandBuffer command, RenderTargetIdentifier identifier){
            if(_usingTempRTs.ContainsKey(identifier)){
                var handle = _usingTempRTs[identifier];
                _usingTempRTs.Remove(identifier);
                command.ReleaseTemporaryRT(handle.id);
                _freeRTHandlers.Add(handle);
            }
        }

        public void Cleanup(CommandBuffer cmd){
            foreach(var kv in _usingTempRTs){
                var handle = kv.Value;
                cmd.ReleaseTemporaryRT(handle.id);
                _freeRTHandlers.Add(handle);
            }
            _usingTempRTs.Clear();
        }
          
    }
}
