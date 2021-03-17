using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{

    
    public class GrassRenderFeature : ScriptableRendererFeature
    {

        private GrassRenderPass _pass = null;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if(cameraData.renderType == CameraRenderType.Base){
                renderer.EnqueuePass(_pass);
            }
        }

        public override void Create()
        {
            _pass = new GrassRenderPass();
        }




        public class GrassRenderPass : ScriptableRenderPass
        {
            public GrassRenderPass(){
                this.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            }

            private const string NameOfCommandBuffer = "Grass";

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
              
                var cmd = CommandBufferPool.Get(NameOfCommandBuffer);
                try{
                    cmd.Clear();
                    var index = 0;
                    foreach(var grassTerrian in GrassTerrian.actives){
                        if(!grassTerrian){
                            continue;
                        }
                        if(!grassTerrian.material){
                            continue;
                        }
                        grassTerrian.UpdateMaterialProperties();
                        cmd.DrawMeshInstancedProcedural(GrassUtil.unitMesh,0,grassTerrian.material,0,grassTerrian.grassCount,grassTerrian.materialPropertyBlock);
                        index ++;
                    }
                    context.ExecuteCommandBuffer(cmd);
                }finally{
                    cmd.Release();
                }


            }
        }
    }
}
