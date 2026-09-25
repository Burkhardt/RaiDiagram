using System.Xml.Linq;
using OsLib;

namespace RaiDiagram.Tests;

public sealed class DiagramArtifactManagerTests : IDisposable
{
	private readonly RaiPath root = Os.TempDir / "RAIkeep" / "raidiagram-tests" /
		nameof(DiagramArtifactManagerTests);

	public DiagramArtifactManagerTests()
	{
		Cleanup();
		root.mkdir();
	}

	public void Dispose() => Cleanup();

	[Fact]
	public void CompilerOutput_RoundTripsThroughPlantUmlImporter_WithEquivalentSemanticAst()
	{
		var original = TestDiagrams.CreateUseCaseModel().Manifest;
		var source = new PlantUmlDiagramCompiler().Compile(original).Source;

		var imported = new PlantUmlModelImporter().Import(new StringReader(source)).Manifest;

		Assert.Equal(
			DiagramSemanticHasher.Compute(original),
			DiagramSemanticHasher.Compute(imported));
		Assert.Equal(original.Projection.Elements.Count, imported.Projection.Elements.Count);
		Assert.Equal(original.Projection.Relationships.Count, imported.Projection.Relationships.Count);
	}

	[Fact]
	public void HydratableSvg_EmitsDeclaredExpression_ButNeverDynamicEvaluationState()
	{
		var manifest = TestDiagrams.CreateUseCaseModel().Manifest;
		manifest.Projection.Relationships[0].Guard = "Availability = Confirmed";
		DeterministicDiagramCanvas.Ensure(manifest);

		var svg = AimSvg.Emit(manifest, AimSvgProfile.Hydratable);

		Assert.Contains("aim-expression=\"Availability = Confirmed\"", svg);
		Assert.Contains("aim-source-port=\"port-bottom\"", svg);
		Assert.Contains("aim-target-port=\"port-top\"", svg);
		Assert.Contains("aim-rol=\"true\"", svg);
		Assert.Contains("aim-uc=\"true\"", svg);
		Assert.DoesNotContain("aim-satisfied", svg, StringComparison.Ordinal);
		AimSvg.Validate(svg, AimSvgProfile.Hydratable);
	}

	[Fact]
	public void PlainSvg_PreservesVisibleDiagram_WithoutAimHydrationMetadata()
	{
		var manifest = TestDiagrams.CreateUseCaseModel().Manifest;
		DeterministicDiagramCanvas.Ensure(manifest);
		var svg = AimSvg.Emit(manifest, AimSvgProfile.Plain);
		var document = XDocument.Parse(svg);

		Assert.DoesNotContain("aim-", svg, StringComparison.Ordinal);
		Assert.NotEmpty(document.Descendants(XName.Get("rect", "http://www.w3.org/2000/svg")));
		AimSvg.Validate(svg, AimSvgProfile.Plain);
	}

	[Fact]
	public void Refresh_WritesMissingDerivatives_ThenPerformsAProvenNoOp()
	{
		var artifacts = Artifacts();
		artifacts.RaidManifest.SaveManifest(TestDiagrams.CreateUseCaseModel().Manifest);
		var authoritativeBefore = artifacts.RaidManifest.ReadAllText();
		var manager = new DiagramArtifactManager();

		var first = manager.Refresh(artifacts);
		var pumlWrite = artifacts.PlantUmlSource.LastWriteTimeUtc;
		var svgWrite = artifacts.Svg.LastWriteTimeUtc;
		var second = manager.Refresh(artifacts);

		Assert.True(first.PlantUmlWritten);
		Assert.True(first.SvgWritten);
		Assert.False(second.Changed);
		Assert.Equal(pumlWrite, artifacts.PlantUmlSource.LastWriteTimeUtc);
		Assert.Equal(svgWrite, artifacts.Svg.LastWriteTimeUtc);
		Assert.Equal(authoritativeBefore, artifacts.RaidManifest.ReadAllText());
	}

	[Fact]
	public void Refresh_ReplacesStaleDerivativesFromChangedAuthoritativeManifest()
	{
		var artifacts = Artifacts();
		var manifest = TestDiagrams.CreateUseCaseModel().Manifest;
		artifacts.RaidManifest.SaveManifest(manifest);
		var manager = new DiagramArtifactManager();
		manager.Refresh(artifacts);
		var oldSvg = new TextFile(artifacts.Svg.FullName).ReadAllText();

		manifest.Projection.Elements[0].DisplayName = "Changed role";
		artifacts.RaidManifest.SaveManifest(manifest);
		var refreshed = manager.Refresh(artifacts);
		var newSvg = new TextFile(artifacts.Svg.FullName).ReadAllText();

		Assert.True(refreshed.PlantUmlWritten);
		Assert.True(refreshed.SvgWritten);
		Assert.NotEqual(oldSvg, newSvg);
		Assert.Contains("Changed role", newSvg);
	}

	[Fact]
	public void Export_DerivesCurrentOutput_InsteadOfCopyingStaleSiblings()
	{
		var artifacts = Artifacts();
		var manifest = TestDiagrams.CreateUseCaseModel().Manifest;
		artifacts.RaidManifest.SaveManifest(manifest);
		new TextFile(artifacts.Svg.FullName).DeleteAll().Append("stale").Save();
		var destination = root / "export";

		var files = new DiagramArtifactManager().Export(
			artifacts,
			destination,
			DiagramArtifactFormat.Svg,
			AimSvgProfile.Hydratable);

		var exported = Assert.Single(files);
		var svg = new TextFile(exported.FullName).ReadAllText();
		Assert.NotEqual("stale", svg);
		Assert.Contains("aim-node=\"true\"", svg);
		Assert.Equal("stale\n", new TextFile(artifacts.Svg.FullName).ReadAllText());
	}

	private DiagramArtifactSet Artifacts()
		=> new(root / "Image", "AfricaStage", "ScheduleRehearsal");

	private void Cleanup()
	{
		if (root.Exists())
			root.rmdir(depth: 8, deleteFiles: true);
	}
}
