using UnityEngine;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine.Splines;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    [CustomEditor(typeof(SplinePathShape))]
    public class SplinePathShapeEditor : UnityEditor.Editor
    {
        private SplinePathShape m_SplinePathShape;
        
        private void OnEnable()
        {
            m_SplinePathShape = (SplinePathShape)target;
            
            // Ensure SceneView repaint when debug visualization is enabled
            SceneView.duringSceneGui += OnSceneGUIDelegate;
        }
        
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUIDelegate;
        }
        
        private void OnSceneGUIDelegate(SceneView sceneView)
        {
            // This ensures the visualization gets rendered properly when needed
            if (m_SplinePathShape != null && m_SplinePathShape.m_DebugMode)
            {
                sceneView.Repaint();
            }
        }
        
        private void OnSceneGUI()
        {
            if (m_SplinePathShape == null || !m_SplinePathShape.m_DebugMode || 
                m_SplinePathShape.SplineContainer == null) 
                return;
            
            // Draw gizmos to help visualize spline heights
            Color originalHandlesColor = Handles.color;
            Handles.color = Color.yellow;
            
            foreach (var spline in m_SplinePathShape.SplineContainer.Splines)
            {
                if (spline == null || spline.Count < 2) continue;
                
                float length = spline.GetLength();
                int steps = Mathf.Max(20, Mathf.CeilToInt(length));
                
                for (int i = 0; i < steps; i++)
                {
                    float t = (float)i / (steps - 1);
                    
                    SplineUtility.Evaluate(spline, t, out var pos, out var dir, out var up);
                    Vector3 worldPos = m_SplinePathShape.transform.TransformPoint(pos);
                    
                    // Draw sphere at each evaluation point
                    float handleSize = HandleUtility.GetHandleSize(worldPos) * 0.1f;
                    Handles.SphereHandleCap(
                        0, 
                        worldPos, 
                        Quaternion.identity, 
                        handleSize, 
                        EventType.Repaint
                    );
                    
                    // Draw line to visualize height from ground
                    if (i % 5 == 0)
                    {
                        Vector3 groundPos = worldPos;
                        groundPos.y = 0;
                        Handles.DrawLine(groundPos, worldPos);
                        
                        // Draw height text
                        Handles.Label(worldPos + Vector3.up * 0.5f, $"Y: {worldPos.y:F1}");
                    }
                }
            }
            
            Handles.color = originalHandlesColor;
        }
        
        public override void OnInspectorGUI()
        {
            // Start checking for changes
            EditorGUI.BeginChangeCheck();
            
            // Draw the default inspector for all other properties
            DrawDefaultInspector();
            
            // If debug mode changed, force a SceneView repaint
            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }
        }
    }
} 