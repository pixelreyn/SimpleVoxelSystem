using UnityEngine;
using System.Collections.Generic;

namespace PixelReyn.SimpleVoxelSystem
{
    public class OctreeNode
    {
        public OctreeNode[] children;
        public Voxel voxel;
        private Bounds bounds; // Each node has its own bounds


        public OctreeNode(Bounds bounds)
        {
            this.bounds = bounds;
        }

        public void Add(Voxel voxel, Vector3 position)
        {
            // First, check if the position is within the bounds of this node
            if (!bounds.Contains(position)) return;

            // Check if the node is already at the minimum size (1 unit bounds)
            // or if it has reached the maximum depth you've defined.
            float voxelSize = 1f / ((float)(voxel.Size + 1));
            bool atMinimumSize = bounds.size.x <= voxelSize && bounds.size.y <= voxelSize && bounds.size.z <= voxelSize;
            if (atMinimumSize)
            {
                // Store the voxel here as we can't subdivide further
                this.voxel = voxel;
                return;
            }

            // If the node can be subdivided further (not at minimum size), check if it already has children
            if (children == null || children.Length != 8)
            {
                // Split the node if it's not already split
                SplitNode();
            }
            
            // Now delegate the voxel to the correct child node
            int index = DetermineChildIndex(position);
            if(children == null || children[index] == null || voxel == null)
                return;

            children[index].Add(voxel, position);
        }

        private void SplitNode()
        {
            children = new OctreeNode[8];
            for (int i = 0; i < 8; i++)
            {
                Vector3 childCenter = bounds.center;
                childCenter.x += (i & 4) == 4 ? bounds.extents.x / 2 : -bounds.extents.x / 2;
                childCenter.y += (i & 2) == 2 ? bounds.extents.y / 2 : -bounds.extents.y / 2;
                childCenter.z += (i & 1) == 1 ? bounds.extents.z / 2 : -bounds.extents.z / 2;
                Bounds childBounds = new Bounds(childCenter, bounds.size / 2);
                children[i] = new OctreeNode(childBounds);
            }
        }

        public bool Remove(Vector3 position, out Voxel voxel)

        {
            if (children == null)
            {
                if (this.Position == position)
                {
                    voxel = this.voxel;
                    this.voxel = null;
                    return true;
                }
            }
            else
            {
                int index = DetermineChildIndex(position);
                return children[index].Remove(position, out voxel);
            }
            voxel = null;
            return false;
        }

        public Voxel Query(Vector3 position)
        {
            if (children == null || children.Length != 8)
            {
                if (Position == position || (voxel != null && bounds.Contains(position)))
                {
                    return voxel;
                }
            }
            else
            {
                int index = DetermineChildIndex(position);
                return children[index].Query(position);
            }

            return null;
        }

        private int DetermineChildIndex(Vector3 position)
        {
            int index = 0;

            if (position.x > bounds.center.x)
                index |= 4;
            if (position.y > bounds.center.y)
                index |= 2;
            if (position.z > bounds.center.z)
                index |= 1;

            return index;
        }

        public void GetAllLeaves(List<OctreeNode> leaves)
        {
            if (children == null) // This node is a leaf
            {
                if (voxel != null) // Ensure the leaf has voxel data
                {
                    leaves.Add(this);
                }
            }
            else
            {
                // If not a leaf, recurse into children
                foreach (var child in children)
                {
                    child.GetAllLeaves(leaves);
                }
            }
        }

        public Bounds Bounds
        {
            get => bounds;
            set => bounds = value;
        }
        public Vector3 Position
        {
            get => Bounds.center;
        }

        public float HalfSize => bounds.size.x / 2;
    }
}