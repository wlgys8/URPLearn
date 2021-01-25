using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{
    public abstract class PostProcessingEffect : ScriptableObject
    {
        
        public bool active;
        
        public abstract bool Render(CommandBuffer cmd, ref RenderingData renderingData,RenderTargetIdentifier source,RenderTargetIdentifier dst);
    }
}
