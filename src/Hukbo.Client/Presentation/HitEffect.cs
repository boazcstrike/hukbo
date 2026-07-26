namespace Hukbo.Client.Presentation;

internal readonly record struct HitEffect(
    long Sequence,
    ulong TargetEntityId,
    int XRaw,
    int YRaw,
    int Damage,
    bool IsLethal,
    float AgeSeconds)
{
    public float LifetimeSeconds => IsLethal ? 0.28f : 0.18f;
}
