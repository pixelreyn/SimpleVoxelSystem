using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Threading.Tasks;
using System.Linq;
using Unity.VisualScripting;





#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelReyn.SimpleVoxelSystem
{
    [ExecuteAlways]
    public class VoxelContainer : MonoBehaviour
    {
        public VoxelObject VoxelObject;
        private Vector3 position;
        public Bounds rootBoundsCentered;



        private void Awake(){
            position = transform.position;
            rootBoundsCentered = voxelObject.root.Bounds;
            rootBoundsCentered.center = transform.position;
            for(int x = -3; x < 3; x++)
            {
                for(int z = -3; z < 3; z++){
                    VoxelObject.AddVoxel(new Voxel(0, false, false), new Vector3(x,0,z));
                }

            }
            VoxelObject.AddVoxel(new Voxel(1, false, false, 7), new Vector3(0,.125f,0));
            
            #if UNITY_EDITOR
                EditorUtility.SetDirty(VoxelObject);
            #endif
        }
        

        void Update() {

        }


        private void OnDrawGizmos(){
            var leaves = voxelObject.GetAllLeaves();
            foreach(var leaf in leaves){
                Gizmos.color = VoxelSettings.Instance.voxelColors[leaf.voxel.Id];
                Gizmos.DrawWireCube(leaf.Position + position, (Vector3.one * leaf.HalfSize * 2));
            }

        }
        public VoxelObject voxelObject {
            get
            {
                    return this.VoxelObject;
            }
        }

        public void DestroyBuffers(){

        }

        private void OnDisable(){
            Shutdown();
        }

        private void OnDestroy(){
            Shutdown();
        }

        private void Shutdown(){
            DestroyBuffers();
        }

        #if UNITY_EDITOR
            [MenuItem("GameObject/SimpleVoxelSystem/Create Voxel Container", false, 10)]
            private static void CreateVoxelWorld(MenuCommand menuCommand)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Voxel Container";
                go.AddComponent<VoxelContainer>();
                GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
                Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
                Selection.activeObject = go;
            }
        #endif
    }
}