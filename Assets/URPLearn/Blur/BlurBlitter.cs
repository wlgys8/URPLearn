using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{

    public enum BlurType{
        None,
        Box,
        BoxBilinear,
        Gaussian,
        GaussianBilinear,
    }
    public class BlurBlitter
    {
        private static class ShaderConstants{
            public static int blurScale = Shader.PropertyToID("_BlurScale");
            public static int kernelSize = Shader.PropertyToID("_KernelSize");
        }

        private RenderTargetIdentifier _sourceRT;
        private RenderTextureDescriptor _sourceRenderTextureDescriptor;
        private Shader _usingShader;
        private Material _material;

        private BlurType _blurType = BlurType.None;
        private float _blurScale = 1;
        private int _boxKernelSizeHalf = 3;
        private int _iteratorCount = 1;
        private int _downSample = 1;

        private TemporaryRTManager _tempRTManager = new TemporaryRTManager();

        public BlurBlitter(){
        }

        public void SetSource(RenderTargetIdentifier sourceRT,RenderTextureDescriptor renderTextureDescriptor){
            _sourceRT = sourceRT;
            _sourceRenderTextureDescriptor = renderTextureDescriptor;
        }

        public BlurType blurType{
            get{
                return _blurType;
            }set{
                _blurType = value;
                _usingShader = FindShaderByBlurType(value);
                if(!_usingShader){
                    Debug.LogWarning("missing shader for blur type:" + value);
                }
            }
        }

        public int iteratorCount{
            get{
                return _iteratorCount;
            }set{
                _iteratorCount = Mathf.Clamp(value,0,5);
            }
        }

        public int downSample{
            get{
                return _downSample;
            }set{
                _downSample = Mathf.Clamp(value,1,5);
            }
        }

        public int boxKernelSizeHalf{
            get{
                return _boxKernelSizeHalf;
            }set{
                _boxKernelSizeHalf = Mathf.Clamp(value,1,4);
            }
        }

        public float blurScale{
            get{
                return _blurScale;
            }set{
                _blurScale = Mathf.Clamp(value,1,5);
            }
        }

        private static Shader FindShaderByBlurType(BlurType type){
            switch(type){
                case BlurType.Box:
                case BlurType.BoxBilinear:
                return Shader.Find("URPLearn/PostProcessing/BoxBlur");
                case BlurType.Gaussian:
                case BlurType.GaussianBilinear:
                return Shader.Find("URPLearn/PostProcessing/GaussianBlur");
                default:
                return null;
            }
        }


        public void Render(CommandBuffer cmd){
            if(!_usingShader){
                return;
            }
            if(!_material || _material.shader != _usingShader){
                _material = new Material(_usingShader);
            }
            if(_blurType == BlurType.GaussianBilinear || _blurType == BlurType.BoxBilinear){
                _material.EnableKeyword("_BilinearMode");
            }else{
                _material.DisableKeyword("_BilinearMode");
            }
            _material.SetFloat(ShaderConstants.blurScale,_blurScale);
            _material.SetInt(ShaderConstants.kernelSize,_boxKernelSizeHalf);
            _iteratorCount = Mathf.Clamp(_iteratorCount,0,6);
            if(_downSample <= 1){
                if(_iteratorCount > 0){ 
                    //ping pong blit
                    RenderTargetIdentifier ping = _sourceRT;
                    RenderTargetIdentifier pong = _tempRTManager.Request(cmd,_sourceRenderTextureDescriptor);
                    for(var i = 0; i < _iteratorCount ; i ++){
                        //第一个pass,水平blur
                        cmd.Blit(ping,pong,_material,0);
                        //第二个pass,垂直blur
                        cmd.Blit(pong,ping,_material,1);
                    }
                    //clean up
                    _tempRTManager.Cleanup(cmd);
                }
            }else{
                var texDescriptor = _sourceRenderTextureDescriptor;
                var mipLevel = 0;
                //downsample
                var fromTex = _sourceRT;
                RenderTargetIdentifier upTex = _sourceRT;
                RenderTargetIdentifier downTex;
                while(mipLevel < _downSample - 1){
                    texDescriptor.width = texDescriptor.width >> 1;
                    texDescriptor.height = texDescriptor.height >> 1;
                    downTex = _tempRTManager.Request(cmd,texDescriptor,FilterMode.Bilinear);
                    cmd.Blit(upTex,downTex);
                    if(mipLevel > 0){
                        _tempRTManager.Release(cmd,upTex);
                    }
                    upTex = downTex;
                    mipLevel ++;
                }

                //ping pong blit
                var pingTex = upTex;
                var pongTex = _tempRTManager.Request(cmd,texDescriptor);

                for(var i = 0; i < _iteratorCount ; i ++){
                    //第一个pass,水平blur
                    cmd.Blit(pingTex,pongTex,_material,0);
                    //第二个pass,垂直blur
                    cmd.Blit(pongTex,pingTex,_material,1);
                }
                _tempRTManager.Release(cmd,pongTex);

                //upsample
                cmd.Blit(pingTex,_sourceRT);
                _tempRTManager.Cleanup(cmd);
            }
        }

    }
}
