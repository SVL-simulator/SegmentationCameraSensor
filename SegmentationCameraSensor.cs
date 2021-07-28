/**
 * Copyright (c) 2019-2021 LG Electronics, Inc.
 *
 * This software contains code licensed as described in LICENSE.
 *
 */

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Simulator.Bridge.Data;
using Simulator.Utilities;
using System.Collections.Generic;

namespace Simulator.Sensors
{
    using UnityEngine.Experimental.Rendering;

    [SensorType("Segmentation Camera", new[] {typeof(ImageData)})]
    [RequireComponent(typeof(Camera))]
    public class SegmentationCameraSensor : CameraSensorBase
    {
        private static class Properties
        {
            public static readonly int Output = Shader.PropertyToID("_OutputXR");
            public static readonly int TexSize = Shader.PropertyToID("_TexSize");
        }
        
        public enum InstanceCandidateTags
        {
            Car,
            Road,
            Sidewalk,
            Vegetation,
            Obstacle,
            TrafficLight,
            Building,
            Sign,
            Shoulder,
            Pedestrian,
            Curb
        }

        // "InstanceSegmentationTags" indicates which tags should have instance segmention.
        // This setting should be a global one for whole simulator. But we need WebUI to allow users
        // able to set this global setting (not via Unity editor).
        // Before we get WebUI support for that, we temporarily put this setting as a local property
        // of SegmentationCameraSensor here, and use it to reset SegmentationColors in SimulatorManager.

        // TODO: Move this setting to SimulatorManager and use WebUI to set it.
        public List<InstanceCandidateTags> InstanceSegmentationTags = new List<InstanceCandidateTags>();

        private ShaderTagId passId;

        private ComputeShader computeUtils;

        protected override void Initialize()
        {
            base.Initialize();
            // SegmentationCameraSensor always use JpegQuality = 100
            JpegQuality = 100;
            SensorCamera.GetComponent<HDAdditionalCameraData>().customRender += CustomRender;
            passId = new ShaderTagId("SimulatorSegmentationPass");
            computeUtils = Instantiate(RuntimeSettings.Instance.ComputeUtils);

            if (InstanceSegmentationTags.Count > 0)
            {
                // Check if instance segmentation has been set (either by Editor or by another SegmentationCamera).
                if (SimulatorManager.Instance.CheckInstanceSegmentationSetting())
                {
                    // TODO: Change both semantic segmentation and instance segmentation from global to per camera.
                    // so that this error can be removed.
                    Debug.LogWarning("Instance segmentation has been set for some tags. Please only load SegmentationCamera once!");
                }

                foreach (InstanceCandidateTags tag in InstanceSegmentationTags)
                {
                    SimulatorManager.Instance.SetInstanceColor(tag.ToString());
                }
                SimulatorManager.Instance.ResetSegmentationColors();
            }
        }

        void CustomRender(ScriptableRenderContext context, HDCamera hd)
        {
            var cmd = CommandBufferPool.Get();
            SensorPassRenderer.Render(context, cmd, hd, renderTarget, passId, SimulatorManager.Instance.SkySegmentationColor);
            var kernel = computeUtils.FindKernel("FillAlphaXR");
            cmd.SetComputeTextureParam(computeUtils, kernel, Properties.Output, renderTarget.ColorHandle);
            cmd.SetComputeVectorParam(computeUtils, Properties.TexSize, new Vector4(Width, Height, 1f / Width, 1f / Height));
            cmd.DispatchCompute(computeUtils, kernel, HDRPUtilities.GetGroupSize(Width, 8), HDRPUtilities.GetGroupSize(Height, 8), 1);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }
}
