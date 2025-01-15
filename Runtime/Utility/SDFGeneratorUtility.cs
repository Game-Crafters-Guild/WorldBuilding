using Unity.Collections;
using UnityEngine;

public static class SDFGeneratorUtility
{
    private const int kMaskTextureWidth = 256;
    private const int kMaskTextureHeight = 256;
    private static readonly int kComputeResultId = Shader.PropertyToID("Result");
    public static Texture GenerateSDFTexture(Texture inputTexture, ref Texture resultTexture)
    {
        ComputeShader sdfComputeShader = Resources.Load<ComputeShader>("Shaders/GenerateSDF");
        int workgroupsX = Mathf.CeilToInt(kMaskTextureWidth / 8.0f);
        int workgroupsY = Mathf.CeilToInt(kMaskTextureHeight / 8.0f);


        int stepSize = kMaskTextureWidth / 2;
        RenderTexture renderTexture = RenderTexture.GetTemporary(kMaskTextureWidth, kMaskTextureHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;
        int kernel = sdfComputeShader.FindKernel("SeedJumpFlood");
        sdfComputeShader.SetTexture(kernel, kComputeResultId, renderTexture);
        sdfComputeShader.SetTexture(kernel, "Input", inputTexture);
        sdfComputeShader.SetVector("TextureSize", new Vector4(kMaskTextureHeight, kMaskTextureWidth, 0.0f, 0.0f));
        sdfComputeShader.SetInt("StepSize", stepSize);
        sdfComputeShader.Dispatch(kernel, workgroupsX, workgroupsY, 1);


        // Perform JumpFlood.
        RenderTexture jumpFloodResultRT = RenderTexture.GetTemporary(kMaskTextureWidth, kMaskTextureHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture jumpFloodInputRT = renderTexture;
        jumpFloodResultRT.enableRandomWrite = true;
        int jumpFloodKernel = sdfComputeShader.FindKernel("JumpFlood");
        sdfComputeShader.SetVector("TextureSize", new Vector4(kMaskTextureHeight, kMaskTextureWidth, 0.0f, 0.0f));
        stepSize = stepSize / 2;
        while (stepSize > 1)
        {
            sdfComputeShader.SetTexture(jumpFloodKernel, kComputeResultId, jumpFloodResultRT);
            sdfComputeShader.SetTexture(jumpFloodKernel, "Input", jumpFloodInputRT);
            sdfComputeShader.SetInt("StepSize", stepSize);
            sdfComputeShader.Dispatch(jumpFloodKernel, workgroupsX, workgroupsY, 1);

            // Swap render buffers.
            (jumpFloodResultRT, jumpFloodInputRT) = (jumpFloodInputRT, jumpFloodResultRT);
            stepSize = stepSize / 2;
        }
        
        // Final step.
        int sdfFromJumpFloodKernel = sdfComputeShader.FindKernel("SdfFromJumpFlood");
        ComputeBuffer furthestDistanceBuffer = new ComputeBuffer(1, sizeof(uint));
        NativeArray<uint> furthestDistance = new NativeArray<uint>(1, Allocator.Temp);
        furthestDistanceBuffer.SetData(furthestDistance);
        furthestDistance.Dispose();
        sdfComputeShader.SetTexture(sdfFromJumpFloodKernel, kComputeResultId, jumpFloodResultRT);
        sdfComputeShader.SetTexture(sdfFromJumpFloodKernel, "Input", jumpFloodInputRT);
        sdfComputeShader.SetBuffer(sdfFromJumpFloodKernel, "furthestDistance", furthestDistanceBuffer);
        sdfComputeShader.SetInt("StepSize", stepSize);
        sdfComputeShader.Dispatch(sdfFromJumpFloodKernel, workgroupsX, workgroupsY, 1);
        
        if (resultTexture == null)
        {
            resultTexture = new Texture2D(kMaskTextureWidth, kMaskTextureHeight, TextureFormat.ARGB32, false, true);
            resultTexture.wrapMode = TextureWrapMode.Clamp;
        }
        
        Graphics.CopyTexture(jumpFloodResultRT, resultTexture);
        resultTexture.wrapMode = TextureWrapMode.Clamp;

        RenderTexture.ReleaseTemporary(jumpFloodInputRT);
        RenderTexture.ReleaseTemporary(jumpFloodResultRT);

        return resultTexture;
    }

    public static Texture InvertBlackWhiteTexture(Texture inputTexture, ref Texture resultTexture)
    {
        ComputeShader sdfComputeShader = Resources.Load<ComputeShader>("Shaders/GenerateSDF");
        int workgroupsX = Mathf.CeilToInt(kMaskTextureWidth / 8.0f);
        int workgroupsY = Mathf.CeilToInt(kMaskTextureHeight / 8.0f);
        int kernel = sdfComputeShader.FindKernel("InvertBlackWhite");
        
        RenderTexture renderTarget = RenderTexture.GetTemporary(kMaskTextureWidth, kMaskTextureHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        renderTarget.enableRandomWrite = true;

        sdfComputeShader.SetTexture(kernel, kComputeResultId, renderTarget);
        sdfComputeShader.SetTexture(kernel, "Input", inputTexture);
        sdfComputeShader.Dispatch(kernel, workgroupsX, workgroupsY, 1);

        if (resultTexture == null)
        {
            resultTexture = new Texture2D(kMaskTextureWidth, kMaskTextureHeight, TextureFormat.ARGB32, false, true);
            resultTexture.wrapMode = TextureWrapMode.Clamp;
        }
        Graphics.CopyTexture(renderTarget, resultTexture);
        RenderTexture.ReleaseTemporary(renderTarget);

        return resultTexture;
    }
}
