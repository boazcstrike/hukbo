using Hukbo.Diagnostics;

namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// Emits the three missing-visual diagnostics (visual-system-integration-
/// design.md section 4, R-W6.5) through <see cref="DiagnosticLog"/> on the
/// <c>assets</c> channel at <see cref="LogLevel.Warning"/>, deduplicated by a
/// fixed-capacity seen-set so a resolver stuck on the same missing identifier
/// logs once per session instead of once per frame.
/// </summary>
/// <remarks>
/// <para>
/// One instance is owned by whichever Client component holds a
/// <see cref="DiagnosticLog"/> reference and calls
/// <see cref="VisualFallbackResolver.Resolve{T}"/> — the resolver and the
/// catalogs it walks stay pure and never see a log. The seen-set check runs
/// before any payload work, so the disabled and already-seen paths allocate
/// nothing: the key is a value-type <see cref="SeenKey"/>, never a
/// concatenated string, and <see cref="HashSet{T}"/> over a struct key that
/// implements <see cref="IEquatable{T}"/> does not box.
/// </para>
/// <para>
/// Client-side presentation state only (visual-system-integration-design.md
/// section 4): never read by the simulation, never snapshotted, never fed
/// back into a mix stream.
/// </para>
/// </remarks>
internal sealed class VisualDiagnostics
{
    /// <summary>
    /// The seen-set's fixed capacity (R-X.16: no unbounded caches). "On the
    /// order of 64" per visual-system-integration-design.md section 4; once
    /// full, further distinct identifiers stop logging rather than growing
    /// the set.
    /// </summary>
    internal const int SeenSetCapacity = 64;

    private readonly HashSet<SeenKey> _seen = new();

    /// <summary>
    /// Emits <see cref="LogEvents.AssetsVisualVariantMissing"/> once per
    /// distinct <paramref name="catalogId"/>/<paramref name="requestedId"/>
    /// pair: the fallback chain resolved past step 1
    /// (<see cref="VisualFallbackStep.SpecificVariant"/>).
    /// </summary>
    /// <param name="log">The log to write through.</param>
    /// <param name="catalogId">Which catalog the lookup was against.</param>
    /// <param name="requestedId">The identifier or index that was requested.</param>
    /// <param name="resolvedStep">The chain step the resolution actually reached.</param>
    public void ReportVariantMissing(
        DiagnosticLog log,
        string catalogId,
        string requestedId,
        VisualFallbackStep resolvedStep)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedId);

        if (!ShouldEmit(log, LogEvents.AssetsVisualVariantMissing, catalogId, requestedId))
        {
            return;
        }

        log.Write(
            LogLevel.Warning,
            LogChannel.Assets,
            LogEvents.AssetsVisualVariantMissing,
            "catalogId",
            catalogId,
            "requestedId",
            requestedId,
            "resolvedStep",
            (int)resolvedStep);
    }

    /// <summary>
    /// Emits <see cref="LogEvents.AssetsVisualFallback"/> once per distinct
    /// <paramref name="catalogId"/>/<paramref name="requestedId"/> pair: the
    /// fallback chain reached <see cref="VisualFallbackStep.DiagnosticPlaceholder"/>.
    /// </summary>
    /// <param name="log">The log to write through.</param>
    /// <param name="catalogId">Which catalog the lookup was against.</param>
    /// <param name="requestedId">The identifier or index that was requested.</param>
    public void ReportFallback(
        DiagnosticLog log,
        string catalogId,
        string requestedId)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedId);

        if (!ShouldEmit(log, LogEvents.AssetsVisualFallback, catalogId, requestedId))
        {
            return;
        }

        log.Write(
            LogLevel.Warning,
            LogChannel.Assets,
            LogEvents.AssetsVisualFallback,
            "catalogId",
            catalogId,
            "requestedId",
            requestedId);
    }

    /// <summary>
    /// Emits <see cref="LogEvents.AssetsVisualCatalogInvalid"/> once per
    /// distinct <paramref name="catalogId"/>/<paramref name="reason"/> pair: a
    /// startup validation failure. <paramref name="reason"/> is a stable
    /// reason code, never prose; free prose belongs in
    /// <paramref name="message"/> only.
    /// </summary>
    /// <param name="log">The log to write through.</param>
    /// <param name="catalogId">Which catalog failed validation.</param>
    /// <param name="reason">A stable reason code, not a sentence.</param>
    /// <param name="message">Optional free-text detail for a human reader.</param>
    public void ReportCatalogInvalid(
        DiagnosticLog log,
        string catalogId,
        string reason,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (!ShouldEmit(log, LogEvents.AssetsVisualCatalogInvalid, catalogId, reason))
        {
            return;
        }

        if (message is null)
        {
            log.Write(
                LogLevel.Warning,
                LogChannel.Assets,
                LogEvents.AssetsVisualCatalogInvalid,
                "catalogId",
                catalogId,
                "reason",
                reason);
        }
        else
        {
            log.Write(
                LogLevel.Warning,
                LogChannel.Assets,
                LogEvents.AssetsVisualCatalogInvalid,
                "catalogId",
                catalogId,
                "reason",
                reason,
                "msg",
                message);
        }
    }

    /// <summary>
    /// True when the caller should build a payload and write: the log is
    /// enabled for <c>warn</c>/<c>assets</c>, and the
    /// (<paramref name="eventId"/>, <paramref name="catalogId"/>,
    /// <paramref name="discriminator"/>) triple has not already been recorded
    /// and there is still room to record it. Checked first, and with a
    /// non-allocating struct key, so the disabled and already-seen paths do
    /// no further work.
    /// </summary>
    private bool ShouldEmit(
        DiagnosticLog log,
        string eventId,
        string catalogId,
        string discriminator)
    {
        if (!log.IsEnabledFor(LogLevel.Warning, LogChannel.Assets))
        {
            return false;
        }

        var key = new SeenKey(eventId, catalogId, discriminator);
        if (_seen.Contains(key))
        {
            return false;
        }

        if (_seen.Count >= SeenSetCapacity)
        {
            return false;
        }

        _seen.Add(key);
        return true;
    }

    /// <summary>
    /// The seen-set's entry: which event, which catalog, which distinguishing
    /// identifier (the requested variant ID for the two resolver events, the
    /// reason code for the catalog-invalid event). A value type so recording
    /// and checking membership never allocates.
    /// </summary>
    private readonly record struct SeenKey(
        string EventId,
        string CatalogId,
        string Discriminator);
}
