using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace URPLearn{

    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public class GroundGenerator : MonoBehaviour
    {   
        [SerializeField]
        private int _size = 10;

        private void Awake(){
            this.RebuildMesh();
        }        

        [ContextMenu("RebuildMesh")]
        private void RebuildMesh(){
            var meshFilter = GetComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateMesh(_size);
        }

        private static Mesh CreateMesh(int size = 100){
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for(var x = 0; x <= size; x ++){
                for(var z = 0; z <= size; z ++){
                    var height = 0;//Mathf.PerlinNoise(x / 10f,z/10f) * 5;
                    var v = new Vector3(x,height,z);
                    vertices.Add(v);
                }
            }
            for(var x = 0; x < size; x ++){
                for(var z = 0; z < size; z ++){
                    var i1 = x * (size + 1) + z;
                    var i2 = (x + 1) * (size + 1) + z;
                    var i3 = x * (size + 1) + z + 1;
                    var i4 = (x + 1) * (size + 1) + z + 1;
                    indices.Add(i1);
                    indices.Add(i3);
                    indices.Add(i2);
                    indices.Add(i2);
                    indices.Add(i3);
                    indices.Add(i4);
                }
            }
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices,MeshTopology.Triangles,0,true);
            mesh.RecalculateNormals();
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}
