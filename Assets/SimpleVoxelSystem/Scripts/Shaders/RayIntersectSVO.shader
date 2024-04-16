Shader "Custom/RayIntersectSVO"
{
    Properties
    {
    }
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Includes/Shading.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float3 hitPoint : TEXCOORD0;
            float3 rayOrigin : TEXCOORD1;
        };

        struct Candidate {
            int nodeIndex;
            float distance;
        };
        

        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
            OUT.hitPoint = mul (unity_ObjectToWorld, IN.positionOS);
            OUT.rayOrigin = _WorldSpaceCameraPos.xyz;
            return OUT;
        }

        bool rayBoxIntersectGPT(float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax, out float tMin) {
            float3 invDir = 1.0 / rayDir;
            float3 t0 = (boxMin - rayOrigin) * invDir;
            float3 t1 = (boxMax - rayOrigin) * invDir;
        
            float3 tmin = min(t0, t1);
            float3 tmax = max(t0, t1);
        
            float tentry = max(tmin.x, max(tmin.y, tmin.z));
            float texit = min(tmax.x, min(tmax.y, tmax.z));
        
            // Check if the ray originates inside the box
            if (tentry < 0.0 && texit > 0.0) {
                tMin = 0.0; // The ray originates inside the box
                return true;
            }
        
            // Check for intersection
            if (tentry > texit || texit < 0.0) {
                return false; // No intersection
            }
        
            tMin = tentry; // Distance to the nearest intersection
            return true;
        }
        float LinearToDepth(float linearDepth)
        {
            return (1.0 - _ZBufferParams.w * linearDepth) / (linearDepth * _ZBufferParams.z);
        }
    
        half4 frag(Varyings IN, out float depth : SV_Depth) : SV_Target
        {
            uint MAX_CANDIDATES = 16; // Based on tree depth
            depth = 0.;
            //Early out if the data isn't set
            GPUNode root = _SVOBuffer[0];
            if(root.voxelData == 0)
                return half4(0,0,1,0.1);

            float distTravelled = 0.0;
            
            float3 rayO = IN.rayOrigin;
            float3 rayD = normalize(IN.hitPoint - rayO);
            float3 rootPos = _ObjectWorldPos + root.position;
            Bounds rootBounds = GetNodeBounds(root);

            float dist = 0;
            if(!rayBoxIntersectGPT(rayO, rayD, rootBounds.min, rootBounds.max, dist))
                return half4(0,0,0,0);

            //Dist to edge of the root is dist - from here we know we intersect with the octree and simply have to traverse it to the first leaf node we hit
            float3 startPos = rayO + dist * rayD;
            int candidateCount = 1;
            
            Candidate candidateNodes[32];
            Candidate rootC;
            rootC.nodeIndex = 0;
            rootC.distance = 0;
            candidateNodes[0] = rootC;

            while (candidateCount > 0) {
                int closestCandidateIndex = 0; // Assume the first candidate is the closest
                // Find the closest candidate (could optimize this part by maintaining a sorted order of candidates)
                for (int j = 1; j < candidateCount; j++) {
                    if (candidateNodes[j].distance < candidateNodes[closestCandidateIndex].distance) {
                        closestCandidateIndex = j;
                    }
                }
                
                Candidate closestCandidate = candidateNodes[closestCandidateIndex];
                // Remove the closest candidate from the list by swapping with the last and decrementing the count
                candidateNodes[closestCandidateIndex] = candidateNodes[candidateCount - 1];
                candidateCount--;
            
                GPUNode currentNode = _SVOBuffer[closestCandidate.nodeIndex];

                // If currentNode is a leaf with voxel data, perform shading and exit
                if (currentNode.voxelData != -1){
                    depth = LinearToDepth(closestCandidate.distance);
                    return ShadeVoxel(currentNode, (rayO + closestCandidate.distance * rayD), rayD);
                }
            
                // If not a leaf, add child nodes to candidates
                for (int i = 0; i < 8; i++) {
                    GPUNode childNode = _SVOBuffer[currentNode.childIndex + i];
                    Bounds childBounds = GetNodeBounds(childNode);

                    //Skip if this is a leaf with no data
                    if(childNode.childIndex == -1 && childNode.voxelData == -1)
                        continue;
                        
                    float distToChild;
                    if (rayBoxIntersectGPT(rayO, rayD, childBounds.min, childBounds.max, distToChild)) {
                        // Add childNode to candidates if it potentially intersects the ray
                        if (candidateCount < 32) {
                            Candidate newCandidate;
                            newCandidate.nodeIndex = currentNode.childIndex + i;
                            newCandidate.distance = distToChild;
                            candidateNodes[candidateCount++] = newCandidate;
                        }
                    }
                }
            }
            
            return half4(0, 0, 0, 0); // Background color
        }

    ENDHLSL

    SubShader
    {
        
        Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="Opaque"}
        Pass
        {
            AlphaToMask On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
            Cull Off
            //Blend SrcAlpha OneMinusSrcAlpha
        }
    }
}