using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace PixelReyn.SimpleVoxelSystem {
    [CustomEditor(typeof(VoxelContainer))]
    public class VoxelEditor : Editor
    {    
        private int selectedVoxelId = 0; // Default or selected voxel ID
        public int voxelSize = 0;
        public bool isStatic = false;
        public bool isTransparent = false;
        private VoxelSettings voxelSettings; // Reference to VoxelSettings
        private Rect windowRect = new Rect(20, 20, 200, 150); // Initial position and size of the window
        private string[] editModes = { "Add", "Remove", "Replace" };
        private int selectedEditModeIndex = 0; // Default to "Add" mode

        private void OnEnable()
        {
            // Load VoxelSettings
            voxelSettings = Resources.Load<VoxelSettings>("VoxelSettings");
            VoxelContainer voxelContainer = (VoxelContainer)target;
            if(voxelContainer.voxelObject == null)
                return;
            voxelContainer.InitializeBuffers();
        }
        private void OnDisable() {
            VoxelContainer voxelContainer = (VoxelContainer)target;
            if(voxelContainer.voxelObject == null  || voxelContainer.IsPlaying)
                return;
            voxelContainer.DestroyBuffers();
        }
        private void DrawSettingsWindow(int windowID)
        {
            VoxelContainer voxelContainer = (VoxelContainer)target;
            // Place your GUI code here (e.g., GUILayout.Label, GUILayout.Button)
            GUILayout.Label("Voxel Settings", EditorStyles.boldLabel);
            selectedEditModeIndex = GUILayout.Toolbar(selectedEditModeIndex, editModes);
            EditorGUI.BeginChangeCheck();
            selectedVoxelId = EditorGUILayout.IntField("Voxel ID", selectedVoxelId);
            if (EditorGUI.EndChangeCheck()) // If ID changes, update color picker
            {
                selectedVoxelId = Mathf.Clamp(selectedVoxelId, 0, voxelSettings.voxelColors.Length - 1);
            }

            if (selectedVoxelId >= 0 && voxelSettings != null && selectedVoxelId < voxelSettings.voxelColors.Length)
            {
                EditorGUI.BeginChangeCheck();
                Color newColor = EditorGUILayout.ColorField("Voxel Color", voxelSettings.voxelColors[selectedVoxelId]);
                if (EditorGUI.EndChangeCheck())
                {
                    if (EditorUtility.DisplayDialog("Confirm Color Change",
                        "Changing this color will affect all voxels with this ID. Are you sure?",
                        "Yes", "No"))
                    {
                        voxelSettings.voxelColors[selectedVoxelId] = newColor;
                        voxelContainer.InitializeBuffers();
                    }
                }
            }
            voxelSize = EditorGUILayout.IntField("Voxel Size", voxelSize);
            isStatic = EditorGUILayout.Toggle("Static Block", isStatic);
            isTransparent = EditorGUILayout.Toggle("Transparent", isTransparent);

            // Make the entire window draggable
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        void OnSceneGUI()
        {
            // Ensure user interaction is only when the VoxelWorld object is selected
            VoxelContainer voxelContainer = (VoxelContainer)target;
            if(voxelContainer.voxelObject == null)
                return;
                
            if(voxelContainer.voxelObject.root == null){
                voxelContainer.voxelObject.Initialize(new Bounds(Vector3.zero, Vector3.one * 16));
            }

            // Draw the floating toolbar
            windowRect = GUILayout.Window(0, windowRect, DrawSettingsWindow, "Voxel Editor");
            Event e = Event.current;

            DrawGrid(voxelContainer);
            // Handle mouse input
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Plane plane = new Plane(Vector3.up, voxelContainer.transform.position);
                var raymarch = voxelContainer.RayIntersect(ray);
                if (!raymarch.Item1 && plane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);

                    // Align hitPoint to grid
                    float intSize = voxelSize + 1f;
                    hitPoint = math.ceil(hitPoint / intSize) * intSize;
                    hitPoint -= (Vector3)math.round(voxelContainer.transform.position);

                    // Place voxel based on the selected edit mode
                    switch (editModes[selectedEditModeIndex])
                    {
                        case "Add":
                            voxelContainer.voxelObject.AddVoxel(new Voxel((sbyte)selectedVoxelId, isTransparent, isStatic, (byte)voxelSize), hitPoint);
                            break;
                        case "Remove":
                            voxelContainer.voxelObject.RemoveVoxel(hitPoint, out _);
                            break;
                    }
                    EditorUtility.SetDirty(voxelContainer);
                    EditorUtility.SetDirty(voxelContainer.voxelObject);
                    voxelContainer.InitializeBuffers(true);
                    e.Use(); // Mark the event as used
                }
                else if(raymarch.Item1){
                        switch (editModes[selectedEditModeIndex])
                        {
                            case "Add":
                                float3 normalizedNormal = raymarch.Item2;
                                float size = 1f / (voxelSize + 1f);
                                normalizedNormal = math.normalize(normalizedNormal);
                                Vector3 normalizedPoint = (float3)raymarch.Item3 + (-normalizedNormal);// * size); 
                                voxelContainer.voxelObject.AddVoxel(new Voxel((sbyte)selectedVoxelId, isTransparent, isStatic, (byte)voxelSize), normalizedPoint);
                                break;
                            case "Replace":
                                voxelContainer.voxelObject.AddVoxel(new Voxel((sbyte)selectedVoxelId, isTransparent, isStatic), raymarch.Item3);
                                break;
                            case "Remove":
                                voxelContainer.voxelObject.RemoveVoxel(raymarch.Item3, out _);
                                break;
                        }
                        EditorUtility.SetDirty(voxelContainer);
                        EditorUtility.SetDirty(voxelContainer.voxelObject);
                        voxelContainer.InitializeBuffers(true);
                        e.Use(); // Mark the event as used
                }

            }
            SceneView.RepaintAll();
        }

        void DrawGrid(VoxelContainer voxelContainer)
        {
            
            var gridSize = (int)voxelContainer.voxelObject.root.HalfSize; // Define the grid size
            var gridSpacing = 1;//voxelContainer.voxelObject.root.minSize; // Define the spacing between grid lines
            var gridOrigin = voxelContainer.transform.position; // Starting point of the grid
            
            Handles.color = Color.gray;
            for (int x = -gridSize; x <= gridSize; x++)
            {
                Vector3 startPoint = gridOrigin + new Vector3(x * gridSpacing, 0, -gridSize * gridSpacing);
                Vector3 endPoint = gridOrigin + new Vector3(x * gridSpacing, 0, gridSize * gridSpacing);
                Handles.DrawLine(startPoint, endPoint);
            }

            for (int z = -gridSize; z <= gridSize; z++)
            {
                Vector3 startPoint = gridOrigin + new Vector3(-gridSize * gridSpacing, 0, z * gridSpacing);
                Vector3 endPoint = gridOrigin + new Vector3(gridSize * gridSpacing, 0, z * gridSpacing);
                Handles.DrawLine(startPoint, endPoint);
            }
        }
    }
}