using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using Unity.Mathematics;

namespace GameCraftersGuild.WorldBuilding.Editor
{
#if UNITY_2023_1_OR_NEWER
    [EditorTool("Create Spline Stamp", toolPriority = 10)]
#else
    [EditorTool("Create Spline Stamp")]
#endif
    public class CreateSplineStampTool : SplineTool
    {
        private List<float3> m_DrawnPoints = new List<float3>();
        private bool m_IsDrawing = false;
        private readonly float m_MinimumDistanceBetweenPoints = 0.25f;
        private readonly float m_BaseClosingThreshold = 1.0f; // Base threshold for close proximity
        private GUIContent m_IconContent;
        
        public override GUIContent toolbarIcon => m_IconContent;
        
        private void OnEnable()
        {
            m_IconContent = new GUIContent
            {
                image = EditorGUIUtility.IconContent("EditCollider").image,
                text = "Create Spline Stamp",
                tooltip = "Draw a shape to create a new spline stamp"
            };
        }
        
        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView sceneView))
                return;
            
            Event evt = Event.current;
            
            // Handle input events
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            
            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (evt.button == 0) // Left mouse button
                    {
                        if (!m_IsDrawing)
                        {
                            StartDrawing(evt);
                            evt.Use();
                        }
                    }
                    break;
                    
                case EventType.MouseDrag:
                    if (evt.button == 0 && m_IsDrawing)
                    {
                        AddPointIfValid(evt);
                        evt.Use();
                    }
                    break;
                    
                case EventType.MouseUp:
                    if (evt.button == 0 && m_IsDrawing)
                    {
                        FinishDrawing(evt);
                        evt.Use();
                    }
                    break;
                    
                case EventType.KeyDown:
                    if (evt.keyCode == KeyCode.Escape && m_IsDrawing)
                    {
                        CancelDrawing();
                        evt.Use();
                    }
                    break;
                    
                case EventType.Repaint:
                    DrawPreview();
                    break;
            }
        }
        
        private void StartDrawing(Event evt)
        {
            m_DrawnPoints.Clear();
            m_IsDrawing = true;
            
            Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                m_DrawnPoints.Add(hit.point);
            }
            else
            {
                // If we didn't hit anything, use a point on the grid
                float distanceToGrid = 10f; // Default distance if no grid
                if (Physics.Raycast(ray, out RaycastHit gridHit, Mathf.Infinity, LayerMask.GetMask("Grid")))
                {
                    distanceToGrid = gridHit.distance;
                }
                
                Vector3 pointOnGrid = ray.origin + ray.direction * distanceToGrid;
                m_DrawnPoints.Add(pointOnGrid);
            }
        }
        
        private void AddPointIfValid(Event evt)
        {
            if (m_DrawnPoints.Count == 0)
                return;
                
            Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            Vector3 newPoint;
            
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                newPoint = hit.point;
            }
            else
            {
                // If we didn't hit anything, use a point on the grid
                float distanceToGrid = 10f; // Default distance if no grid
                if (Physics.Raycast(ray, out RaycastHit gridHit, Mathf.Infinity, LayerMask.GetMask("Grid")))
                {
                    distanceToGrid = gridHit.distance;
                }
                
                newPoint = ray.origin + ray.direction * distanceToGrid;
            }
            
            // Only add point if it's far enough from the last one
            if (Vector3.Distance(newPoint, m_DrawnPoints[m_DrawnPoints.Count - 1]) > m_MinimumDistanceBetweenPoints)
            {
                m_DrawnPoints.Add(newPoint);
            }
        }
        
        private void FinishDrawing(Event evt)
        {
            if (m_DrawnPoints.Count < 3)
            {
                // Not enough points to create a spline
                CancelDrawing();
                return;
            }
            
            // Calculate a dynamic closing threshold based on distance from camera
            float closingThreshold = CalculateClosingThreshold();
            
            // Check if the shape is closed (last point is close to first point)
            bool shouldClosePath = Vector3.Distance(m_DrawnPoints[0], m_DrawnPoints[m_DrawnPoints.Count - 1]) < closingThreshold;
            
            if (shouldClosePath)
            {
                // Close the loop by making last point the same as first
                m_DrawnPoints[m_DrawnPoints.Count - 1] = m_DrawnPoints[0];
            }
            
            // Create the spline game object
            CreateSplineFromDrawnPoints(shouldClosePath);
            
            // Reset state
            m_IsDrawing = false;
            m_DrawnPoints.Clear();
        }
        
        private float CalculateClosingThreshold()
        {
            if (m_DrawnPoints.Count == 0)
                return m_BaseClosingThreshold;
                
            // Get the active scene view camera
            Camera camera = SceneView.lastActiveSceneView.camera;
            if (camera == null)
                return m_BaseClosingThreshold;
                
            // More robust approach that properly scales with distance
            Vector3 pointPosition = (Vector3)m_DrawnPoints[0];
            
            // Calculate distance from camera to point
            float distanceToCamera = Vector3.Distance(camera.transform.position, pointPosition);
            
            // Keep in mind that ScreenToWorldPoint in Unity uses the distance from the camera to determine scale
            // We need to use the camera's projection parameters to calculate screen space to world space conversion
            
            // Use field of view and distance to calculate how many world units per pixel at this distance
            float fovRadians = camera.fieldOfView * Mathf.Deg2Rad;
            float worldUnitsPerPixelAtDistance = 2.0f * distanceToCamera * Mathf.Tan(fovRadians * 0.5f) / camera.pixelHeight;
            
            // Desired size in pixels (how close cursor needs to be to first point to activate closing)
            float desiredScreenSizePixels = 10.0f;
            
            // Convert to world units
            float worldSpaceThreshold = worldUnitsPerPixelAtDistance * desiredScreenSizePixels;
            
            // Add a distance bias factor - make the threshold larger at greater distances
            // but don't increase it too much for close objects
            float distanceBiasFactor = Mathf.Max(1.0f, Mathf.Log10(distanceToCamera + 1) * 2.0f);
            worldSpaceThreshold *= distanceBiasFactor;

            return worldSpaceThreshold;
        }
        
        private void CancelDrawing()
        {
            m_IsDrawing = false;
            m_DrawnPoints.Clear();
        }
        
        private void DrawPreview()
        {
            if (!m_IsDrawing || m_DrawnPoints.Count < 2)
                return;
                
            // Set the color for drawing
            Handles.color = Color.green;
            
            // Draw lines between points
            for (int i = 0; i < m_DrawnPoints.Count - 1; i++)
            {
                Handles.DrawLine(m_DrawnPoints[i], m_DrawnPoints[i + 1], 5.0f);
            }
            
            // Draw points
            foreach (Vector3 point in m_DrawnPoints)
            {
                Handles.color = Color.white;
                Handles.SphereHandleCap(0, point, Quaternion.identity, 0.2f, EventType.Repaint);
            }
            
            // Calculate dynamic closing threshold for preview
            float closingThreshold = CalculateClosingThreshold();
            
            // Draw dashed line from last point to mouse position if drawing
            if (m_IsDrawing)
            {
                Handles.color = new Color(1, 1, 0, 0.5f);
                
                Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Handles.DrawDottedLine(m_DrawnPoints[m_DrawnPoints.Count - 1], hit.point, 5f);
                }
                else
                {
                    float distanceToGrid = 10f;
                    if (Physics.Raycast(ray, out RaycastHit gridHit, Mathf.Infinity, LayerMask.GetMask("Grid")))
                    {
                        distanceToGrid = gridHit.distance;
                    }
                    
                    Vector3 pointOnGrid = ray.origin + ray.direction * distanceToGrid;
                    Handles.DrawDottedLine(m_DrawnPoints[m_DrawnPoints.Count - 1], pointOnGrid, 5f);
                }
                
                // Show closing indicator if close to the first point
                if (m_DrawnPoints.Count > 2)
                {
                    Vector3 mousePosition = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition).GetPoint(10f);
                    if (Physics.Raycast(ray, out RaycastHit closeHit))
                    {
                        mousePosition = closeHit.point;
                    }
                    
                    if (Vector3.Distance(mousePosition, m_DrawnPoints[0]) < closingThreshold)
                    {
                        Handles.color = Color.yellow;
                        Handles.DrawWireDisc(m_DrawnPoints[0], Vector3.up, closingThreshold);
                    }
                }
            }
        }
        
        private void CreateSplineFromDrawnPoints(bool isSplineClosed)
        {
            // Create a new GameObject with a SplineContainer
            GameObject splineObject = new GameObject("Spline Stamp");
            SplineContainer splineContainer = splineObject.AddComponent<SplineContainer>();
            
            // Create points for the spline
            List<float3> positions = ProcessDrawnPointsForSpline();
            
            // Position the object at the center of the drawn points
            float3 center = CalculateCenter(positions);
            splineObject.transform.position = center;
            
            // Create the spline
            Spline spline = splineContainer.Spline != null ? splineContainer.Spline : new Spline();
            
            // Calculate a dynamic error threshold based on the shape size
            float errorThreshold = CalculateErrorThreshold(positions);
            
            // Adjust positions relative to the object's position
            List<float3> localPositions = new List<float3>(positions.Count);
            foreach (var pos in positions)
            {
                localPositions.Add(pos - center);
            }
            
            // Use SplineUtility.FitSplineToPoints to create a spline from the points
            SplineUtility.FitSplineToPoints(localPositions, errorThreshold, isSplineClosed, out spline);
            
            // Add the spline to the container
            splineContainer.Spline = spline;
            
            // Select the created object
            Selection.activeGameObject = splineObject;
            
            // Optional: Add a Stamp component 
            if (splineObject.TryGetComponent<Stamp>(out var stamp) == false)
            {
                stamp = splineObject.AddComponent<Stamp>();
            }
            
            // Create and set up a SplinePathShape
            /*var splinePathShape = ObjectFactory.CreateInstance<SplinePathShape>();
            splinePathShape.m_SplineContainer = splineContainer;
            stamp.Shape = splinePathShape;
            
            // Add the stamp to the WorldBuildingSystem if needed
            var worldBuildingSystem = WorldBuildingSystem.GetOrCreate();
            worldBuildingSystem.RegisterBuilder(stamp);*/
        }
        
        private float3 CalculateCenter(List<float3> points)
        {
            if (points == null || points.Count == 0)
                return float3.zero;
                
            float3 sum = float3.zero;
            foreach (var point in points)
            {
                sum += point;
            }
            
            return sum / points.Count;
        }
        
        private float CalculateErrorThreshold(List<float3> points)
        {
            if (points.Count < 2)
                return 0.2f; // Default fallback
                
            // Method 1: Base on average distance between points
            float totalDistance = 0f;
            float maxDistance = 0f;
            
            for (int i = 0; i < points.Count - 1; i++)
            {
                float distance = math.distance(points[i], points[i + 1]);
                totalDistance += distance;
                maxDistance = math.max(maxDistance, distance);
            }
            
            float avgDistance = totalDistance / (points.Count - 1);
            
            // Method 2: Calculate the bounding box size
            float3 min = points[0];
            float3 max = points[0];
            
            foreach (var pt in points)
            {
                min = math.min(min, pt);
                max = math.max(max, pt);
            }
            
            float boundingBoxDiagonal = math.length(max - min);
            
            // Use a combination of methods to determine a good threshold
            // - For small shapes, provide more precision (smaller threshold)
            // - For large shapes, allow more deviation (larger threshold)
            // - Keep it proportional to the shape size
            
            // Between 1% and 5% of the diagonal, clamped between 0.05 and 0.5
            float diagonalPercentage = boundingBoxDiagonal * 0.02f;
            float basedOnAvgDistance = avgDistance * 0.2f;
            
            // Choose the smaller of the two to favor precision
            float threshold = math.min(diagonalPercentage, basedOnAvgDistance);
            
            // Ensure reasonable bounds
            threshold = math.clamp(threshold, 0.05f, 0.5f);
            
            return threshold;
        }
        
        private List<float3> ProcessDrawnPointsForSpline()
        {
            // Process the drawn points for spline creation
            // We may want to simplify, smooth, or otherwise process the points
            return new List<float3>(m_DrawnPoints);
        }
    }
}
