struct GPUNode
{
    float3 position;
    float halfSize;
    int childIndex;
    int voxelData;
};

StructuredBuffer<GPUNode> _SVOBuffer;
float3 _ObjectWorldPos;

struct Bounds
{
    float3 min;
    float3 max;
    float3 center;
};


Bounds GetNodeBounds(GPUNode node) {
    Bounds bounds;
    float halfSize = node.halfSize;//clamp(node.halfSize, 0.5, 1000);
    float3 pos = _ObjectWorldPos + node.position;
    bounds.min = pos - float3(halfSize, halfSize, halfSize);
    bounds.max = pos + float3(halfSize, halfSize, halfSize);
    bounds.center = pos;
   return bounds;
}

bool IsPositionWithinNodeBounds(float3 position, GPUNode node) {
    // Calculate the minimum and maximum corners of the node
    Bounds bounds = GetNodeBounds(node);
    // Check if the position is within the node's bounds
    return all(position >= bounds.min && position <= bounds.max);
}

int DetermineChildIndex(float3 position, GPUNode node)
{
    int index = 0;
    float3 pos = _ObjectWorldPos + node.position;
    if (position.x > pos.x)
        index |= 4;
    if (position.y > pos.y)
        index |= 2;
    if (position.z > pos.z)
        index |= 1;

    return node.childIndex + index;
}

bool IsLeafNode(GPUNode node) {
    if (node.childIndex == -1)
        return true;

    return false;
}

int FindChildIndex(float3 position) {
    int index = 0; // Start with the root node
    int depth = 0;
    while (true) {
        GPUNode node = _SVOBuffer[index];
        if (IsLeafNode(node))
            return index;

        int nextIndex = DetermineChildIndex(position, node);

        // Assuming nextIndex calculation is always valid within buffer range
        // Direct assignment without checking if index has changed
        index = nextIndex;

        // Optional: Include a bailout condition based on a maximum depth to prevent infinite loops
        depth++;
        if (depth > 8) break;
    }
    return -1;
}