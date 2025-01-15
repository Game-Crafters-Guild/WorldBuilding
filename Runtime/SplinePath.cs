using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.Splines;

[RequireComponent(typeof(SplineContainer))]
[ExecuteInEditMode]
public class SplinePath : BaseWorldBuilder
{
    private static readonly int kMaterialColorId = Shader.PropertyToID("_Color");

    [Range(1.0f, 100.0f)]
    public float Width = 4.0f;
    [SerializeReference][HideInInspector]
    private SplineContainer m_SplineContainer;
    public override IReadOnlyList<Spline> Splines => m_SplineContainer.Splines;
    
    // Mask Texture.
    [SerializeField] private Texture m_MaskTexture;
    [SerializeField] private Material m_SplineToMaskMaterial;
    private const int kMaskTextureWidth = 256;
    private const int kMaskTextureHeight = 256;
    private Bounds m_WorldBounds;
    public override Bounds WorldBounds => m_WorldBounds;

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
            m_SplineToMaskMaterial = Resources.Load<Material>("Materials/Unlit");
        }
#endif
    }

    public override void GenerateMask(RenderTexture renderTexture)
    {
        if (m_MaskTexture == null)
        {
            m_MaskTexture = new Texture2D(kMaskTextureWidth, kMaskTextureHeight, TextureFormat.ARGB32, false, true);
            m_MaskTexture.wrapMode = TextureWrapMode.Clamp;
        }

        Mesh splineMesh = new Mesh();
        SplineMesh.Extrude(m_SplineContainer.Splines, splineMesh, radius: Width * 0.5f, sides: 2, segmentsPerUnit: 10, capped: false,
            new float2(0.0f, 1.0f));

        Bounds meshBounds = splineMesh.bounds;
        m_WorldBounds = meshBounds;
        m_WorldBounds.center = transform.TransformPoint(m_WorldBounds.center);
        float largerMeshExtents = math.max(meshBounds.extents.x, meshBounds.extents.z);
        Matrix4x4 projectionMatrix = Matrix4x4.Ortho(-largerMeshExtents, largerMeshExtents, -largerMeshExtents,
            largerMeshExtents, Mathf.Min(-10.0f, meshBounds.min.y - 1.0f), Mathf.Max(10.0f, meshBounds.max.y + 1.0f));

        CommandBuffer cmd = new CommandBuffer();
        cmd.SetRenderTarget(renderTexture);
        cmd.ClearRenderTarget(true, true, Color.clear);
        cmd.SetProjectionMatrix(projectionMatrix);

        // This is needed because Unity uses OpenGL conventions for rendering.
        Matrix4x4 viewScaleMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
            new Vector3(1, 1, SystemInfo.usesReversedZBuffer ? 1 : -1));
        Matrix4x4 lookAtMatrix = Matrix4x4.LookAt(meshBounds.center + Vector3.up, meshBounds.center, Vector3.forward);
        Matrix4x4 viewMatrix = viewScaleMatrix * lookAtMatrix.inverse;
        cmd.SetViewMatrix(viewMatrix);

        MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
        materialPropertyBlock.SetColor(kMaterialColorId, Color.white);
        cmd.DrawMesh(splineMesh, Matrix4x4.identity, material: m_SplineToMaskMaterial, 0, 0, properties: materialPropertyBlock);
        Graphics.ExecuteCommandBuffer(cmd);
        
        m_MaskTexture = SDFGeneratorUtility.InvertBlackWhiteTexture(renderTexture, ref m_MaskTexture);
        m_MaskTexture = SDFGeneratorUtility.GenerateSDFTexture(m_MaskTexture, ref m_MaskTexture);
    }

    public override void ApplyHeights(WorldBuildingContext context)
    {
        foreach (var heightModifier in m_Modifiers.TerrainHeightModifiers)
        {
            context.MaskFalloff = new MaskFalloff();
            heightModifier.ApplyHeightmap(context, WorldBounds, m_MaskTexture);
        }
    }

    public override void ApplySplatmap(WorldBuildingContext context)
    {
        foreach (var splatModifier in m_Modifiers.TerrainSplatModifiers)
        {
            context.MaskFalloff = new MaskFalloff();
            splatModifier.ApplySplatmap(context, WorldBounds, m_MaskTexture);
        }
    }
    public override void ProcessSpline(Spline spline)
    {
        /*if (m_SplineContainer == null)
        {
            m_SplineContainer = GetComponent<SplineContainer>();
        }
        if (m_SplineCache == null)
        {
            m_SplineCache = new SplineCache();
        }
        m_SplineCache.BakePath(m_SplineContainer, Resolution);*/
    }

    public override void SpawnGameObjects(WorldBuildingContext context)
    {
        
    }
}
