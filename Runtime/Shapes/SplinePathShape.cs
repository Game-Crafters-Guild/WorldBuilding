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
        //[SerializeField, HideInInspector] private ComputeShader m_BlurComputeShader;
        
        [Tooltip("Higher resolution gives smoother edges but uses more memory")]
        [SerializeField] private int m_MaskResolution = 256;
        
        /*[Tooltip("Number of blur passes, higher values give smoother edges")]
        [SerializeField, Range(1, 10)] private int m_BlurPasses = 3;
        
        [Tooltip("Blur strength, higher values give smoother but more spread out edges")]
        [SerializeField, Range(1, 10)] private int m_BlurStrength = 5;*/

        private static readonly int kMaterialColorId = Shader.PropertyToID("_Color");
        private static readonly int kBlurStrengthId = Shader.PropertyToID("_BlurStrength");

        [SerializeField] List<SplineData<float>> m_Widths = new List<SplineData<float>>();

        [SerializeField] private bool m_DebugMode = false;
        [SerializeField] private bool m_ForceExactHeights = true;

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
            float splineLength = 0;
            for (int i = 0; i < m_SplineContainer.Splines.Count; i++)
            {
                splineLength += m_SplineContainer.Splines[i].GetLength();
            }
            bool isLongPath = splineLength > 50f;
            
            // PERFORMANCE: Use adaptive resolution based on path length
            // Smaller resolution for better performance, still high enough for quality
            int maskTextureWidth;
            if (isLongPath) {
                // For very long paths, use reasonable scaling (not too high)
                maskTextureWidth = Mathf.Min(1024, Mathf.CeilToInt(splineLength * 4));
            } else {
                maskTextureWidth = Mathf.Min(1024, Mathf.Max(512, m_MaskResolution));
            }
            
            // Make power of 2 for better GPU performance
            maskTextureWidth = Mathf.NextPowerOfTwo(maskTextureWidth);
            int maskTextureHeight = maskTextureWidth;

            RenderTexture renderTexture = RenderTexture.GetTemporary(maskTextureWidth, maskTextureHeight, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            renderTexture.enableRandomWrite = true;

            FindSplineMaskMaterial();

            MaskTexture = new Texture2D(maskTextureWidth, maskTextureHeight, TextureFormat.ARGB32, false, true);
            MaskTexture.wrapMode = TextureWrapMode.Clamp;

            // Use our optimized mesh generation
            Mesh splineMesh = GenerateSplineMeshOptimized();

            Bounds meshBounds = splineMesh.bounds;
            Bounds splineBounds = CalculateSplineBoundsWithHeight();

            // CRITICAL FIX: Preserve the exact height information from the spline
            LocalBounds = new Bounds(new Vector3(meshBounds.center.x, splineBounds.center.y, meshBounds.center.z),
                new Vector3(meshBounds.size.x, splineBounds.size.y, meshBounds.size.z));
            
            float extentsYPlusOne = Mathf.Max(10.0f, meshBounds.extents.y + 1.0f);
            float largerMeshExtents = math.max(meshBounds.extents.x, meshBounds.extents.z);
            Matrix4x4 projectionMatrix = Matrix4x4.Ortho(-largerMeshExtents, largerMeshExtents, -largerMeshExtents,
                largerMeshExtents, -extentsYPlusOne, extentsYPlusOne);
            projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, false);

            CommandBuffer cmd = new CommandBuffer();
            cmd.SetRenderTarget(renderTexture);
            cmd.ClearRenderTarget(true, true, Color.clear);
            cmd.SetProjectionMatrix(projectionMatrix);

            // This is needed because Unity uses OpenGL conventions for rendering.
            Matrix4x4 lookAtMatrix =
                Matrix4x4.LookAt(meshBounds.center + Vector3.up, meshBounds.center, Vector3.forward);
            Matrix4x4 viewMatrix = lookAtMatrix.inverse;
            cmd.SetViewMatrix(viewMatrix);

            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetColor(kMaterialColorId, Color.white);
            
            // CRITICAL FIX: Set the exact Y bounds values to preserve height information
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

            // PERFORMANCE: Use faster blur with fewer passes for better performance
            /*if (m_BlurComputeShader != null)
            {
                try
                {
                    // Reduce blur passes for performance, still maintain quality
                    int blurPasses = isStraightLine ? 2 : Mathf.Min(m_BlurPasses, 2);
                    float blurStrength = isStraightLine ? Mathf.Min(m_BlurStrength, 3) : Mathf.Min(m_BlurStrength, 2);
                    
                    // Create two temporary textures for ping-pong blurring
                    RenderTexture blurTextureA = RenderTexture.GetTemporary(maskTextureWidth, maskTextureHeight, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    RenderTexture blurTextureB = RenderTexture.GetTemporary(maskTextureWidth, maskTextureHeight, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    
                    blurTextureA.enableRandomWrite = true;
                    blurTextureB.enableRandomWrite = true;
                    
                    // Create and activate the textures to ensure they're valid
                    blurTextureA.Create();
                    blurTextureB.Create();
                    
                    // Initial copy
                    Graphics.Blit(renderTexture, blurTextureA);

                    // Calculate dispatch dimensions - ensure at least 1 workgroup
                    int workgroupsX = Mathf.Max(1, Mathf.CeilToInt(maskTextureWidth / 8.0f));
                    int workgroupsY = Mathf.Max(1, Mathf.CeilToInt(maskTextureHeight / 8.0f));
                    
                    // Verify kernel exists
                    int blurKernel = m_BlurComputeShader.FindKernel("CSBlurMask");
                    
                    // Set blur strength - clamp to reasonable values
                    int clampedBlurStrength = Mathf.Clamp(Mathf.RoundToInt(blurStrength), 1, 5);
                    m_BlurComputeShader.SetInt(kBlurStrengthId, clampedBlurStrength);

                    // Limit blur passes to avoid potential issues
                    int clampedBlurPasses = Mathf.Clamp(blurPasses, 1, 3);
                    
                    // Multi-pass blur with ping-pong rendering
                    RenderTexture currentSource = blurTextureA;
                    RenderTexture currentTarget = blurTextureB;
                    
                    for (int pass = 0; pass < clampedBlurPasses; pass++)
                    {
                        // Set textures for this pass
                        m_BlurComputeShader.SetTexture(blurKernel, "Source", currentSource);
                        m_BlurComputeShader.SetTexture(blurKernel, "Result", currentTarget);
                        
                        // For straight lines, we can use special flag in shader
                        m_BlurComputeShader.SetInt("_IsStraightLine", isStraightLine ? 1 : 0);
                        
                        // Dispatch the shader
                        m_BlurComputeShader.Dispatch(blurKernel, workgroupsX, workgroupsY, 1);
                        
                        // Swap textures for next pass
                        RenderTexture temp = currentSource;
                        currentSource = currentTarget;
                        currentTarget = temp;
                    }
                    
                    // Copy the final blurred result back to our mask texture
                    Graphics.CopyTexture(currentSource, MaskTexture);
                    
                    // Release temporary RenderTextures
                    RenderTexture.ReleaseTemporary(blurTextureA);
                    RenderTexture.ReleaseTemporary(blurTextureB);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error during spline path blur: {e.Message}");
                    // Fallback - just use the unblurred texture
                    Graphics.CopyTexture(renderTexture, MaskTexture);
                }
            }*/

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
            
            // Ensure mask resolution is a power of 2
            m_MaskResolution = Mathf.NextPowerOfTwo(Mathf.Clamp(m_MaskResolution, 512, 4096));
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

        private Mesh GenerateSplineMesh()
        {
            NativeList<float3> positions = new NativeList<float3>(Allocator.Temp);
            NativeList<float3> normals = new NativeList<float3>(Allocator.Temp);
            NativeList<float2> uvs = new NativeList<float2>(Allocator.Temp);
            NativeList<int> indices = new NativeList<int>(Allocator.Temp);

            for (int i = 0; i < m_SplineContainer.Splines.Count; i++)
            {
                GenerateSplineMesh(m_SplineContainer.Splines[i], i, ref positions, ref normals, ref uvs, ref indices);
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

        private void GenerateSplineMesh(Spline spline, int widthDataIndex, ref NativeList<float3> positions,
            ref NativeList<float3> normals, ref NativeList<float2> uvs, ref NativeList<int> indices)
        {
            if (spline == null || spline.Count < 2)
                return;

            float length = spline.GetLength();

            if (length <= 0.001f)
                return;

            // Special handling for straight lines with just 2 points
            bool isStraightLine = spline.Count == 2;
            
            // For straight lines with just 2 points, ensure we have enough segments
            int kBaseSegmentsPerMeter = isStraightLine ? 15 : 10;
            
            // Scale down segments for very long splines to prevent too many vertices
            float segmentDensity = Mathf.Min(kBaseSegmentsPerMeter, kBaseSegmentsPerMeter * 50f / Mathf.Max(50f, length));
            
            // Ensure a minimum number of segments for short straight lines
            if (isStraightLine)
            {
                segmentDensity = Mathf.Max(segmentDensity, 1.0f);
            }
            
            var segmentsPerLength = segmentDensity * length;
            var segments = Mathf.Max(8, Mathf.CeilToInt(segmentsPerLength)); // Ensure at least 8 segments
            var segmentStepT = 1f / segments; // Use normalized spacing
            var steps = segments + 1;
            var vertexCount = steps * 2;
            var triangleCount = segments * 6;
            var prevVertexCount = positions.Length;

            positions.Capacity += vertexCount;
            normals.Capacity += vertexCount;
            uvs.Capacity += vertexCount;
            indices.Capacity += triangleCount;

            // For straight paths, compute the direction once
            float3 straightDirection = float3.zero;
            float3 straightUp = new float3(0, 1, 0);
            
            if (isStraightLine)
            {
                // Get start and end points
                SplineUtility.Evaluate(spline, 0, out var startPos, out _, out _);
                SplineUtility.Evaluate(spline, 1, out var endPos, out _, out _);
                
                // Calculate direction vector
                straightDirection = math.normalize(endPos - startPos);
                
                // If direction is nearly vertical, use a different up vector
                if (math.abs(math.dot(straightDirection, new float3(0, 1, 0))) > 0.9f)
                {
                    straightUp = new float3(1, 0, 0);
                }
            }

            // Calculate the appropriate scale factor for UVs to prevent stretching on long paths
            float uvScaleFactor = Mathf.Min(1f, 10f / Mathf.Sqrt(length));

            var t = 0f;
            for (int i = 0; i < steps; i++)
            {
                float3 pos, dir, up;
                
                // Handle direction calculation differently for straight lines
                if (isStraightLine)
                {
                    // For straight lines, interpolate position directly
                    SplineUtility.Evaluate(spline, t, out pos, out _, out _);
                    dir = straightDirection;
                    up = straightUp;
                }
                else
                {
                    // Normal spline evaluation
                    SplineUtility.Evaluate(spline, t, out pos, out dir, out up);
                }

                // If dir evaluates to zero (could happen with linear splines sometimes)
                if (math.length(dir) < 0.001f)
                {
                    if (isStraightLine)
                    {
                        dir = straightDirection;
                    }
                    else
                    {
                        // Try to calculate direction by sampling nearby points
                        var nextPos = spline.GetPointAtLinearDistance(t, 0.01f, out _);
                        dir = math.normalizesafe(nextPos - pos);

                        if (math.length(dir) < 0.001f)
                        {
                            nextPos = spline.GetPointAtLinearDistance(t, -0.01f, out _);
                            dir = -math.normalizesafe(nextPos - pos);
                        }

                        if (math.length(dir) < 0.001f)
                            dir = new float3(0, 0, 1);
                    }
                }

                var scale = transform.lossyScale;
                var tangent = math.normalizesafe(math.cross(up, dir)) *
                              new float3(1f / scale.x, 1f / scale.y, 1f / scale.z);
                              
                // Check if tangent is valid (could be zero if dir and up are parallel)
                if (math.length(tangent) < 0.001f)
                {
                    // Try alternate up vector
                    float3 altUp = math.abs(dir.y) > 0.9f ? new float3(1, 0, 0) : new float3(0, 1, 0);
                    tangent = math.normalizesafe(math.cross(altUp, dir)) *
                              new float3(1f / scale.x, 1f / scale.y, 1f / scale.z);
                }

                var w = Width * 0.5f;
                if (widthDataIndex < m_Widths.Count)
                {
                    w = m_Widths[widthDataIndex].DefaultValue;
                    if (m_Widths[widthDataIndex] != null && m_Widths[widthDataIndex].Count > 0)
                    {
                        w = m_Widths[widthDataIndex].Evaluate(spline, t, PathIndexUnit.Normalized,
                            new UnityEngine.Splines.Interpolators.LerpFloat());
                        w = math.clamp(w, .001f, 10000f) * 0.5f;
                    }
                }
                
                // For straight lines, ensure minimum width for better visibility
                if (isStraightLine)
                {
                    w = math.max(w, 0.5f);
                }

                positions.Add(pos - (tangent * w));
                positions.Add(pos + (tangent * w));
                normals.Add(up);
                normals.Add(up);

                // Improved UV mapping
                float v = 1.0f;
                /*if (isStraightLine)
                {
                    // For straight lines, simple linear mapping works well
                    v = t;
                }
                else if (length > Width * 2)
                {
                    // For long paths, use repeating pattern
                    float normalizedDistance = t * length / Width;
                    v = (normalizedDistance * uvScaleFactor) % 1.0f;
                }
                else
                {
                    // For shorter paths
                    v = t;
                }*/

                uvs.Add(new Vector2(-1f, v));
                uvs.Add(new Vector2(1f, v));

                t = math.min(1f, t + segmentStepT);
            }

            for (int i = 0, n = prevVertexCount; i < triangleCount; i += 6, n += 2)
            {
                indices.Add((n + 2) % (prevVertexCount + vertexCount));
                indices.Add((n + 1) % (prevVertexCount + vertexCount));
                indices.Add((n + 0) % (prevVertexCount + vertexCount));
                indices.Add((n + 2) % (prevVertexCount + vertexCount));
                indices.Add((n + 3) % (prevVertexCount + vertexCount));
                indices.Add((n + 1) % (prevVertexCount + vertexCount));
            }
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
            int vertexCount = steps * 2;
            int triangleCount = segmentCount * 6;
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

            // PERFORMANCE: Sample each point in one loop instead of calculating repeatedly
            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / (steps - 1);
                float3 pos, dir, up;
                
                // Evaluate spline at this point - preserving EXACT height
                SplineUtility.Evaluate(spline, t, out pos, out dir, out up);
                
                // Quick fixes for invalid directions
                if (math.length(dir) < 0.001f)
                {
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
                
                // Simplified UV calculation
                //float v = isStraightLine ? t : (t * length / Width) * uvScaleFactor;
                float v = 1.0f;
                uvs.Add(new float2(-1f, v));
                uvs.Add(new float2(1f, v));
            }
            
            // Create triangles
            for (int i = 0, n = prevVertexCount; i < triangleCount; i += 6, n += 2)
            {
                indices.Add((n + 2) % (prevVertexCount + vertexCount));
                indices.Add((n + 1) % (prevVertexCount + vertexCount));
                indices.Add((n + 0) % (prevVertexCount + vertexCount));
                indices.Add((n + 2) % (prevVertexCount + vertexCount));
                indices.Add((n + 3) % (prevVertexCount + vertexCount));
                indices.Add((n + 1) % (prevVertexCount + vertexCount));
            }
        }
    }
}