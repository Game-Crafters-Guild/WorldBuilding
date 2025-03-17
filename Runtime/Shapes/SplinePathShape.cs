using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace GameCraftersGuild.WorldBuilding
{
    [RequireComponent(typeof(SplineContainer))]
    public class SplinePathShape : StampShape
    {
        [Range(1.0f, 100.0f)] public float Width = 5.0f;
        [SerializeReference] [HideInInspector] internal SplineContainer m_SplineContainer;
        public override SplineContainer SplineContainer => m_SplineContainer;
        public override bool MaintainMaskAspectRatio { get; } = false;

        [SerializeField, HideInInspector] private Material m_SplineToMaskMaterial;
        
        [Tooltip("Higher resolution gives smoother edges but uses more memory")]
        private const int kMaskResolution = 512;

        private static readonly int kMaterialColorId = Shader.PropertyToID("_Color");

        [SerializeField] List<SplineData<float>> m_Widths = new List<SplineData<float>>();

        [SerializeField] private bool m_DebugMode = false;

        public List<SplineData<float>> Widths
        {
            get
            {
                foreach (var width in m_Widths)
                {
                    if (width.DefaultValue == 0)
                    {
                        width.DefaultValue = Width;
                    }
                }

                return m_Widths;
            }
        }

        public override void GenerateMask()
        {
            if (m_SplineContainer.Splines.Count == 0) return;

            // Adjust resolution based on spline characteristics
            bool isStraightLine = m_SplineContainer.Splines.Count == 1 && m_SplineContainer.Splines[0].Count == 2;
            int maskTextureWidth = kMaskResolution;
            int maskTextureHeight = maskTextureWidth;

            RenderTexture renderTexture = RenderTexture.GetTemporary(maskTextureWidth, maskTextureHeight, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            renderTexture.enableRandomWrite = true;

            FindSplineMaskMaterial();

            if (MaskTexture == null)
            {
                MaskTexture = new Texture2D(maskTextureWidth, maskTextureHeight, TextureFormat.ARGB32, false, true);
                MaskTexture.wrapMode = TextureWrapMode.Clamp;
            }
            else if (MaskTexture.width != maskTextureWidth || MaskTexture.height != maskTextureHeight)
            {
                MaskTexture.Reinitialize(maskTextureWidth, maskTextureHeight, TextureFormat.ARGB32, false);
                MaskTexture.Apply();
            }

            // Use our optimized mesh generation
            Mesh splineMesh = GenerateSplineMeshOptimized();

            Bounds meshBounds = splineMesh.bounds;
            Bounds splineBounds = CalculateSplineBoundsWithHeight();

            // Preserve the exact height information from the spline
            LocalBounds = new Bounds(new Vector3(meshBounds.center.x, splineBounds.center.y, meshBounds.center.z),
                new Vector3(meshBounds.size.x, splineBounds.size.y, meshBounds.size.z));
            
            float extentsYPlusOne = Mathf.Max(10.0f, meshBounds.extents.y + 1.0f);
            float largerMeshExtents = math.max(meshBounds.extents.x, meshBounds.extents.z);
            Matrix4x4 projectionMatrix = Matrix4x4.Ortho(-largerMeshExtents, largerMeshExtents, -largerMeshExtents,
                largerMeshExtents, -extentsYPlusOne, extentsYPlusOne);
            projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, false);

            CommandBuffer cmd = new CommandBuffer();
            cmd.SetRenderTarget(renderTexture);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.SetProjectionMatrix(projectionMatrix);

            // This is needed because Unity uses OpenGL conventions for rendering.
            Matrix4x4 lookAtMatrix =
                Matrix4x4.LookAt(meshBounds.center + Vector3.up, meshBounds.center, Vector3.forward);
            Matrix4x4 viewMatrix = lookAtMatrix.inverse;
            cmd.SetViewMatrix(viewMatrix);

            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetColor(kMaterialColorId, Color.white);
            
            // Set the exact Y bounds values to preserve height information
            float actualMinY = splineBounds.min.y;
            float actualMaxY = splineBounds.max.y;
            
            // Ensure these values are never the same to avoid division by zero
            if (Mathf.Approximately(actualMinY, actualMaxY))
            {
                actualMaxY = actualMinY + 0.0001f;
            }
            
            materialPropertyBlock.SetFloat("_LocalBoundsMinY", actualMinY);
            materialPropertyBlock.SetFloat("_LocalBoundsMaxY", actualMaxY);
            materialPropertyBlock.SetFloat("_Width", Width);
            
            // For straight lines, use additional shader property to highlight it's a straight line
            materialPropertyBlock.SetFloat("_IsStraightLine", isStraightLine ? 1.0f : 0.0f);
            
            // Draw the mesh with explicit encoding of height information
            cmd.DrawMesh(splineMesh, Matrix4x4.identity, material: m_SplineToMaskMaterial, 0, 0,
                properties: materialPropertyBlock);
            cmd.CopyTexture(renderTexture, MaskTexture);
            Graphics.ExecuteCommandBuffer(cmd);

            RenderTexture.ReleaseTemporary(renderTexture);
        }

        public override bool ContainsSplineData(SplineData<float> splineData)
        {
            return Widths.Contains(splineData);
        }

        private void OnValidate()
        {
            if (m_SplineContainer == null)
            {
                m_SplineContainer = GetComponent<SplineContainer>();
            }

            FindSplineMaskMaterial();
        }

        private void FindSplineMaskMaterial()
        {
#if UNITY_EDITOR
            if (m_SplineToMaskMaterial == null)
            {
                m_SplineToMaskMaterial = Resources.Load<Material>("Materials/GenerateSplinePathMask");
            }
#endif
        }

        protected void OnEnable()
        {
            m_SplineContainer = GetComponent<SplineContainer>();
            FindSplineMaskMaterial();
        }
        
        // Add debugging method to visualize height encoding
        private void OnDrawGizmosSelected()
        {
            if (!m_DebugMode || m_SplineContainer == null) return;
            
            // Draw gizmos to help visualize spline heights
            Gizmos.color = Color.yellow;
            
            foreach (var spline in m_SplineContainer.Splines)
            {
                if (spline == null || spline.Count < 2) continue;
                
                float length = spline.GetLength();
                int steps = Mathf.Max(20, Mathf.CeilToInt(length));
                
                for (int i = 0; i < steps; i++)
                {
                    float t = (float)i / (steps - 1);
                    
                    SplineUtility.Evaluate(spline, t, out var pos, out var dir, out var up);
                    Vector3 worldPos = transform.TransformPoint(pos);
                    
                    // Draw sphere at each evaluation point
                    Gizmos.DrawSphere(worldPos, 0.2f);
                    
                    // Draw line to visualize height from ground
                    if (i % 5 == 0)
                    {
                        Vector3 groundPos = worldPos;
                        groundPos.y = 0;
                        Gizmos.DrawLine(groundPos, worldPos);
                        
                        // Draw height text
                        UnityEditor.Handles.Label(worldPos + Vector3.up * 0.5f, $"Y: {worldPos.y:F1}");
                    }
                }
            }
        }

        // Add this method to properly store and encode height information in the mask
        private Bounds CalculateSplineBoundsWithHeight()
        {
            if (m_SplineContainer.Splines.Count == 0)
                return new Bounds();

            // First get the regular bounds
            Bounds splineBounds = m_SplineContainer.Splines[0].GetBounds();
            for (int i = 1; i < m_SplineContainer.Splines.Count; i++)
            {
                splineBounds.Encapsulate(m_SplineContainer.Splines[i].GetBounds());
            }

            // For very flat splines, ensure there's at least some height difference
            if (splineBounds.size.y < 0.1f)
            {
                splineBounds.Encapsulate(splineBounds.center + Vector3.up * 0.05f);
                splineBounds.Encapsulate(splineBounds.center - Vector3.up * 0.05f);
            }

            return splineBounds;
        }

        // Add a new optimized mesh generation method that balances quality and performance
        private Mesh GenerateSplineMeshOptimized()
        {
            NativeList<float3> positions = new NativeList<float3>(Allocator.Temp);
            NativeList<float3> normals = new NativeList<float3>(Allocator.Temp);
            NativeList<float2> uvs = new NativeList<float2>(Allocator.Temp);
            NativeList<int> indices = new NativeList<int>(Allocator.Temp);

            for (int i = 0; i < m_SplineContainer.Splines.Count; i++)
            {
                GenerateSplineMeshSegmentsOptimized(m_SplineContainer.Splines[i], i, ref positions, ref normals, ref uvs, ref indices);
            }

            Mesh mesh = new Mesh();
            if (positions.Length > 0)
            {
                mesh.SetVertices(positions.AsArray());
                mesh.SetNormals<float3>(normals.AsArray());
                mesh.SetUVs(0, uvs.AsArray());
                mesh.subMeshCount = 1;
                mesh.SetIndices(indices.AsArray(), MeshTopology.Triangles, 0);
                mesh.UploadMeshData(true);
            }

            positions.Dispose();
            normals.Dispose();
            uvs.Dispose();
            indices.Dispose();

            return mesh;
        }

        // Optimized segment generation for better performance while maintaining quality
        private void GenerateSplineMeshSegmentsOptimized(Spline spline, int widthDataIndex, 
            ref NativeList<float3> positions, ref NativeList<float3> normals, 
            ref NativeList<float2> uvs, ref NativeList<int> indices)
        {
            if (spline == null || spline.Count < 2)
                return;

            float length = spline.GetLength();
            if (length <= 0.001f)
                return;

            bool isStraightLine = spline.Count == 2;
            bool isLongPath = length > 50f;
            
            // PERFORMANCE: Efficient segment density with good quality/performance balance
            float segmentsPerUnit = isStraightLine ? 0.8f : 1.2f;
            
            // For long paths, reduce density to improve performance
            if (isLongPath) {
                segmentsPerUnit = Mathf.Lerp(segmentsPerUnit, 0.5f, Mathf.Clamp01((length - 50f) / 150f));
            }
            
            // Minimum segment count based on length but capped for performance
            int segmentCount = Mathf.Clamp(
                Mathf.CeilToInt(length * segmentsPerUnit),
                isStraightLine ? 8 : 16,  // Minimum segments (fewer for straight lines)
                256                       // Maximum segments to avoid excessive vertices
            );
            
            // For curved paths with tight turns, ensure enough segments
            if (!isStraightLine && spline.Count > 3) {
                // Sample more points for complex paths but still maintain a reasonable cap
                segmentCount = Mathf.Min(segmentCount, 128);
            }
            
            float segmentStepT = 1f / segmentCount;
            int steps = segmentCount + 1;
            
            // Add 4 more vertices for the extra segments (2 at start, 2 at end)
            int vertexCount = (steps + 2) * 2;
            // We need one extra quad at the start and one at the end (12 more indices)
            int triangleCount = (segmentCount + 2) * 6;
            int prevVertexCount = positions.Length;

            positions.Capacity += vertexCount;
            normals.Capacity += vertexCount;
            uvs.Capacity += vertexCount;
            indices.Capacity += triangleCount;

            // Pre-calculate start and end positions for consistent direction
            SplineUtility.Evaluate(spline, 0, out var startPos, out _, out _);
            SplineUtility.Evaluate(spline, 1, out var endPos, out _, out _);
            
            // PERFORMANCE: Calculate UV scaling once
            float uvScaleFactor = isStraightLine ? 1.0f : 
                (length > Width * 10 ? Width / (length * 0.1f) : 1.0f);

            // Define a small delta for the points immediately after start and before end
            float smallDelta = 0.005f;

            // First, add the segment at t=0 with v=0.0f
            AddSegmentVertex(spline, 0f, 0f, true, widthDataIndex, ref positions, ref normals, ref uvs);
            
            // Add the segment immediately after the start with a small delta
            AddSegmentVertex(spline, smallDelta, 1f, false, widthDataIndex, ref positions, ref normals, ref uvs);

            // PERFORMANCE: Sample each point in one loop instead of calculating repeatedly
            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / (steps - 1);
                // Skip the first and last points as we handle them separately
                if (t < smallDelta || t > (1f - smallDelta))
                    continue;
                
                AddSegmentVertex(spline, t, 1f, false, widthDataIndex, ref positions, ref normals, ref uvs);
            }
            
            // Add the segment immediately before the end with a small delta
            AddSegmentVertex(spline, 1f - smallDelta, 1f, false, widthDataIndex, ref positions, ref normals, ref uvs);
            
            // Add the segment at t=1 with v=0.0f
            AddSegmentVertex(spline, 1f, 0f, true, widthDataIndex, ref positions, ref normals, ref uvs);
            
            // Create triangles
            int vertCount = positions.Length - prevVertexCount;
            for (int i = 0; i < vertCount - 2; i += 2)
            {
                int baseIdx = prevVertexCount + i;
                
                indices.Add(baseIdx);
                indices.Add(baseIdx + 3);
                indices.Add(baseIdx + 1);
                
                indices.Add(baseIdx);
                indices.Add(baseIdx + 2);
                indices.Add(baseIdx + 3);
            }
        }
        
        // Helper method to add a segment vertex with proper UV
        private void AddSegmentVertex(Spline spline, float t, float v, bool isEndpoint, int widthDataIndex,
            ref NativeList<float3> positions, ref NativeList<float3> normals, ref NativeList<float2> uvs)
        {
            float3 pos, dir, up;
            
            // Evaluate spline at this point - preserving EXACT height
            SplineUtility.Evaluate(spline, t, out pos, out dir, out up);
            
            // Quick fixes for invalid directions
            if (math.length(dir) < 0.001f)
            {
                SplineUtility.Evaluate(spline, 0, out var startPos, out _, out _);
                SplineUtility.Evaluate(spline, 1, out var endPos, out _, out _);
                
                bool isStraightLine = spline.Count == 2;
                dir = isStraightLine ? 
                    math.normalize(new float3(endPos.x - startPos.x, 0, endPos.z - startPos.z)) : 
                    new float3(0, 0, 1);
            }
            
            // Always use consistent up vector for stability
            up = new float3(0, 1, 0);
            
            // Calculate tangent vector
            var scale = transform.lossyScale;
            var tangent = math.normalizesafe(math.cross(up, dir)) *
                          new float3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            
            if (math.length(tangent) < 0.001f)
            {
                tangent = math.normalizesafe(new float3(dir.z, 0, -dir.x)) * 
                          new float3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            }
            
            // Get width - PERFORMANCE: Simplified width calculation
            float width = Width * 0.5f;
            if (widthDataIndex < m_Widths.Count && m_Widths[widthDataIndex] != null && 
                m_Widths[widthDataIndex].Count > 0)
            {
                width = m_Widths[widthDataIndex].Evaluate(spline, t, PathIndexUnit.Normalized,
                    new UnityEngine.Splines.Interpolators.LerpFloat());
                width = math.clamp(width, .001f, 10000f) * 0.5f;
            }
            
            // Add vertex positions
            float3 leftPos = pos - (tangent * width);
            float3 rightPos = pos + (tangent * width);
            
            // Preserve exact height
            leftPos.y = rightPos.y = pos.y;
            
            // Add positions and normals
            positions.Add(leftPos);
            positions.Add(rightPos);
            normals.Add(up);
            normals.Add(up);
            
            // Set UV with specified v value
            uvs.Add(new float2(-1f, v));
            uvs.Add(new float2(1f, v));
        }
    }
}