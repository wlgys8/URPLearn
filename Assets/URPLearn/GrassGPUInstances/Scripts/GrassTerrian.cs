using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace URPLearn{

    [ExecuteInEditMode]
    public class GrassTerrian : MonoBehaviour
    {


        private static HashSet<GrassTerrian> _actives = new HashSet<GrassTerrian>();

        public static IReadOnlyCollection<GrassTerrian> actives{
            get{
                return _actives;
            }
        }

        [SerializeField]
        private Material _material;

        [SerializeField]
        private Vector2 _grassQuadSize = new Vector2(0.1f,0.6f);

        [SerializeField]
        private int _grassCountPerMeter = 100;

        public Material material{
            get{
                return _material;
            }
        }


        private int _seed;

        private ComputeBuffer _grassBuffer;

        private int _grassCount;

        private void Awake() {
             _seed = System.Guid.NewGuid().GetHashCode();
        }


        public int grassCount{
            get{
                return _grassCount;
            }
        }
        
        public ComputeBuffer grassBuffer{
            get{
                if(_grassBuffer != null){
                    return _grassBuffer;
                }
                var filter = GetComponent<MeshFilter>();
                var terrianMesh = filter.sharedMesh;
                var matrix = transform.localToWorldMatrix;
                var grassIndex = 0;
                List<GrassInfo> grassInfos = new List<GrassInfo>();
                var maxGrassCount = 10000;
                Random.InitState(_seed);

                var indices = terrianMesh.triangles;
                var vertices = terrianMesh.vertices;

                for(var j = 0; j < indices.Length / 3; j ++){
                    var index1 = indices[j * 3];
                    var index2 = indices[j * 3 + 1];
                    var index3 = indices[j * 3 + 2];
                    var v1 = vertices[index1];
                    var v2 = vertices[index2];
                    var v3 = vertices[index3];

                    //面得到法向
                    var normal = GrassUtil.GetFaceNormal(v1,v2,v3);

                    //计算up到faceNormal的旋转四元数
                    var upToNormal = Quaternion.FromToRotation(Vector3.up,normal);

                    //三角面积
                    var arena = GrassUtil.GetAreaOfTriangle(v1,v2,v3);

                    //计算在该三角面中，需要种植的数量
                    var countPerTriangle = Mathf.Max(1,_grassCountPerMeter * arena);

                    for(var i = 0; i < countPerTriangle; i ++){
                        
                        var positionInTerrian = GrassUtil.RandomPointInsideTriangle(v1,v2,v3);
                        float rot = Random.Range(0,180);
                        var localToTerrian = Matrix4x4.TRS(positionInTerrian,  upToNormal * Quaternion.Euler(0,rot,0) ,Vector3.one);

                        Vector2 texScale = Vector2.one;
                        Vector2 texOffset = Vector2.zero;
                        Vector4 texParams = new Vector4(texScale.x,texScale.y,texOffset.x,texOffset.y);
                        

                        var grassInfo = new GrassInfo(){
                            localToTerrian = localToTerrian,
                            texParams = texParams
                        };
                        grassInfos.Add(grassInfo);
                        grassIndex ++;
                        if(grassIndex >= maxGrassCount){
                            break;
                        }
                    }
                    if(grassIndex >= maxGrassCount){
                        break;
                    }
                }
               
                _grassCount = grassIndex;
                _grassBuffer = new ComputeBuffer(_grassCount,64 + 16);
                _grassBuffer.SetData(grassInfos);
                return _grassBuffer;
            }
        }

        private MaterialPropertyBlock _materialBlock;
        
        public void UpdateMaterialProperties(){
            materialPropertyBlock.SetMatrix(ShaderProperties.TerrianLocalToWorld,transform.localToWorldMatrix);
            materialPropertyBlock.SetBuffer(ShaderProperties.GrassInfos,grassBuffer);
            materialPropertyBlock.SetVector(ShaderProperties.GrassQuadSize,_grassQuadSize);
        }

        public MaterialPropertyBlock materialPropertyBlock{
            get{
                if(_materialBlock == null){
                    _materialBlock = new MaterialPropertyBlock();
                }
                return _materialBlock;
            }
        }

        [ContextMenu("ForceRebuildGrassInfoBuffer")]
        private void ForceUpdateGrassBuffer(){
            if(_grassBuffer != null){
                _grassBuffer.Dispose();
                _grassBuffer = null;
            }
            UpdateMaterialProperties();
        }

        void OnEnable(){
            _actives.Add(this);
        }

        void OnDisable(){
            _actives.Remove(this);
            if(_grassBuffer != null){
                _grassBuffer.Dispose();
                _grassBuffer = null;
            }
        }


        public struct GrassInfo{
            public Matrix4x4 localToTerrian;
            public Vector4 texParams;
        }


        private class ShaderProperties{

            public static readonly int TerrianLocalToWorld = Shader.PropertyToID("_TerrianLocalToWorld");
            public static readonly int GrassInfos = Shader.PropertyToID("_GrassInfos");
            public static readonly int GrassQuadSize = Shader.PropertyToID("_GrassQuadSize");

        }




    }
}
