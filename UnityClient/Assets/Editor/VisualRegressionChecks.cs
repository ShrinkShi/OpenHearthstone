public static class VisualRegressionChecks
{
    public static void Run()
    {
        InteractionRenderChecks.Run();
        WindowRenderChecks.Run();
        AssociatedRenderChecks.Run();
        LifecycleRenderChecks.Run();
        NativeEffectRenderChecks.Run();
        NativeShaderRenderChecks.Run();
        NativeCaptureOrderChecks.Run();
        NativeGlowRenderChecks.Run();
    }
}
