using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{

    [CreateAssetMenu(menuName = "URPLearn/Bloom")]
    public class Bloom : PostProcessingEffect
    {

        [SerializeField]
        private Shader _shader;

        private Material _material;

        [SerializeField]
        private int downSample = 1;

        [SerializeField]
        private int _blurIteratorCount = 1;

        [SerializeField]
        [Range(0,1)]
        private float _threshold = 0.8f;

        [SerializeField]
        private bool _debug;

        private BlurBlitter _blurBlitter = new BlurBlitter();


        public override void Render(CommandBuffer cmd, ref RenderingData renderingData, PostProcessingRenderContext context)
        {

            if(!_shader){
                return;
            }
            if(!_material){
                _material = new Material(_shader);
            }

            if(_debug){
                _material.EnableKeyword("_BloomDebug");
            }else{
                _material.DisableKeyword("_BloomDebug");
            }

            _material.SetFloat("_Threshold",_threshold);

            var descriptor = context.sourceRenderTextureDescriptor;

            var temp1 =  context.GetTemporaryRT(cmd,descriptor,FilterMode.Bilinear);

            //first pass，提取光亮部分
            cmd.Blit(context.activeRenderTarget,temp1,_material,0);

            //模糊处理
            _blurBlitter.SetSource(temp1,descriptor);

            _blurBlitter.downSample = downSample;
            _blurBlitter.iteratorCount = _blurIteratorCount;
            _blurBlitter.blurType = BlurType.Box;

            _blurBlitter.Render(cmd);

            cmd.SetGlobalTexture("_BloomTex",temp1);

            //combine
            context.BlitAndSwap(cmd,_material,3);

            context.ReleaseTemporaryRT(cmd,temp1);

        }
    }
}
