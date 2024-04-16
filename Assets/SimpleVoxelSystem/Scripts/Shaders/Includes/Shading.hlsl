#include "Functions.hlsl"
float3 _LightPositionWS;
StructuredBuffer<float4> _VoxelColors;

float3 DecodeColor(int voxelData) {
    if (voxelData != -1){
        int id = voxelData & 0x3F;
        return _VoxelColors[id].rgb;
    }
    return float3(1,0,0);
}

float3 ApproximateNormalsGPT(float3 hitPosition, GPUNode node) {
    Bounds bounds = GetNodeBounds(node);
    float3 cellCenter = (bounds.min + bounds.max) * 0.5;
    float3 localPos = hitPosition - cellCenter; // Localize the hit position to the cell center
    float3 extents = (bounds.max - bounds.min) * 0.5; // Half-extents of the AABB

    float3 normal = float3(0.0, 0.0, 1.0); // Default normal pointing towards +Z
    float maxDist = -1.0;

    // Iterate over each axis to find which face of the AABB the hit is closest to
    for (int i = 0; i < 3; ++i) {
        float distToMinFace = abs(localPos[i] - (-extents[i]));
        float distToMaxFace = abs(localPos[i] - extents[i]);

        if (distToMinFace > maxDist) {
            maxDist = distToMinFace;
            normal = float3(0.0, 0.0, 0.0);
            if (i == 0)
                normal.x = -1.0;
            if (i == 1)
                normal.y = -1.0;
            if (i == 2)
                normal.z = -1.0;
        }

        if (distToMaxFace > maxDist) {
            maxDist = distToMaxFace;
            normal = float3(0.0, 0.0, 0.0);
            if (i == 0)
                normal.x = 1.0;
            if (i == 1)
                normal.y = 1.0;
            if (i == 2)
                normal.z = 1.0;
        }
    }

    return normalize(-normal);
}

half4 ShadeVoxel(GPUNode node, float3 position, float3 rayDir)
{
    // Decode voxel data. This will depend on your encoding scheme.
    // For simplicity, assume voxelData encodes a basic color.
    float3 color = DecodeColor(node.voxelData);

    // Simple shading based on normal direction and light direction
    float3 normal = ApproximateNormalsGPT(position, node); // Implement based on your SVO data
    float3 lightDir = normalize(_LightPositionWS - position); // Assuming a directional light source

    float NdotL = max(dot(normal, lightDir), 0.0);
    float3 diffuse = color * NdotL;

    return half4(diffuse, 1); // Assuming opaque voxels
}