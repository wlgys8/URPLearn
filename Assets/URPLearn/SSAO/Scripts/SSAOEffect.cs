using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{

    [CreateAssetMenu(menuName = "URPLearn/SSAO")]
    public class SSAOEffect : PostProcessingEffect
    {
        [SerializeField]
        private Shader _shader;

        [SerializeField]
        private float _contrast = 1;

        [SerializeField]
        private float _atten = 1;

        [SerializeField]
        private float _sampleRadius = 0.3f;

        [SerializeField]
        private int _sampleCount = 4;

        private Material _material;

        [SerializeField]
        private bool _debug = false;

        private void OnValidate() {
            this.UpdateMaterialProperties();
        }

        private void UpdateMaterialProperties(){
            if(_material){
                _material.SetFloat("_Atten",_atten);
                _material.SetFloat("_Contrast",_contrast);
                _material.SetFloat("_SampleRadius",_sampleRadius);
                if(_debug){
                    _material.EnableKeyword("__AO_DEBUG__");
                }else{
                    _material.DisableKeyword("__AO_DEBUG__");
                }
                _sampleCount = Mathf.Clamp(_sampleCount,1,32);
                _material.SetInt("_SampleCount",_sampleCount);
            }              
        }

        public override bool Render(CommandBuffer cmd, ref RenderingData renderingData, RenderTargetIdentifier source,RenderTargetIdentifier dst)
        {
            if(!_shader){
                return false;
            }
            if(_material == null){
                _material = new Material(_shader);
            }
            this.UpdateMaterialProperties();
            var projMatrix = renderingData.cameraData.GetGPUProjectionMatrix();
            _material.SetMatrix("CustomProjMatrix",projMatrix);
            _material.SetMatrix("CustomInvProjMatrix",projMatrix.inverse);
            cmd.Blit(source,dst,_material);
            return true;
        }
    }
}
