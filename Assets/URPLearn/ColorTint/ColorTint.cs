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
                UpdateMaterialProperties();
            }
        }

        private void UpdateMaterialProperties(){
             _material.SetColor("_TintColor",_color);
        }

        public override void Render(CommandBuffer cmd, ref RenderingData renderingData, PostProcessingRenderContext context)
        {
            if(!_shader){
                return;
            }
            if(_material == null){
                _material = new Material(_shader);
                UpdateMaterialProperties();
            }
            context.BlitAndSwap(cmd,_material);
        }
    }
}
