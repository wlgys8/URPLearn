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

        [SerializeField]
        private bool _blur = false;

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
        private PingPongBlitter _blitter = new PingPongBlitter();

        public override void Render(CommandBuffer cmd, ref RenderingData renderingData,PostProcessingRenderContext context)
        {
            if(!_shader){
                return;
            }
            if(_material == null){
                _material = new Material(_shader);
            }
            this.UpdateMaterialProperties();
            var projMatrix = renderingData.cameraData.GetGPUProjectionMatrix();
            _material.SetMatrix("CustomProjMatrix",projMatrix);
            _material.SetMatrix("CustomInvProjMatrix",projMatrix.inverse);
            if(_blur){
                _material.EnableKeyword("_Blur");
            }else{
                _material.DisableKeyword("_Blur");
            }
            if(_blur){
                var temp1 = context.GetTemporaryRT(cmd);
                var temp2 = context.GetTemporaryRT(cmd);
                _blitter.Prepare(temp1,temp2);
                //first pass, calculate AO
                _blitter.BlitAndSwap(cmd,_material,0);

                //second pass, blur horizantal
                _blitter.BlitAndSwap(cmd,_material,1);

                //third pass, blur vertical
                _blitter.BlitAndSwap(cmd,_material,2);

                cmd.SetGlobalTexture("_AOTex",_blitter.pingRT);

                context.BlitAndSwap(cmd,_material,3);

                context.ReleaseTemporaryRT(cmd,temp1);
                context.ReleaseTemporaryRT(cmd,temp2);
            }else{
                context.BlitAndSwap(cmd,_material);
            }
        }
    }
}
