using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VRMirrorRenderPassFeature : ScriptableRendererFeature
{
    class VRMirrorRenderPass : ScriptableRenderPass
    {
        public Material material;
        public Transform mirrorCam;
        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
   
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Debug.Log("vr mirror");

            SortingCriteria sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;

            var tags = new List<ShaderTagId>();
            tags.Add(new ShaderTagId("SRPDefaultUnlit"));
            tags.Add(new ShaderTagId("UniversalForward"));
            tags.Add(new ShaderTagId("UniversalForwardOnly"));
            DrawingSettings drawingSettings = CreateDrawingSettings(tags, ref renderingData, sortingCriteria);
            drawingSettings.overrideMaterial = material;
            drawingSettings.overrideMaterialPassIndex = 0;

            var m_FilteringSettings = new FilteringSettings(RenderQueueRange.all, LayerMask.GetMask("Default"));
            var m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);


            var camera = renderingData.cameraData.camera;
            var originPos = camera.transform.position;
            camera.transform.position = new Vector3(0,0,0);
            context.SetupCameraProperties(camera, true);
            // Render the objects...
            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings, ref m_RenderStateBlock);
            camera.transform.position = originPos;
            context.SetupCameraProperties(camera, true);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
    }

    VRMirrorRenderPass m_ScriptablePass;
    public Material overrideMaterial = null;

    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new VRMirrorRenderPass();
        m_ScriptablePass.material = overrideMaterial;
        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }
}


