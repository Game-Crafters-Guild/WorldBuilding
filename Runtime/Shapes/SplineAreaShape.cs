using Unity.Collections;
using UnityEngine;
using UnityEngine.Splines;

namespace GameCraftersGuild.WorldBuilding
{
    [RequireComponent(typeof(SplineContainer))]
    public class SplineAreaShape : StampShape
    {
        [SerializeReference] [HideInInspector] SplineContainer m_SplineContainer;
        public override SplineContainer SplineContainer => m_SplineContainer;

        private const int kMaskTextureWidth = 256;
        private const int kMaskTextureHeight = 256;

        // Store the bounds used for mask generation
        private Vector3 m_MaskGenBoundsMin;
        private Vector3 m_MaskGenBoundsSize;

        // Shader Parameters.
        [SerializeField, HideInInspector] private ComputeShader m_CreateSplineAreaTextureComputeShader;
        private static readonly int kComputeResultId = Shader.PropertyToID("Result");
        private static readonly int kComputeRegionMinId = Shader.PropertyToID("RegionMin");
        private static readonly int kComputeRegionSizeId = Shader.PropertyToID("RegionSize");
        private static readonly int kComputeNumPositionsId = Shader.PropertyToID("NumPositions");
        private static readonly int kComputeSplinePositions = Shader.PropertyToID("SplinePositions");

        public override bool MaintainMaskAspectRatio { get; } = false;

        private void OnValidate()
        {
            if (m_SplineContainer == null)
            {
                m_SplineContainer = GetComponent<SplineContainer>();
            }

            FindComputeShader();
        }

        private void FindComputeShader()
        {
#if UNITY_EDITOR
            if (m_CreateSplineAreaTextureComputeShader == null)
            {
                m_CreateSplineAreaTextureComputeShader =
                    Resources.Load<ComputeShader>("Shaders/SplineAreaComputeShader");
            }
#endif
        }

        protected void OnEnable()
        {
            m_SplineContainer = GetComponent<SplineContainer>();
            FindComputeShader();
        }

        private void CalculateWorldBounds()
        {
            if (m_SplineContainer.Splines.Count == 0)
            {
                LocalBounds = new Bounds();
                return;
            }

            Bounds splineBounds = m_SplineContainer.Splines[0].GetBounds();
            for (int i = 1; i < m_SplineContainer.Splines.Count; i++)
            {
                splineBounds.Encapsulate(m_SplineContainer.Splines[i].GetBounds());
            }

            LocalBounds = splineBounds;
        }

        public override void GenerateMask()
        {
            var renderTextureDesc = new RenderTextureDescriptor(kMaskTextureWidth, kMaskTextureHeight)
            {
                depthBufferBits = 0,
                colorFormat = RenderTextureFormat.ARGB32,
                sRGB = false,
                enableRandomWrite = true
            };
            RenderTexture renderTexture = RenderTexture.GetTemporary(renderTextureDesc);

            CalculateWorldBounds();
            MaskTexture = new Texture2D(kMaskTextureWidth, kMaskTextureHeight, TextureFormat.ARGB32, false, true);
            MaskTexture.wrapMode = TextureWrapMode.Clamp;

            FindComputeShader();

            Spline spline = m_SplineContainer.Spline;
            if (spline.Count <= 1)
            {
                return;
            }

            bool wasClosedShape = m_SplineContainer.Spline.Closed;
            spline.Closed = true;
            Bounds splineBounds = spline.GetBounds();
            if (splineBounds.size.x > splineBounds.size.z)
            {
                splineBounds.size = new Vector3(splineBounds.size.x, splineBounds.size.y, splineBounds.size.x);
            }
            else
            {
                splineBounds.size = new Vector3(splineBounds.size.z, splineBounds.size.y, splineBounds.size.z);
            }

            // Store the bounds used for generation
            m_MaskGenBoundsMin = splineBounds.min;
            m_MaskGenBoundsSize = splineBounds.size;

            //
            // Evaluate the positions on the spline.
            //
            int evaluateSplinePositionsKernel =
                m_CreateSplineAreaTextureComputeShader.FindKernel("EvaluateSplinePositions");
            float kSplineEvaluationResolution = 0.2f;
            float splineLength = spline.GetLength();
            float numPointsInSplineWithFraction = splineLength / kSplineEvaluationResolution + 1;
            int numSplinePoints = Mathf.CeilToInt(numPointsInSplineWithFraction / 64.0f) * 64;

            // Create a buffer for the spline points
            ComputeBuffer splinePointsComputeBuffer = new ComputeBuffer(numSplinePoints, sizeof(float) * 3);

            // Create the Unity Spline buffer.
            var splineBuffers = new SplineComputeBufferScope<Spline>(spline);
            splineBuffers.Bind(m_CreateSplineAreaTextureComputeShader, evaluateSplinePositionsKernel, "info", "curves",
                "curveLengths");
            splineBuffers.Upload();

            m_CreateSplineAreaTextureComputeShader.SetInt(kComputeNumPositionsId, numSplinePoints);
            m_CreateSplineAreaTextureComputeShader.SetBuffer(evaluateSplinePositionsKernel, kComputeSplinePositions,
                splinePointsComputeBuffer);
            m_CreateSplineAreaTextureComputeShader.Dispatch(evaluateSplinePositionsKernel,
                Mathf.CeilToInt(numSplinePoints / 64.0f), 1, 1);
            splineBuffers.Dispose();

            //
            // Create the distance field.
            //
            int workgroupsX = Mathf.CeilToInt(kMaskTextureWidth / 8.0f);
            int workgroupsY = Mathf.CeilToInt(kMaskTextureHeight / 8.0f);
            int kernel = m_CreateSplineAreaTextureComputeShader.FindKernel("CSCreateSplineAreaMask");
            ComputeBuffer furthestDistanceBuffer = new ComputeBuffer(1, sizeof(uint));
            NativeArray<uint> furthestDistance =
                new NativeArray<uint>(1, Allocator.Temp, NativeArrayOptions.ClearMemory);
            furthestDistanceBuffer.SetData(furthestDistance);
            furthestDistance.Dispose();
            m_CreateSplineAreaTextureComputeShader.SetBuffer(kernel, "furthestDistance", furthestDistanceBuffer);
            m_CreateSplineAreaTextureComputeShader.SetTexture(kernel, kComputeResultId, renderTexture);
            m_CreateSplineAreaTextureComputeShader.SetVector(kComputeRegionMinId, splineBounds.min);
            m_CreateSplineAreaTextureComputeShader.SetVector(kComputeRegionSizeId, splineBounds.size);
            m_CreateSplineAreaTextureComputeShader.SetInt(kComputeNumPositionsId, numSplinePoints);
            m_CreateSplineAreaTextureComputeShader.SetBuffer(kernel, kComputeSplinePositions,
                splinePointsComputeBuffer);
            m_CreateSplineAreaTextureComputeShader.Dispatch(kernel, workgroupsX, workgroupsY, 1);
            splinePointsComputeBuffer.Release();

            // Normalize the distances.
            int normalizeDistancesKernel =
                m_CreateSplineAreaTextureComputeShader.FindKernel("CSCalculateNormalizedDistances");
            m_CreateSplineAreaTextureComputeShader.SetBuffer(normalizeDistancesKernel, "furthestDistance",
                furthestDistanceBuffer);
            m_CreateSplineAreaTextureComputeShader.SetTexture(normalizeDistancesKernel, kComputeResultId,
                renderTexture);
            m_CreateSplineAreaTextureComputeShader.Dispatch(normalizeDistancesKernel, workgroupsX, workgroupsY, 1);

            furthestDistanceBuffer.Dispose();
            Graphics.CopyTexture(renderTexture, MaskTexture);

            // Blur the SDF.
            int blurSDFKernel = m_CreateSplineAreaTextureComputeShader.FindKernel("CSBlurSDF");
            m_CreateSplineAreaTextureComputeShader.SetTexture(blurSDFKernel, kComputeResultId, renderTexture);
            m_CreateSplineAreaTextureComputeShader.SetTexture(blurSDFKernel, "SDF", MaskTexture);
            m_CreateSplineAreaTextureComputeShader.Dispatch(blurSDFKernel, workgroupsX, workgroupsY, 1);

            Graphics.CopyTexture(renderTexture, MaskTexture);
            RenderTexture.ReleaseTemporary(renderTexture);

            spline.Closed = wasClosedShape;
        }

        // Override properties to return the specific bounds used for mask generation
        public override Vector3 MaskGenerationBoundsMin => m_MaskGenBoundsMin;
        public override Vector3 MaskGenerationBoundsSize => m_MaskGenBoundsSize;
    }
}