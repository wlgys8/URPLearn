using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPLearn{

    public class PingPongBlitter{

        private RenderTargetIdentifier _ping;
        private RenderTargetIdentifier _pong;

        public PingPongBlitter(){

        }


        public void Prepare(RenderTargetIdentifier ping,RenderTargetIdentifier pong){
            _ping = ping;
            _pong = pong;
        }


        public void BlitAndSwap(CommandBuffer command,Material material,int pass = 0){
            command.Blit(_ping,_pong,material,pass);
            var temp = _ping;
            _ping = _pong;
            _pong = temp;
        }

        public RenderTargetIdentifier pingRT{
            get{
                return _ping;
            }
        }
    }
}