using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace PixelReyn.SimpleVoxelSystem
{
[CreateAssetMenu(fileName = "VoxelObject", menuName = "SimpleVoxelSystem/VoxelObject", order = 1)]
    public class VoxelObject : ScriptableObject, ISerializationCallbackReceiver
    {
        public OctreeNode root;

        [HideInInspector,SerializeField] private List<SerializedNode> serializedNodes = new List<SerializedNode>();

        [SerializeField] private Bounds bounds;

        public void Initialize(Bounds bounds)
        {
            // Initialize the root with the initial bounds
            root = new OctreeNode(bounds);
            this.bounds = bounds;
        }

        public void AddVoxel(Voxel voxel, Vector3 position)
        {
            if (!root.Bounds.Contains(position))
                return;

            // Add the voxel to the octree
            root.Add(voxel, position);
        }        
        public Voxel GetVoxel(Vector3 position)
        {
            if (!root.Bounds.Contains(position))
                return null;

            // Add the voxel to the octree
            return root.Query(position);
        }

        public bool RemoveVoxel(Vector3 position, out Voxel voxel)
        {
            if (root.Bounds.Contains(position))
                return root.Remove(position, out voxel);
            voxel = null;
            return false;
        }


        public List<OctreeNode> GetAllLeaves()
        {
            List<OctreeNode> leaves = new List<OctreeNode>();
            if (root != null)
            {
                root.GetAllLeaves(leaves);
            }
            return leaves;
        }

        public void OnBeforeSerialize()
        {
            serializedNodes.Clear();
            if (root != null)
                FlattenTree(root, root.Bounds); // Start with the root node
        }

        public void OnAfterDeserialize()
        {
            // Initialize or clear the root node. Adjust the initial bounds as necessary.
            // This bounds should be large enough to encompass all possible voxel positions
            // or dynamically adjust based on serialized data.
            root = new OctreeNode(bounds);

            // Recreate each voxel in the tree
            foreach (var serializedNode in serializedNodes)
            {
                Voxel voxel = new Voxel(serializedNode.voxelData); // Assume constructor from data
                root.Add(voxel, serializedNode.position); // Let the tree structure itself
            }
        }


        private void FlattenTree(OctreeNode node, Bounds bounds)
        {
            if (node == null) return;

            // Simplification: Assume if node has voxel, it's a leaf (for this example)
            if (node.voxel != null)
            {
                serializedNodes.Add(new SerializedNode
                {
                    position = bounds.center,
                    size = bounds.size.x, // Assuming cubic bounds for simplicity
                    voxelData = node.voxel.Data
                });
            }

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    if (child != null)
                    {
                        FlattenTree(child, child.Bounds);
                    }
                }
            }
        }

        public void Clear(float minSize = 1f){
            root = null;
            serializedNodes.Clear();
            Initialize(bounds);
            #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        public void Clear(Bounds bounds, float minSize = 1f){
            root = null;
            serializedNodes.Clear();
            Initialize(bounds);
            #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
    }
    [System.Serializable]
    public struct SerializedNode
    {
        public Vector3 position;
        public float size; // Assuming cubic nodes for simplicity
        public short voxelData; // Voxel data, could be more complex or a reference
    }
}