using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Threading.Tasks;
using System.Linq;
using Unity.VisualScripting;
using System;






#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PixelReyn.SimpleVoxelSystem
{
    [ExecuteAlways]
    public class VoxelContainer : MonoBehaviour
    {
        public VoxelObject VoxelObject;
        private VoxelObject runtimeObject;
        private Vector3 position;
        public Bounds rootBoundsCentered;

        public Material rayMarchingMaterial;
        public Light sceneLight; // Assign your main light here
        public bool Dynamic = false;
        ComputeBuffer octreeBuffer;
        ComputeBuffer colorBuffer;
        private GPUNode[] nodes;
        private bool IsInitialized = false;
        public bool IsPlaying = false;

        //small GUI helpers:
        public Vector3 debugPosVec3 = Vector3.zero;
        public Vector3 debugPos2Vec3 = Vector3.zero;
        public float debugSize = 0;

        private void Awake(){
            position = transform.position;
            rootBoundsCentered = voxelObject.root.Bounds;
            rootBoundsCentered.center = transform.position;
            for(int x = -3; x < 3; x++)
            {
                for(int z = -3; z < 3; z++){
                    voxelObject.AddVoxel(new Voxel(0, false, false), new Vector3(x,0,z));
                }

            }
            voxelObject.AddVoxel(new Voxel(1, false, false, 7), new Vector3(0,.125f,0));
            
            #if UNITY_EDITOR
                EditorUtility.SetDirty(voxelObject);
            #endif
            
            if(Application.isPlaying)
                InitializeBuffers();
        }
        

        void Update() {
            if (rayMarchingMaterial)
            {
                if(nodes == null)
                    InitializeBuffers();

                if(sceneLight)
                    rayMarchingMaterial.SetVector("_LightPositionWS", sceneLight.transform.position);

                rayMarchingMaterial.SetVector("_ObjectWorldPos", transform.position);
            }
        }


        private void OnDrawGizmos(){
            var leaves = voxelObject.GetAllLeaves();
            foreach(var leaf in leaves){
                Gizmos.color = VoxelSettings.Instance.voxelColors[leaf.voxel.Id];
                Gizmos.DrawWireCube(leaf.Position + position, (Vector3.one * leaf.HalfSize * 2));
            }

            //draw "Add" location hinting:
            if (debugSize > 0)
            {
                Gizmos.DrawWireCube(debugPosVec3, (Vector3.one * debugSize));
                Gizmos.DrawSphere(debugPos2Vec3, 0.2f * debugSize);
            }
        }

        public void InitializeBuffers(bool force = false){
            if(IsInitialized && !force)
                return;

            if(Application.isPlaying && runtimeObject == null){
                runtimeObject = Instantiate(VoxelObject);
            }
            IsPlaying = Application.isPlaying;

            nodes = voxelObject.FlattenForGPU();
            
            octreeBuffer?.Release();
            colorBuffer?.Release();

            if(nodes.Length == 0)
                return;

            octreeBuffer = new ComputeBuffer(nodes.Length, 24, ComputeBufferType.Default);
            octreeBuffer.SetData(nodes);

            colorBuffer = new ComputeBuffer(VoxelSettings.Instance.voxelColors.Length, 16, ComputeBufferType.Default);
            colorBuffer.SetData(VoxelSettings.Instance.voxelColors);
            if(rayMarchingMaterial){
            
                if(!rayMarchingMaterial.name.Contains("Clone"))
                    rayMarchingMaterial = Instantiate(rayMarchingMaterial);

                rayMarchingMaterial.SetBuffer("_SVOBuffer", octreeBuffer);
                rayMarchingMaterial.SetBuffer("_VoxelColors", colorBuffer);

                GetComponent<MeshRenderer>().material = rayMarchingMaterial;
            }
            IsInitialized = true;
        }
        bool rayBoxIntersect(float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax, out float tMin) {
            float3 invDir = 1.0f / rayDir;
            float3 t0 = (boxMin - rayOrigin) * invDir;
            float3 t1 = (boxMax - rayOrigin) * invDir;
        
            float3 tmin = math.min(t0, t1);
            float3 tmax = math.max(t0, t1);
        
            float tentry = math.max(tmin.x, math.max(tmin.y, tmin.z));
            float texit = math.min(tmax.x, math.min(tmax.y, tmax.z));
        
            // Check if the ray originates inside the box
            if (tentry < 0.0f && texit > 0.0f) {
                tMin = 0.0f; // The ray originates inside the box
                return true;
            }
        
            // Check for intersection
            if (tentry > texit || texit < 0.0) {
                tMin = 0f;
                return false; // No intersection
            }
        
            tMin = tentry; // Distance to the nearest intersection
            return true;
        }

        public (bool, Vector3, Vector3, Vector3) RayIntersect(Ray ray){
            if(nodes.Length == 0 || nodes[0].childIndex == -1)
                return (false, Vector3.zero, Vector3.zero, Vector3.zero);
            int candidateCount = 1;
            bool hit = false;
            Vector3 hitNormal = Vector3.zero;
            Vector3 hitCenter = Vector3.zero;

            Candidate[] candidateNodes = new Candidate[16];
            Candidate rootC;
            rootC.nodeIndex = 0;
            rootC.distance = 0;
            candidateNodes[0] = rootC;

            while (candidateCount > 0) {
                int closestCandidateIndex = 0; // Assume the first candidate is the closest
                // Find the closest candidate (could optimize this part by maintaining a sorted order of candidates)
                for (int i = 1; i < candidateCount; ++i) {
                    if (candidateNodes[i].distance < candidateNodes[closestCandidateIndex].distance) {
                        closestCandidateIndex = i;
                    }
                }
                
                Candidate closestCandidate = candidateNodes[closestCandidateIndex];
                // Remove the closest candidate from the list by swapping with the last and decrementing the count
                candidateNodes[closestCandidateIndex] = candidateNodes[candidateCount - 1];
                candidateCount--;
            
                GPUNode currentNode = nodes[closestCandidate.nodeIndex];

                // If currentNode is a leaf with voxel data, perform shading and exit
                if (currentNode.voxelData != -1){
                    Vector3 currentPos = ray.origin + closestCandidate.distance * ray.direction;
                    hit = true;
                    hitCenter = currentNode.position;
                    hitNormal = (transform.position + currentNode.position - currentPos).normalized;
                    return (hit, hitNormal, hitCenter, currentPos - transform.position);
                }
            
                // If not a leaf, add child nodes to candidates

                for (int i = 0; i < 8; ++i) {
                    if(currentNode.childIndex + i >= nodes.Length || currentNode.childIndex + i < 0)
                        continue;

                    GPUNode childNode = nodes[currentNode.childIndex + i];
                    VBounds childBounds = new VBounds(childNode, transform.position);

                    //Skip if this is a leaf with no data
                    if(childNode.childIndex == -1 && childNode.voxelData == -1)
                        continue;
                        
                    float distToChild;
                    if (rayBoxIntersect(ray.origin, ray.direction, childBounds.min, childBounds.max, out distToChild)) {
                        // Add childNode to candidates if it potentially intersects the ray
                        if (candidateCount < 16) {
                            Candidate newCandidate;
                            newCandidate.nodeIndex = currentNode.childIndex + i;
                            newCandidate.distance = distToChild;
                            candidateNodes[candidateCount++] = newCandidate;
                        }
                    }
                }
            }
            
            return (hit, hitNormal, hitCenter, Vector3.zero);
        }

        public Vector3 getOffsetToNewCentreClosest(Vector3 proposedPt, Vector3 hitCen, int intSize)
        {
            Vector3 offsetToNewCentre = Vector3.zero;
            float invSize = 1f / intSize;

            int axis = 1;  //up
            float halfSizeHit = -1f;//, total = 0f;
            Vector3 axisNormal;// = Vector3.zero;
            Vector3 neigbourCenOffset = Vector3.zero;
            //bool dirPos = true; //up, not down
            for (int i = 0; i < 3; i++)
            {
                neigbourCenOffset[i] = proposedPt[i] - hitCen[i];
                if (Math.Abs(neigbourCenOffset[i]) > halfSizeHit)      //use normalizedNormal instead?
                {
                    axis = i;
                    axisNormal = Vector3.zero;
                    axisNormal[i] = 1f;
                    //    dirPos = (raymarch.hitPt[i] > raymarch.hitCen[i]);
                    halfSizeHit = Math.Abs(proposedPt[i] - hitCen[i]);      //halfsize
                }
                //total += Math.Abs(proposedPt[i] - hitCen[i]);
            }

            // Align hitPoint to grid
            for (int i = 0; i < 3; i++)
            {
                if (i == axis)
                {
                    offsetToNewCentre[i] = (neigbourCenOffset[i] < 0 ? -1 : 1) * (halfSizeHit + invSize / 2);
                }
                else
                {
                    //round to grid:
                    offsetToNewCentre[i] = (float)Math.Ceiling((neigbourCenOffset[i] + halfSizeHit) * intSize) / intSize - halfSizeHit - invSize / 2;
                }
            }

            return offsetToNewCentre;
        }

        public void DestroyBuffers(){
            octreeBuffer?.Dispose();
            colorBuffer?.Dispose();
            octreeBuffer = null;
            colorBuffer = null;
            IsInitialized = false;
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

        struct Candidate {
            public int nodeIndex;
            public float distance;
        }
        public class VBounds
        {
            public float3 min;
            public float3 max;
            public float3 center;

            public bool Contains(Vector3 position){
                return position.x >= min.x && position.x <= max.x && 
                position.y >= min.y && position.y <= max.y && 
                position.z >= min.z && position.z <= max.z;
            }

            public VBounds (GPUNode node, Vector3 worldPos) {
                float halfSize = node.halfSize;//clamp(node.halfSize, 0.5, 1000);
                float3 pos = worldPos + node.position;
                min = pos - new float3(halfSize, halfSize, halfSize);
                max = pos + new float3(halfSize, halfSize, halfSize);
                center = pos;
            }
            public VBounds(Vector3 center, float size) {
                float halfSize = size / 2;
                min = -new float3(halfSize, halfSize, halfSize);
                max = new float3(halfSize, halfSize, halfSize);
                this.center = center;
            }
        };

        public VoxelObject voxelObject {
            get
            {
                    return VoxelObject;
            }
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