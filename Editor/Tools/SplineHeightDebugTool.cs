using UnityEngine;
using UnityEditor;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;

namespace GameCraftersGuild.WorldBuilding
{
    /// <summary>
    /// Debug tool to visualize height encoding in spline paths
    /// </summary>
    public class SplineHeightDebugTool : EditorWindow
    {
        private SplineContainer selectedSpline;
        private Terrain selectedTerrain;
        private bool showHeightInfo = true;
        private bool showWorldSpaceHeights = true;
        private bool showMaskPreview = false;
        private float debugHeight = 0;
        
        //[MenuItem("Tools/World Building/Spline Height Debug")]
        public static void ShowWindow()
        {
            GetWindow<SplineHeightDebugTool>("Spline Height Debug");
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Spline Height Debugging Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox("This tool helps debug height encoding in spline paths.", MessageType.Info);
            
            EditorGUILayout.Space();
            selectedSpline = EditorGUILayout.ObjectField("Spline Container", selectedSpline, typeof(SplineContainer), true) as SplineContainer;
            selectedTerrain = EditorGUILayout.ObjectField("Terrain", selectedTerrain, typeof(Terrain), true) as Terrain;
            
            if (selectedSpline == null || selectedTerrain == null)
            {
                EditorGUILayout.HelpBox("Select both a spline container and terrain to debug heights.", MessageType.Warning);
                return;
            }
            
            EditorGUILayout.Space();
            showHeightInfo = EditorGUILayout.Toggle("Show Height Info", showHeightInfo);
            showWorldSpaceHeights = EditorGUILayout.Toggle("Show World Space Heights", showWorldSpaceHeights);
            showMaskPreview = EditorGUILayout.Toggle("Show Mask Preview", showMaskPreview);
            
            if (showHeightInfo && selectedSpline != null)
            {
                DrawSplineHeightInfo();
            }
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Apply Debug Height"))
            {
                ApplyDebugHeight();
            }
            
            debugHeight = EditorGUILayout.Slider("Debug Height", debugHeight, 0f, 100f);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Update Visualization"))
            {
                SceneView.RepaintAll();
            }
        }
        
        private void DrawSplineHeightInfo()
        {
            EditorGUILayout.LabelField("Spline Height Information", EditorStyles.boldLabel);
            
            if (selectedSpline.Splines.Count == 0)
            {
                EditorGUILayout.HelpBox("Selected spline has no spline data.", MessageType.Warning);
                return;
            }
            
            EditorGUILayout.LabelField($"Spline Count: {selectedSpline.Splines.Count}");
            
            for (int splineIndex = 0; splineIndex < selectedSpline.Splines.Count; splineIndex++)
            {
                Spline spline = selectedSpline.Splines[splineIndex];
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Spline {splineIndex}:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Knot Count: {spline.Count}");
                
                float minY = float.MaxValue;
                float maxY = float.MinValue;
                
                for (int knotIndex = 0; knotIndex < spline.Count; knotIndex++)
                {
                    BezierKnot knot = spline[knotIndex];
                    float3 worldPos = selectedSpline.transform.TransformPoint(knot.Position);
                    
                    minY = Mathf.Min(minY, worldPos.y);
                    maxY = Mathf.Max(maxY, worldPos.y);
                    
                    EditorGUILayout.LabelField($"  Knot {knotIndex}: " +
                                              $"Local Y = {knot.Position.y:F2}, " +
                                              $"World Y = {worldPos.y:F2}");
                }
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"  Min Height: {minY:F2}, Max Height: {maxY:F2}");
                EditorGUILayout.LabelField($"  Height Range: {maxY - minY:F2}");
                
                if (selectedTerrain != null)
                {
                    float terrainMinY = selectedTerrain.transform.position.y;
                    float terrainMaxY = terrainMinY + selectedTerrain.terrainData.size.y;
                    
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField($"  Terrain Height Range: {terrainMinY:F2} - {terrainMaxY:F2}");
                    
                    // Check if spline heights are within terrain range
                    if (minY < terrainMinY || maxY > terrainMaxY)
                    {
                        EditorGUILayout.HelpBox("Some spline heights are outside the terrain's height range!", MessageType.Warning);
                    }
                    
                    // Calculate normalized heights relative to terrain
                    float normalizedMin = Mathf.InverseLerp(terrainMinY, terrainMaxY, minY);
                    float normalizedMax = Mathf.InverseLerp(terrainMinY, terrainMaxY, maxY);
                    
                    EditorGUILayout.LabelField($"  Normalized Height Range: {normalizedMin:F2} - {normalizedMax:F2}");
                }
            }
        }
        
        private void ApplyDebugHeight()
        {
            if (selectedSpline == null) return;
            
            Undo.RecordObject(selectedSpline, "Apply Debug Height");
            
            for (int splineIndex = 0; splineIndex < selectedSpline.Splines.Count; splineIndex++)
            {
                Spline spline = selectedSpline.Splines[splineIndex];
                
                for (int knotIndex = 0; knotIndex < spline.Count; knotIndex++)
                {
                    BezierKnot knot = spline[knotIndex];
                    
                    // Set all knots to the same debug height
                    float3 newPos = knot.Position;
                    newPos.y = debugHeight;
                    knot.Position = newPos;
                    
                    spline[knotIndex] = knot;
                }
            }
            
            EditorUtility.SetDirty(selectedSpline);
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (!showHeightInfo || selectedSpline == null) return;
            
            for (int splineIndex = 0; splineIndex < selectedSpline.Splines.Count; splineIndex++)
            {
                Spline spline = selectedSpline.Splines[splineIndex];
                
                for (int knotIndex = 0; knotIndex < spline.Count; knotIndex++)
                {
                    BezierKnot knot = spline[knotIndex];
                    float3 worldPos = selectedSpline.transform.TransformPoint(knot.Position);
                    
                    // Draw sphere at knot position
                    Handles.color = Color.yellow;
                    Handles.SphereHandleCap(0, worldPos, Quaternion.identity, 0.5f, EventType.Repaint);
                    
                    // Draw height information
                    if (showWorldSpaceHeights)
                    {
                        Handles.Label(worldPos + (float3)Vector3.up * 0.5f, $"Y: {worldPos.y:F2}");
                    }
                    else
                    {
                        Handles.Label(worldPos + (float3)Vector3.up * 0.5f, $"Y: {knot.Position.y:F2}");
                    }
                    
                    // Draw vertical line to visualize height
                    if (selectedTerrain != null)
                    {
                        float terrainY = selectedTerrain.transform.position.y;
                        Vector3 terrainPos = new Vector3(worldPos.x, terrainY, worldPos.z);
                        Handles.color = Color.green;
                        Handles.DrawLine(terrainPos, worldPos);
                    }
                }
            }
        }
        
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }
    }
} 