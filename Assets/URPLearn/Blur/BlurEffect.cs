using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{

    [CreateAssetMenu(menuName = "URPLearn/BlurEffect")]
    public class BlurEffect : PostProcessingEffect
    {
        
        private Material _material;

        [SerializeField]
        private Shader _shader;

        [SerializeField]
        private int _iteratorCount = 1;

        [SerializeField]
        private float _blurRadius = 1;


        public override void Render(CommandBuffer cmd, ref RenderingData renderingData,PostProcessingRenderContext context)
        {
            if(!_shader){
                return;
            }
            if(!_material){
                _material = new Material(_shader);
            }
            _material.SetFloat("_BlurRadius",_blurRadius);
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
