namespace ME.UITK.Particles {

    [System.Flags]
    public enum ParticleRenderMode {
        None = 0,
        Mesh = 1 << 0,
        RenderTexture = 1 << 1,
        GPU = 1 << 2,
        RenderTextureAtlas = 1 << 3,
        VFXGraph = 1 << 4,
        Auto = GPU | Mesh | RenderTexture | RenderTextureAtlas,
    }

}
