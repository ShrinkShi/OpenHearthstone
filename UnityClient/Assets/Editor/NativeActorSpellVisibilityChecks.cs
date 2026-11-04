public static class NativeActorSpellVisibilityChecks
{
    public static void Run()
    {
        NativeEffectChecks.Run();
        NativeEntryGraphChecks.RunGpu();
    }
}
