using System.Runtime.CompilerServices;
using Hukbo.Diagnostics;
using Microsoft.Xna.Framework.Graphics;
using Sandata.Client.Theming;

namespace Sandata.Client.Tests;

/// <summary>
/// Covers plan task 33's font pipeline: <see cref="SandataFontRamp"/>'s role
/// contract, the on-disk content pipeline it must stay in sync with, and
/// <see cref="SandataFontSet"/>'s pure loader. No test here constructs a
/// <c>GraphicsDevice</c>, a <c>SpriteBatch</c>, or a window
/// (<c>CLAUDE.md</c> section 5); the round-trip test below builds its
/// <see cref="SpriteFont"/> stand-ins with
/// <see cref="RuntimeHelpers.GetUninitializedObject"/>, which allocates an
/// instance without running any constructor and therefore never touches a
/// device.
/// </summary>
public sealed class SandataFontRampTests
{
    [Fact]
    public void EveryRoleResolvesToADistinctAssetId()
    {
        var assetIds = SandataFontRamp.AllRoles
            .Select(SandataFontRamp.GetAssetId)
            .ToArray();

        Assert.Equal(assetIds.Length, assetIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryAssetIdHasAMatchingSpritefontFileOnDisk()
    {
        var fontsDirectory = Path.Combine(
            GetRepositoryRoot(), "src", "Sandata.Client", "Content");

        foreach (var role in SandataFontRamp.AllRoles)
        {
            var assetId = SandataFontRamp.GetAssetId(role);
            var descriptorPath = Path.Combine(
                fontsDirectory,
                assetId.Replace('/', Path.DirectorySeparatorChar) + ".spritefont");

            Assert.True(
                File.Exists(descriptorPath),
                $"Role {role} declares asset id '{assetId}' but no descriptor " +
                $"exists at {descriptorPath}.");
        }
    }

    [Fact]
    public void EveryAssetIdAppearsInContentMgcb()
    {
        var mgcbPath = Path.Combine(
            GetRepositoryRoot(),
            "src", "Sandata.Client", "Content", "Content.mgcb");
        var mgcbText = File.ReadAllText(mgcbPath);

        foreach (var role in SandataFontRamp.AllRoles)
        {
            var assetId = SandataFontRamp.GetAssetId(role);

            Assert.Contains(
                $"#begin {assetId}.spritefont",
                mgcbText,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PixelSizesRiseMonotonicallyAcrossRoles()
    {
        var sizes = SandataFontRamp.AllRoles
            .Select(SandataFontRamp.GetPixelSize)
            .ToArray();

        for (var index = 1; index < sizes.Length; index++)
        {
            Assert.True(
                sizes[index] > sizes[index - 1],
                $"{SandataFontRamp.AllRoles[index]} ({sizes[index]}px) must " +
                $"be strictly larger than {SandataFontRamp.AllRoles[index - 1]} " +
                $"({sizes[index - 1]}px).");
        }
    }

    [Fact]
    public void LoadWithStubFuncRoundTripsEveryRole()
    {
        var stubsByAssetId = SandataFontRamp.AllRoles.ToDictionary(
            SandataFontRamp.GetAssetId,
            _ => CreateUninitializedSpriteFont(),
            StringComparer.Ordinal);

        var set = SandataFontSet.Load(
            assetId => stubsByAssetId[assetId]);

        foreach (var role in SandataFontRamp.AllRoles)
        {
            Assert.Same(
                stubsByAssetId[SandataFontRamp.GetAssetId(role)],
                set.Get(role));
        }
    }

    /// <summary>
    /// A distinct, never-null <see cref="SpriteFont"/> reference that never
    /// touches a <c>GraphicsDevice</c>: <see cref="RuntimeHelpers"/> allocates
    /// the instance directly, skipping every constructor, which is all a
    /// reference-identity round-trip test needs.
    /// </summary>
    private static SpriteFont CreateUninitializedSpriteFont() =>
        (SpriteFont)RuntimeHelpers.GetUninitializedObject(typeof(SpriteFont));

    private static string GetRepositoryRoot()
    {
        var root = LogPaths.FindRepositoryRoot(AppContext.BaseDirectory);
        Assert.True(
            root is not null,
            "No ancestor of " + AppContext.BaseDirectory + " contains " +
            LogPaths.RepositoryMarkerFileName +
            ", so the source tree cannot be scanned.");
        return root!;
    }
}
