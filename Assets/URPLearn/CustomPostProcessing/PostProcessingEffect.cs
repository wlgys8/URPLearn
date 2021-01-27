using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{
    public abstract class PostProcessingEffect : ScriptableObject
    {
        
        public bool active = true;
        
        public abstract void Render(CommandBuffer cmd, ref RenderingData renderingData,PostProcessingRenderContext context);
    }
}
