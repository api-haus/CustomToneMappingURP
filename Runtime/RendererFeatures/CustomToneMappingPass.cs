using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace CustomToneMapping.URP.RendererFeatures
{
    public class CustomToneMappingPass : ScriptableRenderPass
    {
        private readonly Material _material;
        private static readonly int HDROutputLuminanceParams = Shader.PropertyToID("_HDROutputLuminanceParams");

        public CustomToneMappingPass(Shader shader)
        {
            if (shader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(shader);
            }
        }

        private void ConfigureHDROutputInternal(ColorGamut hdrDisplayColorGamut,
            HDROutputUtils.HDRDisplayInformation hdrDisplayInformation)
        {
            HDROutputUtils.ConfigureHDROutput(_material, hdrDisplayColorGamut,
                HDROutputUtils.Operation.ColorConversion);
            var hdrParams = new Vector4(
                hdrDisplayInformation.minToneMapLuminance,
                hdrDisplayInformation.maxToneMapLuminance,
                hdrDisplayInformation.paperWhiteNits,
                1.0f / hdrDisplayInformation.paperWhiteNits
            );
            _material.SetVector(HDROutputLuminanceParams, hdrParams);
        }

        private class CopyPassData
        {
            public TextureHandle SourceLut;
        }

        private class ToneMapPassData
        {
            public Material Material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();

            if (!resourceData.internalColorLut.IsValid()) return;

            // Prevent double tonemapping
            if (VolumeManager.instance.stack.GetComponent<Tonemapping>().mode != TonemappingMode.None)
            {
                return;
            }

            var cameraData = frameData.Get<UniversalCameraData>();

            // Bind tone map LUT
            if (!UrpBridge.PrepareMaterial(_material,
                    cameraData.isHDROutputActive ? cameraData.hdrDisplayInformation : null))
            {
                return;
            }

            ConfigureHDROutput(cameraData);

            var lutDesc = renderGraph.GetTextureDesc(resourceData.internalColorLut);
            lutDesc.name = "CustomToneMapTemp";
            lutDesc.clearBuffer = false;
            var tempLut = renderGraph.CreateTexture(lutDesc);

            using (var builder =
                   renderGraph.AddRasterRenderPass<ToneMapPassData>("Apply Tone Map", out var passData))
            {
                passData.Material = _material;

                builder.SetInputAttachment(resourceData.internalColorLut, 0);
                builder.SetRenderAttachment(tempLut, 0);

                builder.SetRenderFunc((ToneMapPassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3, 1);
                });
            }

            renderGraph.AddCopyPass(tempLut, resourceData.internalColorLut, passName: "Copy Back LUT");
        }

        private void ConfigureHDROutput(UniversalCameraData cameraData)
        {
            if (cameraData.isHDROutputActive)
            {
                ConfigureHDROutputInternal(cameraData.hdrDisplayColorGamut, cameraData.hdrDisplayInformation);
            }
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }
    }
}
