using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{

    [CreateAssetMenu(menuName = "URPLearn/BlurEffect")]
    public class BlurEffect : PostProcessingEffect
    {

        public enum BlurType{
            None,
            Box,
            BoxBilinear,
            Gaussian,
            GaussianBilinear,
        }


        
        private Material _material;

        [SerializeField]
        private Shader _boxBlurShader;

        [SerializeField]
        private Shader _gaussianBlurShader;

        [SerializeField]
        private BlurType _blurType;

        
        
        [SerializeField]
        private int _iteratorCount = 1;

        [Tooltip("Only work for BoxFilter")]
        [SerializeField]
        private int _boxKernelSizeHalf = 2;

        [SerializeField]
        private float _blurScale = 1;

        private Shader _usingShader;


        private void OnValidate() {
            switch(_blurType){
                case BlurType.Box:
                case BlurType.BoxBilinear:
                _usingShader = _boxBlurShader;
                break;
                case BlurType.Gaussian:
                case BlurType.GaussianBilinear:
                _usingShader = _gaussianBlurShader;
                break;
                default:
                _usingShader = null;
                break;
            }    
            _boxKernelSizeHalf = Mathf.Clamp(_boxKernelSizeHalf,1,4);
            _blurScale = Mathf.Clamp(_blurScale,1,5);
        }

        public override void Render(CommandBuffer cmd, ref RenderingData renderingData,PostProcessingRenderContext context)
        {
            if(!_boxBlurShader){
                return;
            }
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
            _material.SetFloat("_BlurScale",_blurScale);
            _material.SetInt("_KernelSize",_boxKernelSizeHalf);
            _iteratorCount = Mathf.Clamp(_iteratorCount,1,6);
            for(var i = 0; i < _iteratorCount ; i ++){
                //第一个pass,水平blur
                context.BlitAndSwap(cmd,_material,0);
                //第二个pass,垂直blur
                context.BlitAndSwap(cmd,_material,1);
            }
        }
    }
}
