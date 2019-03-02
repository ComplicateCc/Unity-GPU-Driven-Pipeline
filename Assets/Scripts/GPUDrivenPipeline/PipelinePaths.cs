using System;
namespace MPipeline
{
    [Serializable]
    public class GPUDeferred : EventsCollection
    {
        public PropertySetEvent propertySet;
        public GeometryEvent geometry;
        public AOEvents ambientOcclusion;
        public LightingEvent lighting;
        public ReflectionEvent reflection;
        public SkyboxEvent skybox;
        public VolumetricLightEvent volumetric;
        public TemporalAAEvent temporalAA;
        public TransEvent transparent;
        public FinalPostEvent postEffects;
    }

    [Serializable]
    public class BakeInEditor : EventsCollection
    {
        public PropertySetEvent propertySet;
        public GeometryEvent geometry;
        public LightingEvent lighting;
        public SkyboxEvent skybox;
    }

    [Serializable]
    public struct AllEvents
    {
        [TargetPath(PipelineResources.CameraRenderingPath.GPUDeferred)]
        public GPUDeferred gpuDeferred;
        [TargetPath(PipelineResources.CameraRenderingPath.Bake)]
        public BakeInEditor baker;
    }
}
