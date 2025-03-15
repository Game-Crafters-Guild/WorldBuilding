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
        // Reusable collections to reduce allocations
        private List<float3> m_DrawnPoints = new List<float3>(128); // Pre-allocate capacity
        private List<float3> m_SimplifiedPoints = new List<float3>(64);
        private List<float3> m_LocalPositions = new List<float3>(64);
        private Stack<SimplifyStackEntry> m_SimplifyStack = new Stack<SimplifyStackEntry>(16);
        
        // Struct to replace recursion with stack-based iteration
        private struct SimplifyStackEntry
        {
            public int StartIdx;
            public int EndIdx;
        }
        
        private bool m_IsDrawing = false;
        private readonly float m_MinimumDistanceBetweenPoints = 0.25f;
        private readonly float m_BaseClosingThreshold = 1.0f; // Base threshold for close proximity
        private GUIContent m_IconContent;
        
        // Cached ray and hit info to reduce allocation
        private RaycastHit m_RaycastHitInfo;
        
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
        
        private void OnDisable()
        {
            // Clear collections when disabled to free memory
            m_DrawnPoints.Clear();
            m_SimplifiedPoints.Clear();
            m_LocalPositions.Clear();
            m_SimplifyStack.Clear();
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
            if (Physics.Raycast(ray, out m_RaycastHitInfo))
            {
                m_DrawnPoints.Add(m_RaycastHitInfo.point);
            }
            else
            {
                // If we didn't hit anything, use a point on the grid
                float distanceToGrid = 10f; // Default distance if no grid
                if (Physics.Raycast(ray, out m_RaycastHitInfo, Mathf.Infinity, LayerMask.GetMask("Grid")))
                {
                    distanceToGrid = m_RaycastHitInfo.distance;
                }
                
                float3 pointOnGrid = ray.origin + ray.direction * distanceToGrid;
                m_DrawnPoints.Add(pointOnGrid);
            }
        }
        
        private void AddPointIfValid(Event evt)
        {
            if (m_DrawnPoints.Count == 0)
                return;
                
            Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            float3 newPoint;
            
            if (Physics.Raycast(ray, out m_RaycastHitInfo))
            {
                newPoint = m_RaycastHitInfo.point;
            }
            else
            {
                // If we didn't hit anything, use a point on the grid
                float distanceToGrid = 10f; // Default distance if no grid
                if (Physics.Raycast(ray, out m_RaycastHitInfo, Mathf.Infinity, LayerMask.GetMask("Grid")))
                {
                    distanceToGrid = m_RaycastHitInfo.distance;
                }
                
                newPoint = ray.origin + ray.direction * distanceToGrid;
            }
            
            // Only add point if it's far enough from the last one
            if (math.distance(newPoint, m_DrawnPoints[m_DrawnPoints.Count - 1]) > m_MinimumDistanceBetweenPoints)
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
            bool shouldClosePath = math.distance(m_DrawnPoints[0], m_DrawnPoints[m_DrawnPoints.Count - 1]) < closingThreshold;
            
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
            float3 pointPosition = m_DrawnPoints[0];
            
            // Calculate distance from camera to point
            float distanceToCamera = math.distance(camera.transform.position, pointPosition);
            
            // Keep in mind that ScreenToWorldPoint in Unity uses the distance from the camera to determine scale
            // We need to use the camera's projection parameters to calculate screen space to world space conversion
            
            // Use field of view and distance to calculate how many world units per pixel at this distance
            float fovRadians = camera.fieldOfView * Mathf.Deg2Rad;
            float worldUnitsPerPixelAtDistance = 2.0f * distanceToCamera * Mathf.Tan(fovRadians * 0.5f) / camera.pixelHeight;
            
            // Desired size in pixels (how close cursor needs to be to first point to activate closing)
            const float desiredScreenSizePixels = 10.0f;
            
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
            Vector3 p1, p2;
            for (int i = 0; i < m_DrawnPoints.Count - 1; i++)
            {
                p1 = m_DrawnPoints[i];
                p2 = m_DrawnPoints[i + 1];
                Handles.DrawLine(p1, p2, 5.0f);
            }
            
            // Draw points
            Handles.color = Color.white;
            foreach (var point in m_DrawnPoints)
            {
                Handles.SphereHandleCap(0, point, Quaternion.identity, 0.2f, EventType.Repaint);
            }
            
            // Calculate dynamic closing threshold for preview
            float closingThreshold = CalculateClosingThreshold();
            
            // Draw dashed line from last point to mouse position if drawing
            if (m_IsDrawing)
            {
                Handles.color = new Color(1, 1, 0, 0.5f);
                
                Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                float3 lastPoint = m_DrawnPoints[m_DrawnPoints.Count - 1];
                float3 cursorPoint;
                
                if (Physics.Raycast(ray, out m_RaycastHitInfo))
                {
                    cursorPoint = m_RaycastHitInfo.point;
                }
                else
                {
                    float distanceToGrid = 10f;
                    if (Physics.Raycast(ray, out m_RaycastHitInfo, Mathf.Infinity, LayerMask.GetMask("Grid")))
                    {
                        distanceToGrid = m_RaycastHitInfo.distance;
                    }
                    
                    cursorPoint = ray.origin + ray.direction * distanceToGrid;
                }
                
                Handles.DrawDottedLine(lastPoint, cursorPoint, 5f);
                
                // Show closing indicator if close to the first point
                if (m_DrawnPoints.Count > 2)
                {
                    float3 firstPoint = m_DrawnPoints[0];
                    
                    if (math.distance(cursorPoint, firstPoint) < closingThreshold)
                    {
                        Handles.color = Color.yellow;
                        Handles.DrawWireDisc(firstPoint, Vector3.up, closingThreshold);
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
            ProcessDrawnPointsForSpline(out var positions);
            
            // Position the object at the center of the drawn points
            float3 center = CalculateCenter(positions);
            splineObject.transform.position = center;
            
            // Create the spline
            Spline spline = splineContainer.Spline != null ? splineContainer.Spline : new Spline();
            
            // Calculate a dynamic error threshold based on the shape size
            float errorThreshold = CalculateErrorThreshold(positions);
            
            // Adjust positions relative to the object's position
            m_LocalPositions.Clear();
            for (int i = 0; i < positions.Count; i++)
            {
                m_LocalPositions.Add(positions[i] - center);
            }
            
            // Use SplineUtility.FitSplineToPoints to create a spline from the points
            SplineUtility.FitSplineToPoints(m_LocalPositions, errorThreshold, isSplineClosed, out spline);
            
            // Add the spline to the container
            splineContainer.Spline = spline;
            
            // Select the created object
            Selection.activeGameObject = splineObject;
            
            // Optional: Add a Stamp component 
            if (splineObject.TryGetComponent<Stamp>(out var stamp) == false)
            {
                stamp = splineObject.AddComponent<Stamp>();
            }
            stamp.GenerateMask();
        }
        
        private float3 CalculateCenter(List<float3> points)
        {
            if (points == null || points.Count == 0)
                return float3.zero;
                
            float3 sum = float3.zero;
            
            // Use direct sum with preallocated points count to avoid division per point
            int pointCount = points.Count;
            for (int i = 0; i < pointCount; i++)
            {
                sum += points[i];
            }
            
            return sum / pointCount;
        }
        
        private float CalculateErrorThreshold(List<float3> points)
        {
            if (points.Count < 2)
                return 0.2f; // Default fallback
                
            // Method 1: Base on average distance between points
            float totalDistance = 0f;
            float maxDistance = 0f;
            
            int pointCount = points.Count;
            for (int i = 0; i < pointCount - 1; i++)
            {
                float distance = math.distance(points[i], points[i + 1]);
                totalDistance += distance;
                maxDistance = math.max(maxDistance, distance);
            }
            
            float avgDistance = totalDistance / (pointCount - 1);
            
            // Method 2: Calculate the bounding box size
            float3 min = points[0];
            float3 max = points[0];
            
            for (int i = 0; i < pointCount; i++)
            {
                min = math.min(min, points[i]);
                max = math.max(max, points[i]);
            }
            
            float boundingBoxDiagonal = math.length(max - min);
            
            // Use a combination of methods to determine a good threshold
            // - For small shapes, provide more precision (smaller threshold)
            // - For large shapes, allow more deviation (larger threshold)
            // - Keep it proportional to the shape size
            
            // Increased percentages to allow more error (fewer points)
            // Between 3% and 8% of the diagonal instead of 1-5%
            float diagonalPercentage = boundingBoxDiagonal * 0.05f;
            float basedOnAvgDistance = avgDistance * 0.4f; // Doubled from 0.2f
            
            // Choose the larger of the two to favor fewer points
            float threshold = math.max(diagonalPercentage, basedOnAvgDistance);
            
            // Ensure reasonable bounds, with higher minimum and maximum
            threshold = math.clamp(threshold, 0.1f, 1.0f); // Increased from 0.05-0.5
            
            return threshold;
        }
        
        private void ProcessDrawnPointsForSpline(out List<float3> results)
        {
            // Use the reusable collection instead of creating a new one
            m_SimplifiedPoints.Clear();
            
            // Apply Ramer-Douglas-Peucker algorithm to simplify the polyline
            // This reduces the number of points while preserving the overall shape
            
            // If we have very few points, just return them as is
            if (m_DrawnPoints.Count <= 3)
            {
                foreach (var pt in m_DrawnPoints)
                {
                    m_SimplifiedPoints.Add(pt);
                }
                results = m_SimplifiedPoints;
                return;
            }
                
            // Choose a simplification tolerance based on the size of the shape
            float3 min = m_DrawnPoints[0];
            float3 max = m_DrawnPoints[0];
            
            foreach (var pt in m_DrawnPoints)
            {
                min = math.min(min, pt);
                max = math.max(max, pt);
            }
            
            float boundingBoxDiagonal = math.length(max - min);
            
            // Set simplification tolerance as a percentage of the bounding box size
            float simplificationTolerance = boundingBoxDiagonal * 0.01f;
            
            // Apply the Douglas-Peucker algorithm
            SimplifyPointsNonRecursive(m_DrawnPoints, simplificationTolerance, m_SimplifiedPoints);
            
            results = m_SimplifiedPoints;
        }
        
        // Non-recursive version of the Ramer-Douglas-Peucker algorithm
        private void SimplifyPointsNonRecursive(List<float3> points, float epsilon, List<float3> result)
        {
            if (points.Count <= 2)
            {
                foreach (var pt in points)
                {
                    result.Add(pt);
                }
                return;
            }
            
            // Use a boolean array to mark which points to keep
            bool[] keepPoint = new bool[points.Count];
            
            // Always keep first and last points
            keepPoint[0] = true;
            keepPoint[points.Count - 1] = true;
            
            // Clear the stack and add the first range
            m_SimplifyStack.Clear();
            m_SimplifyStack.Push(new SimplifyStackEntry { StartIdx = 0, EndIdx = points.Count - 1 });
            
            // Process all segments in the stack
            while (m_SimplifyStack.Count > 0)
            {
                SimplifyStackEntry entry = m_SimplifyStack.Pop();
                int startIdx = entry.StartIdx;
                int endIdx = entry.EndIdx;
                
                float maxDistance = 0;
                int indexOfFurthest = 0;
                
                // Find the point with the maximum distance
                for (int i = startIdx + 1; i < endIdx; i++)
                {
                    float distance = PerpendicularDistance(points[i], points[startIdx], points[endIdx]);
                    
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        indexOfFurthest = i;
                    }
                }
                
                // If max distance is greater than epsilon, mark point to keep and process sub-segments
                if (maxDistance > epsilon)
                {
                    keepPoint[indexOfFurthest] = true;
                    
                    if (indexOfFurthest - startIdx > 1)
                    {
                        m_SimplifyStack.Push(new SimplifyStackEntry { StartIdx = startIdx, EndIdx = indexOfFurthest });
                    }
                    
                    if (endIdx - indexOfFurthest > 1)
                    {
                        m_SimplifyStack.Push(new SimplifyStackEntry { StartIdx = indexOfFurthest, EndIdx = endIdx });
                    }
                }
            }
            
            // Add all the kept points to the result in order
            for (int i = 0; i < points.Count; i++)
            {
                if (keepPoint[i])
                {
                    result.Add(points[i]);
                }
            }
        }
        
        // Calculate the perpendicular distance from a point to a line segment
        private float PerpendicularDistance(float3 point, float3 lineStart, float3 lineEnd)
        {
            float3 lineVec = lineEnd - lineStart;
            float lineLength = math.length(lineVec);
            
            // Handle special case of zero-length line
            if (lineLength < float.Epsilon)
                return math.distance(point, lineStart);
                
            // Project point onto line
            float t = math.clamp(math.dot(point - lineStart, lineVec) / math.dot(lineVec, lineVec), 0f, 1f);
            float3 projection = lineStart + t * lineVec;
            
            // Return distance to projection
            return math.distance(point, projection);
        }
    }
}
