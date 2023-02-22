using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VRMirrorRenderPassFeature : ScriptableRendererFeature
{
    class VRMirrorRenderPass : ScriptableRenderPass
    {
        static readonly int s_DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");

        public Shader VRMirrorShader;
        public List<Transform> Mirrors = new List<Transform>();

        private Material[] _materials;
        private List<ShaderTagId> _tags;
        private FilteringSettings _OpaqueFilteringSettings;
        private FilteringSettings _TransparentFilteringSettings;
        private RenderStateBlock _RenderStateBlock;
        private Plane[] _cullingPlanes;
        public void Setup()
        {
            _cullingPlanes = new Plane[6];
            _tags = new List<ShaderTagId>();
            _tags.Add(new ShaderTagId("SRPDefaultUnlit"));
            _tags.Add(new ShaderTagId("UniversalForward"));
            _tags.Add(new ShaderTagId("UniversalForwardOnly"));

            _OpaqueFilteringSettings = new FilteringSettings(RenderQueueRange.opaque, LayerMask.GetMask("Default"));
            _TransparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent, LayerMask.GetMask("Default"));
            _RenderStateBlock = new RenderStateBlock(RenderStateMask.Raster | RenderStateMask.Depth | RenderStateMask.Stencil);
            _RenderStateBlock.rasterState = new RasterState(CullMode.Front);
            _RenderStateBlock.depthState = new DepthState(true, CompareFunction.Less);
            _RenderStateBlock.stencilState = new StencilState(true, 255, 255, CompareFunction.Equal);

            _materials = new Material[8];
            for (var i = 0; i < 8; i++)
            {
                _materials[i] = new Material(VRMirrorShader);
                _materials[i].SetInt("_StencilRef", 1 << i);
            }
        }
        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }


        private Matrix4x4 MoveNearToPlane(Matrix4x4 mvp)
        {
            Matrix4x4 projectionJitter = Matrix4x4.identity;
            float ka1 = mvp[3, 1] * mvp[0, 0] - mvp[3, 0] * mvp[0, 1];
            float ka2 = mvp[3, 3] * mvp[0, 0] - mvp[3, 0] * mvp[0, 3];
            float kb1 = mvp[3, 1] * mvp[1, 0] - mvp[3, 0] * mvp[1, 1];
            float kb2 = mvp[3, 3] * mvp[1, 0] - mvp[3, 0] * mvp[1, 3];
            float const1 = mvp[3, 1] * mvp[2, 0] - mvp[3, 0] * mvp[2, 1];
            float const2 = mvp[3, 3] * mvp[2, 0] - mvp[3, 0] * mvp[2, 3];

            float a = -(kb2 * const1 - kb1 * const2) / (kb2 * ka1 - kb1 * ka2);
            float b = -(ka1 * a + const1) / kb1;
            float d = -(mvp[0, 0] * a + mvp[1, 0] * b + mvp[2, 0]) / mvp[3, 0];
            // Debug.Log("testabc:" + (ka1 * a + kb1 * b + const1));
            // Debug.Log("testabc:" + (ka2 * a + kb2 * b + const2));
            // Debug.Log("testabcd:" + (a * mvp[0, 0] + b * mvp[1, 0] + mvp[2, 0] + d * mvp[3, 0]));
            // Debug.Log("testabcd:" + (a * mvp[0, 1] + b * mvp[1, 1] + mvp[2, 1] + d * mvp[3, 1]));
            // Debug.Log("testabcd:" + (a * mvp[0, 3] + b * mvp[1, 3] + mvp[2, 3] + d * mvp[3, 3]));

            projectionJitter.SetRow(2, new Vector4(a, b, 1, d + 1));

            // var test = new Vector4(1, 1, 0, 1);
            // Debug.Log("test" + mvp * test + "  " + projectionJitter * mvp * test);
            return projectionJitter;
        }

        void SetViewAndProjectionMatrices(ScriptableRenderContext context, CommandBuffer cmd, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            Matrix4x4 inverseViewMatrix = Matrix4x4.Inverse(viewMatrix);
            Matrix4x4 inverseProjectionMatrix = Matrix4x4.Inverse(projectionMatrix);
            Matrix4x4 inverseViewProjection = inverseViewMatrix * inverseProjectionMatrix;
            Matrix4x4 viewAndProjectionMatrix = projectionMatrix * viewMatrix;

            cmd.SetGlobalMatrix(ShaderPropertyId.viewMatrix, viewMatrix);
            cmd.SetGlobalMatrix(ShaderPropertyId.projectionMatrix, projectionMatrix);
            cmd.SetGlobalMatrix(ShaderPropertyId.viewAndProjectionMatrix, viewAndProjectionMatrix);
            // There's an inconsistency in handedness between unity_matrixV and unity_WorldToCamera
            // Unity changes the handedness of unity_WorldToCamera (see Camera::CalculateMatrixShaderProps)
            // we will also change it here to avoid breaking existing shaders. (case 1257518)
            Matrix4x4 cameraToWorldMatrix = Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f)) * viewMatrix;
            Matrix4x4 worldToCameraMatrix = cameraToWorldMatrix.inverse;
            cmd.SetGlobalMatrix(ShaderPropertyId.worldToCameraMatrix, worldToCameraMatrix);
            cmd.SetGlobalMatrix(ShaderPropertyId.cameraToWorldMatrix, cameraToWorldMatrix);

            cmd.SetGlobalVector(ShaderPropertyId.worldSpaceCameraPos, inverseViewMatrix.GetColumn(3));
            cmd.SetGlobalMatrix(ShaderPropertyId.inverseViewMatrix, inverseViewMatrix);
            cmd.SetGlobalMatrix(ShaderPropertyId.inverseProjectionMatrix, inverseProjectionMatrix);
            cmd.SetGlobalMatrix(ShaderPropertyId.inverseViewAndProjectionMatrix, inverseViewProjection);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            var camera = renderingData.cameraData.camera;
            Transform mainCameraTrans = camera.transform;
            GeometryUtility.CalculateFrustumPlanes(camera, _cullingPlanes);
            Matrix4x4 srcViewMatrix = renderingData.cameraData.GetViewMatrix();
            Matrix4x4 srcProjectionMatrix = renderingData.cameraData.GetGPUProjectionMatrix();

            var allMirrors = GameObject.FindGameObjectsWithTag("VRMirror");
            List<Transform> mirrors = new List<Transform>();
            foreach (var mirror in allMirrors)
            {
                var bounds = mirror.GetComponent<MeshRenderer>().bounds;
                if ((mirror.transform.position - camera.transform.position).magnitude < 50 && GeometryUtility.TestPlanesAABB(_cullingPlanes, bounds))
                {
                    mirrors.Add(mirror.transform);
                }
            }

            // draw stencil & clear depth (unbatched)
            for (var i = 0; i < mirrors.Count; i++)
            {
                cmd.DrawMesh(mirrors[i].GetComponent<MeshFilter>().sharedMesh, mirrors[i].localToWorldMatrix, _materials[i], 0, 0);
            }
            for (var i = 0; i < mirrors.Count; i++)
            {
                cmd.DrawMesh(mirrors[i].GetComponent<MeshFilter>().sharedMesh, mirrors[i].localToWorldMatrix, _materials[i], 0, 1);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            for (var i = 0; i < mirrors.Count; i++)
            {
                var mirrorTrans = mirrors[i].transform;
                var mirrorCamera = mirrors[i].transform.GetChild(0).GetComponent<Camera>();
                Vector3 normalDir = mirrorTrans.rotation * Vector3.back;
                // check distance
                float distance = Vector3.Dot(normalDir, mainCameraTrans.position - mirrorTrans.position);

                var position = mainCameraTrans.position - 2 * distance * normalDir;

                Vector3 forwardDir = mainCameraTrans.rotation * Vector3.forward;
                forwardDir = forwardDir - 2 * Vector3.Dot(normalDir, forwardDir) * normalDir;

                Vector3 upDir = mainCameraTrans.rotation * Vector3.up;
                upDir = upDir - 2 * Vector3.Dot(normalDir, upDir) * normalDir;

                var rotation = Quaternion.LookRotation(forwardDir, upDir);
                mirrorCamera.transform.position = position;
                mirrorCamera.transform.rotation = rotation;

                // Matrix4x4 inverseViewMatrix = Matrix4x4.TRS(position, rotation, new Vector3(-1, 1, -1));
                // Matrix4x4 viewMatrix = inverseViewMatrix.inverse;
                Matrix4x4 viewMatrix = Matrix4x4.Scale(new Vector3(-1,1,1)) * mirrorCamera.worldToCameraMatrix;
                Matrix4x4 projectionMatrix = srcProjectionMatrix;

                // 生成新的projectionMatrix, 使mirror quad成为近平面
                projectionMatrix = MoveNearToPlane(projectionMatrix * viewMatrix * mirrorTrans.localToWorldMatrix) * projectionMatrix;
                // SetViewAndProjectionMatrices(context, cmd, viewMatrix, projectionMatrix);
                RenderingUtils.SetViewAndProjectionMatrices(cmd, viewMatrix, projectionMatrix, true);
                cmd.SetGlobalVector(ShaderPropertyId.worldSpaceCameraPos, position);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                mirrorCamera.TryGetCullingParameters(out var cullingParameters);
                cullingParameters.cullingMatrix = srcProjectionMatrix * viewMatrix;
                var planes = GeometryUtility.CalculateFrustumPlanes(cullingParameters.cullingMatrix);
                for (var i_plane = 0; i_plane < 6; i_plane++)
                {
                    cullingParameters.SetCullingPlane(i_plane, planes[i_plane]);
                    // Debug.Log(i_plane + " " + planes[i_plane]);
                }
                Vector3 viewDir = rotation * Vector3.forward;

                cullingParameters.SetCullingPlane(4, new Plane(viewDir, position + camera.nearClipPlane * viewDir));
                cullingParameters.SetCullingPlane(5, new Plane(-viewDir, position + mirrorCamera.farClipPlane * viewDir));
                // for (var i_plane = 0; i_plane < 6; i_plane++)
                // {
                //     Debug.Log("new" + cullingParameters.GetCullingPlane(i_plane));
                // }
                var cullResults = context.Cull(ref cullingParameters);

                _RenderStateBlock.stencilReference = 1 << i;
                // opaque
                cmd.SetGlobalVector(s_DrawObjectPassDataPropID, new Vector4(0, 0, 0, 1));
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                DrawingSettings drawingSettings = CreateDrawingSettings(_tags, ref renderingData, SortingCriteria.CommonOpaque);
                context.DrawRenderers(cullResults, ref drawingSettings, ref _OpaqueFilteringSettings, ref _RenderStateBlock);

                // skybox!!! use a mesh skybox
                // context.DrawSkybox(camera);

                // transparent
                cmd.SetGlobalVector(s_DrawObjectPassDataPropID, new Vector4(0, 0, 0, 0));
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                drawingSettings = CreateDrawingSettings(_tags, ref renderingData, SortingCriteria.CommonTransparent);
                context.DrawRenderers(cullResults, ref drawingSettings, ref _TransparentFilteringSettings, ref _RenderStateBlock);
            }

            SetViewAndProjectionMatrices(context, cmd, srcViewMatrix, srcProjectionMatrix);
            for (var i = 0; i < mirrors.Count; i++)
            {
                cmd.DrawMesh(mirrors[i].GetComponent<MeshFilter>().sharedMesh, mirrors[i].localToWorldMatrix, _materials[i], 0, 2);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
    }

    VRMirrorRenderPass _ScriptablePass;
    public Shader VRMirrorShader = null;

    public override void Create()
    {
        _ScriptablePass = new VRMirrorRenderPass();
        _ScriptablePass.VRMirrorShader = VRMirrorShader;
        _ScriptablePass.Setup();
        // Configures where the render pass should be injected.
        _ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_ScriptablePass);
    }
}


