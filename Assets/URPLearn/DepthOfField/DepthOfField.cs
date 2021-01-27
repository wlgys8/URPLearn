using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{

    [CreateAssetMenu(menuName = "URPLearn/DepthOfField")]
    public class DepthOfField : PostProcessingEffect
    {

        [SerializeField]
        private Shader _shader;

        private Material _material;

        [SerializeField]
        private int _iteratorCount = 1;

        [Tooltip("相机对焦的物距，单位m，在公式中记为u")]
        [SerializeField]
        private float _focusDistance = 1; //对焦物距，单位米

        [Tooltip("相机的焦距(这里其实应该是成像胶片到镜头的距离),在公式中记为v")]
        [SerializeField]
        private float _focalLength; //焦距(其实是像距) 单位毫米

        [Tooltip("相机的光圈值F = f / 镜片直径")]
        [SerializeField]
        private float _aperture = 6.3f; //单位毫米


        private void OnValidate() {
            _aperture = Mathf.Clamp(_aperture,1,32);
            _focalLength = Mathf.Clamp(_focalLength,1,300);
            _focusDistance = Mathf.Max(_focusDistance,0.1f);
        }

        /// <summary>
        /// 焦距倒数
        /// </summary>
        private float rcpf{
            get{
                return (0.001f / _focusDistance + 1 / _focalLength);
            }
        }

        /// <summary>
        /// 计算成像距离
        /// </summary>
        private float CalculateImageDistance(float objDis){
            return 1 / (rcpf - 0.001f / objDis);
        }

        /// <summary>
        /// 计算弥散圆直径
        /// </summary>
        private float CalculateConfusionCircleDiam(float objDis){
            var imageDis = CalculateImageDistance(objDis);
            return Mathf.Abs(imageDis - _focalLength)  / (_focalLength * rcpf * _aperture) ;
        }

        /// <summary>
        /// 光圈直径
        /// </summary>
        private float apertureDiam{
            get{
                return (1 / (rcpf * _aperture));
            }
        }

        public override void Render(CommandBuffer cmd, ref RenderingData renderingData, PostProcessingRenderContext context)
        {
            if(!_shader){
                return;
            }
            if(!_material){
                _material = new Material(_shader);
            }

            var DOFParams = new Vector4(
                rcpf,
                _focalLength,
                1 / (_focalLength * rcpf * _aperture),
                0
            );
            _material.SetVector("_DOFParams",DOFParams);
            _iteratorCount = Mathf.Clamp(_iteratorCount,1,5);
            for(var i = 0; i < _iteratorCount; i ++){
                //水平blur
                context.BlitAndSwap(cmd,_material,0);
                //垂直blur
                context.BlitAndSwap(cmd,_material,1);
            }
        }
    }
}
