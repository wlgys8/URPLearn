using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace URPLearn{
    [ExecuteInEditMode]
    public class ReflectPlanar : MonoBehaviour
    {

        private static HashSet<ReflectPlanar> _planars = new HashSet<ReflectPlanar>();

        public static IReadOnlyCollection<ReflectPlanar> activePlanars{
            get{
                return _planars;
            }
        }

        void OnEnable(){
            _planars.Add(this);
        }


        void OnDisable(){
            _planars.Remove(this);
        }
    }
}
