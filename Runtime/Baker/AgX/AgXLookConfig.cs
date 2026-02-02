namespace CustomToneMapping.Baker.AgX
{
    public struct AgXLookConfig
    {
        public AgXLookPreset LookPreset;
        public float Intensity;

        public static AgXLookConfig GetPreset(AgXLookPreset preset)
        {
            if (preset == AgXLookPreset.None)
            {
                return new AgXLookConfig
                {
                    LookPreset = AgXLookPreset.None,
                    Intensity = 0.0f
                };
            }

            return new AgXLookConfig
            {
                LookPreset = preset,
                Intensity = 1.0f
            };
        }
    }
}