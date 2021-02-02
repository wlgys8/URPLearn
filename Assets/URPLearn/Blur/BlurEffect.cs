using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{

    [CreateAssetMenu(menuName = "URPLearn/BlurEffect")]
    public class BlurEffect : PostProcessingEffect
    {

        [SerializeField]
        private BlurType _blurType;
        
        [SerializeField]
        private int _iteratorCount = 1;

        [SerializeField]
        private int _downSample = 1;

        [Tooltip("Only work for BoxFilter")]
        [SerializeField]
        private int _boxKernelSizeHalf = 2;

        [SerializeField]
        private float _blurScale = 1;

        private BlurBlitter _blurBlitter = new BlurBlitter();


        private void OnValidate() {
            _blurBlitter.blurType = _blurType;
            _blurBlitter.downSample = _downSample;
            _blurBlitter.iteratorCount = _iteratorCount;
            _blurBlitter.blurScale = _blurScale;
            _blurBlitter.boxKernelSizeHalf = _boxKernelSizeHalf;
        }


        public override void Render(CommandBuffer cmd, ref RenderingData renderingData,PostProcessingRenderContext context)
        {
            _blurBlitter.SetSource(context.activeRenderTarget,context.sourceRenderTextureDescriptor);
            _blurBlitter.Render(cmd);
        }
   
    }
}
