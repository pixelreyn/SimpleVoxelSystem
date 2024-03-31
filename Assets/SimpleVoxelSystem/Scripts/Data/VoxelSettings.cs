using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelReyn.SimpleVoxelSystem {
    [CreateAssetMenu(fileName = "VoxelSettings", menuName = "SimpleVoxelSystem/VoxelSettings", order = 1)]
    public class VoxelSettings : ScriptableObject
    {
        public Color[] voxelColors; // Array to hold colors for voxel IDs

        private static VoxelSettings _instance;
        public static VoxelSettings Instance {
            get{
                if(_instance == null)
                    _instance = Resources.Load<VoxelSettings>("VoxelSettings");
                return _instance;
            }
        }
    }
}