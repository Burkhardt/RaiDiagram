using OsLib;

namespace RaiDiagram.Tests;

public class PlantUmlCompilerTests
{
	[Fact]
	public void Compile_PreservesRoleFirstSemanticsAndFrames()
	{
		var model = TestDiagrams.CreateUseCaseModel();
		model.Manifest.Presentation.Handwritten = true;
		model.Manifest.Presentation.FontName = "Aptos";
		model.Manifest.Presentation.Frames.Add(new DiagramPresentationFrame
		{
			Id = "roles",
			Title = "Participants",
			ElementIds = ["band-manager"]
		});
		var compiler = new PlantUmlDiagramCompiler();

		var result = compiler.Compile(model.Manifest);

		Assert.True(result.Capabilities.CanRender);
		Assert.Contains("actor \"Band Manager\"", result.Source);
		Assert.Contains("usecase \"Schedule rehearsal\"", result.Source);
		Assert.Contains("package \"Scheduling system\"", result.Source);
		Assert.Contains("rectangle \"Participants\"", result.Source);
		Assert.Contains("skinparam handwritten true", result.Source);
		Assert.Contains("skinparam defaultFontName \"Aptos\"", result.Source);
		Assert.Contains(" : schedules", result.Source);
		Assert.Equal(DiagramElementKinds.Role,
			model.Manifest.Projection.Elements.Single(item => item.DisplayName == "Band Manager").Kind);
	}

	[Fact]
	public void Compile_UsesOnlyExplicitApprovedLocalThemeRoot()
	{
		var root = Os.TempDir / "RAIkeep" / "raidiagram-tests" / nameof(Compile_UsesOnlyExplicitApprovedLocalThemeRoot);
		Cleanup(root);
		try
		{
			root.mkdir();
			_ = new TextFile(root, "puml-theme-tenant", "puml", "' local tenant theme");
			var manifest = TestDiagrams.CreateUseCaseModel().Manifest;
			manifest.Presentation.Theme = "tenant";

			var result = new PlantUmlDiagramCompiler().Compile(
				manifest,
				new PlantUmlCompileOptions { LocalThemeRoot = root });

			Assert.Contains($"!theme tenant from {root.FullPath}", result.Source);
			Assert.DoesNotContain("http://", result.Source, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain("https://", result.Source, StringComparison.OrdinalIgnoreCase);
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public void Compile_RejectsUnsupportedConstructInsteadOfDroppingIt()
	{
		var manifest = TestDiagrams.CreateUseCaseModel().Manifest;
		manifest.Projection.Elements.Add(new DiagramElement
		{
			Id = "quantum",
			Kind = "QuantumEntanglement",
			DisplayName = "Entanglement"
		});

		var error = Assert.Throws<UnsupportedDiagramConstructException>(
			() => new PlantUmlDiagramCompiler().Compile(manifest));

		Assert.Equal("QuantumEntanglement", error.ConstructKind);
	}

	[Fact]
	public void Compile_RejectsRemoteOrInjectedThemeNames()
	{
		var manifest = TestDiagrams.CreateUseCaseModel().Manifest;
		manifest.Presentation.Theme = "https://example.test/theme";

		Assert.Throws<RaidSchemaException>(() => new PlantUmlDiagramCompiler().Compile(manifest));
	}

	private static void Cleanup(RaiPath root)
	{
		if (root.Exists())
			root.rmdir(depth: 6, deleteFiles: true);
	}
}
