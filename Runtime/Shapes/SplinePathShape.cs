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
        [SerializeField, HideInInspector] private ComputeShader m_BlurComputeShader;
        
        [Tooltip("Higher resolution gives smoother edges but uses more memory")]
        [SerializeField] private int m_MaskResolution = 256;
        
        [Tooltip("Number of blur passes, higher values give smoother edges")]
        [SerializeField, Range(1, 10)] private int m_BlurPasses = 3;
        
        [Tooltip("Blur strength, higher values give smoother but more spread out edges")]
        [SerializeField, Range(1, 10)] private int m_BlurStrength = 5;

        private static readonly int kMaterialColorId = Shader.PropertyToID("_Color");
        private static readonly int kBlurStrengthId = Shader.PropertyToID("_BlurStrength");

        [SerializeField] List<SplineData<float>> m_Widths = new List<SplineData<float>>();

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

            int maskTextureWidth = m_MaskResolution;
            int maskTextureHeight = m_MaskResolution;

            RenderTexture renderTexture = RenderTexture.GetTemporary(maskTextureWidth, maskTextureHeight, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            renderTexture.enableRandomWrite = true;

            FindSplineMaskMaterial();
            FindBlurComputeShader();

            MaskTexture = new Texture2D(maskTextureWidth, maskTextureHeight, TextureFormat.ARGB32, false, true);
            MaskTexture.wrapMode = TextureWrapMode.Clamp;

            Mesh splineMesh = GenerateSplineMesh();

            Bounds meshBounds = splineMesh.bounds;
            Bounds splineBounds = m_SplineContainer.Splines[0].GetBounds();
            for (int i = 1; i < m_SplineContainer.Splines.Count; i++)
            {
                splineBounds.Encapsulate(m_SplineContainer.Splines[i].GetBounds());
            }

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
            materialPropertyBlock.SetFloat("_LocalBoundsMinY", splineBounds.min.y);
            materialPropertyBlock.SetFloat("_LocalBoundsMaxY",
                splineBounds.min.y == splineBounds.max.y ? (splineBounds.max.y + 0.0001f) : splineBounds.max.y);
            cmd.DrawMesh(splineMesh, Matrix4x4.identity, material: m_SplineToMaskMaterial, 0, 0,
                properties: materialPropertyBlock);
            cmd.CopyTexture(renderTexture, MaskTexture);
            Graphics.ExecuteCommandBuffer(cmd);

            // Apply blur to smooth out the jagged edges
            if (m_BlurComputeShader != null)
            {
                try
                {
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
                    m_BlurStrength = Mathf.Clamp(m_BlurStrength, 1, 10);
                    m_BlurComputeShader.SetInt(kBlurStrengthId, m_BlurStrength);

                    // Limit blur passes to avoid potential issues
                    int blurPasses = Mathf.Clamp(m_BlurPasses, 1, 10);
                    
                    // Multi-pass blur with ping-pong rendering
                    RenderTexture currentSource = blurTextureA;
                    RenderTexture currentTarget = blurTextureB;
                    
                    for (int pass = 0; pass < blurPasses; pass++)
                    {
                        // Set textures for this pass
                        m_BlurComputeShader.SetTexture(blurKernel, "Source", currentSource);
                        m_BlurComputeShader.SetTexture(blurKernel, "Result", currentTarget);
                        
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
            }

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
            FindBlurComputeShader();
            
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

        private void FindBlurComputeShader()
        {
#if UNITY_EDITOR
            if (m_BlurComputeShader == null)
            {
                m_BlurComputeShader = Resources.Load<ComputeShader>("Shaders/SplinePathBlurShader");
            }
#endif
        }

        protected void OnEnable()
        {
            m_SplineContainer = GetComponent<SplineContainer>();
            FindSplineMaskMaterial();
            FindBlurComputeShader();
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

            const int kSegmentsPerMeter = 10;
            var segmentsPerLength = kSegmentsPerMeter * length;
            var segments = Mathf.CeilToInt(segmentsPerLength);
            var segmentStepT = (1f / kSegmentsPerMeter) / length;
            var steps = segments + 1;
            var vertexCount = steps * 2;
            var triangleCount = segments * 6;
            var prevVertexCount = positions.Length;

            positions.Capacity += vertexCount;
            normals.Capacity += vertexCount;
            uvs.Capacity += vertexCount;
            indices.Capacity += triangleCount;

            var t = 0f;
            for (int i = 0; i < steps; i++)
            {
                SplineUtility.Evaluate(spline, t, out var pos, out var dir, out var up);

                // If dir evaluates to zero (linear or broken zero length tangents?)
                // then attempt to advance forward by a small amount and build direction to that point
                if (math.length(dir) == 0)
                {
                    var nextPos = spline.GetPointAtLinearDistance(t, 0.01f, out _);
                    dir = math.normalizesafe(nextPos - pos);

                    if (math.length(dir) == 0)
                    {
                        nextPos = spline.GetPointAtLinearDistance(t, -0.01f, out _);
                        dir = -math.normalizesafe(nextPos - pos);
                    }

                    if (math.length(dir) == 0)
                        dir = new float3(0, 0, 1);
                }

                var scale = transform.lossyScale;
                var tangent = math.normalizesafe(math.cross(up, dir)) *
                              new float3(1f / scale.x, 1f / scale.y, 1f / scale.z);

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

                positions.Add(pos - (tangent * w));
                positions.Add(pos + (tangent * w));
                normals.Add(up);
                normals.Add(up);
                float v = math.min(t <= 0.5 ? ((t * length) / Width) : ((1.0f - t) * length) / Width, Width);
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
    }
}