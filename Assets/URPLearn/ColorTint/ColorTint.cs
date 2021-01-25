using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{

    [CreateAssetMenu(menuName = "URPLearn/ColorTint")]
    public class ColorTint : PostProcessingEffect
    {

        [SerializeField]
        private Shader _shader;

        private Material _material;

        [SerializeField]
        private Color _color;

        void OnValidate(){
            if(_material){
                _material.SetColor("_TintColor",_color);
            }
        }

        public override bool Render(CommandBuffer cmd, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier dst)
        {
            if(!_shader){
                return false;
            }
            if(_material == null){
                _material = new Material(_shader);
                _material.SetColor("_TintColor",_color);
            }
            cmd.Blit(source,dst,_material);
            return true;
        }
    }
}
