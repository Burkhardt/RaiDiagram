using OsLib;
using RaiImage;

namespace RaiDiagram.Tests;

public sealed class PlantUmlRendererTests : IDisposable
{
	private const string ExampleManifestUri =
		"https://github.com/Burkhardt/RaiDiagram/blob/main/examples/ScheduleRehearsal/ScheduleRehearsal.raid";
	private readonly RaiPath? originalPath = PlantUml.PlantUmlPath;
	private readonly string originalCommand = PlantUml.CommandName;
	private readonly string originalJavaCommand = PlantUml.JavaCommand;

	public void Dispose()
	{
		PlantUml.PlantUmlPath = originalPath;
		PlantUml.CommandName = originalCommand;
		PlantUml.JavaCommand = originalJavaCommand;
	}

	[Fact]
	public void CheckedInExample_HasSynchronizedRaidPlantUmlAndSvgProvenance()
	{
		var example = ExampleRoot();
		var model = new RaidFile(example, "ScheduleRehearsal").LoadModel();
		var compiled = new PlantUmlDiagramCompiler().Compile(model.Manifest);
		var configuration = new DiagramStyleProvider()
			.Resolve(model.Manifest, new PlantUmlCompileOptions());
		var checkedInPlantUml = new TextFile(example, "ScheduleRehearsal", "puml").ReadAllText();
		var checkedInConfig = new PumlConfigFile(example, "ScheduleRehearsal").ReadAllText();
		var checkedInSvg = new TextFile(example, "ScheduleRehearsal", "svg").ReadAllText();

		Assert.Equal(NormalizeLines(checkedInPlantUml), NormalizeLines(compiled.Source));
		Assert.Equal(NormalizeLines(checkedInConfig), NormalizeLines(configuration.ToPlantUml()));
		Assert.DoesNotContain("!theme", checkedInPlantUml);
		Assert.DoesNotContain("<style>", checkedInPlantUml);
		Assert.Contains("PlantUML", checkedInSvg, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("Please use '!option handwritten true'", checkedInSvg, StringComparison.Ordinal);
		Assert.Contains("font-family=\"Chalkduster, Comic Sans MS\"", checkedInSvg, StringComparison.Ordinal);
		var provenance = SvgProvenanceMetadata.Read(checkedInSvg);
		AssertProvenance(
			model,
			provenance,
			configuration.ContentHash,
			configuration.ComputeRenderHash(compiled.Source));
		Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", provenance.StyleHash);
		Assert.Empty(provenance.StyleLayers);
		foreach (var element in model.Manifest.Projection.Elements.Where(item => item.Source is not null))
			Assert.Equal(element.SourceSemanticHash, TestDiagrams.Snapshot(element).GetSemanticHash());
	}

	[Fact]
	public async Task RenderAsync_UsesRealPlantUmlCliAndVerifiesSvgProvenance()
	{
		ConfigureRealPlantUml();
		var version = new PlantUmlCommand().Run("-version");
		Assert.Equal(0, version.ExitCode);
		Assert.Contains("PlantUML version", version.Output, StringComparison.OrdinalIgnoreCase);

		var example = ExampleRoot();
		var model = new RaidFile(example, "ScheduleRehearsal").LoadModel();
		var checkedInPlantUml = new TextFile(example, "ScheduleRehearsal", "puml").ReadAllText();
		var checkedInConfig = new PumlConfigFile(example, "ScheduleRehearsal").ReadAllText();
		var root = Os.TempDir / "RAIkeep" / "raidiagram-tests" /
			nameof(RenderAsync_UsesRealPlantUmlCliAndVerifiesSvgProvenance);
		Cleanup(root);
		try
		{
			var result = await new PlantUmlDiagramRenderer().RenderAsync(
				model,
				new DiagramDestination
				{
					ImageTreeRoot = root / "images",
					Subscriber = "ExampleConsumer",
					ItemId = "ScheduleRehearsal"
				},
				new DiagramRenderOptions { ManifestUri = ExampleManifestUri },
				TestContext.Current.CancellationToken);

			Assert.True(result.PlantUmlSource.Exists());
			Assert.True(result.RaidManifest.Exists());
			Assert.True(result.PlantUmlConfig.Exists());
			Assert.True(result.Svg.Exists());
			Assert.IsType<RaidFile>(result.Artifacts.RaidManifest);
			Assert.IsType<PumlSourceFile>(result.Artifacts.PlantUmlSource);
			Assert.IsType<PumlConfigFile>(result.Artifacts.PlantUmlConfig);
			Assert.Equal("config", result.Artifacts.PlantUmlConfig.NameExt);
			Assert.EndsWith("ScheduleRehearsal_config.puml", result.PlantUmlConfig.FullName);
			Assert.Equal(NormalizeLines(checkedInPlantUml),
				NormalizeLines(new TextFile(result.PlantUmlSource.FullName).ReadAllText()));
			Assert.Equal(NormalizeLines(checkedInConfig),
				NormalizeLines(new TextFile(result.PlantUmlConfig.FullName).ReadAllText()));
			Assert.Equal(result.PlantUmlSource.SubdirRoot.FullPath, result.RaidManifest.SubdirRoot.FullPath);
			Assert.Equal(result.PlantUmlSource.SubdirRoot.FullPath, result.PlantUmlConfig.SubdirRoot.FullPath);
			Assert.Equal(result.PlantUmlSource.SubdirRoot.FullPath, result.Svg.SubdirRoot.FullPath);
			Assert.Equal(model.SemanticHash,
				new RaidFile(result.RaidManifest.FullName).LoadModel().SemanticHash);
			Assert.Equal(model.SemanticHash, result.SemanticHash);
			var svg = new TextFile(result.Svg.FullName).ReadAllText();
			Assert.Contains("PlantUML", svg, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain("Please use '!option handwritten true'", svg, StringComparison.Ordinal);
			Assert.Contains("font-family=\"Chalkduster, Comic Sans MS\"", svg, StringComparison.Ordinal);
			AssertProvenance(model, SvgProvenanceMetadata.Read(svg), result.ConfigHash, result.RenderHash);
			Assert.DoesNotContain(root.FullPath, svg, StringComparison.Ordinal);
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public async Task RenderAsync_UsesCheckedInLocalThemeWithRealPlantUml()
	{
		ConfigureRealPlantUml();
		var model = new RaidFile(ExampleRoot(), "ScheduleRehearsal").LoadModel();
		model.Manifest.Presentation.Theme = "raikeep-sketch";
		model.Manifest.Presentation.Handwritten = false;
		model.Manifest.Presentation.FontName = null;
		var themeRoot = new RaiPath(AppContext.BaseDirectory) / "Themes";
		var theme = new PumlThemeFile(themeRoot, model.Manifest.Presentation.Theme);
		Assert.True(theme.Exists());

		var root = Os.TempDir / "RAIkeep" / "raidiagram-tests" /
			nameof(RenderAsync_UsesCheckedInLocalThemeWithRealPlantUml);
		Cleanup(root);
		try
		{
			var result = await new PlantUmlDiagramRenderer().RenderAsync(
				model,
				new DiagramDestination
				{
					ImageTreeRoot = root / "images",
					Subscriber = "ThemeConsumer",
					ItemId = "ScheduleRehearsal"
				},
				new DiagramRenderOptions
				{
					ManifestUri = ExampleManifestUri,
					PlantUml = new PlantUmlCompileOptions { LocalThemeRoot = themeRoot }
				},
				TestContext.Current.CancellationToken);

			var source = new TextFile(result.PlantUmlSource.FullName).ReadAllText();
			Assert.DoesNotContain("!theme", source);
			var config = new TextFile(result.PlantUmlConfig.FullName).ReadAllText();
			Assert.Contains($"!theme raikeep-sketch from {themeRoot.FullPath}", config);
			var svg = new TextFile(result.Svg.FullName).ReadAllText();
			Assert.DoesNotContain("Please use '!option handwritten true'", svg, StringComparison.Ordinal);
			Assert.Contains("font-family=\"Chalkduster, Comic Sans MS\"", svg, StringComparison.Ordinal);
			Assert.DoesNotContain(themeRoot.FullPath, svg, StringComparison.Ordinal);
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public async Task RenderAsync_InjectsDiagramKindStyleFromCatalog()
	{
		ConfigureRealPlantUml();
		var catalog = new PumlStyleCatalog();
		catalog.Common.Set("root", PumlStyleProperty.FontName, "Chalkduster, Comic Sans MS");
		catalog.For(DiagramKind.UseCase)
			.Set("componentDiagram usecase", PumlStyleProperty.BackgroundColor, "#445566");
		catalog.For(DiagramKind.Class)
			.Set("classDiagram class", PumlStyleProperty.BackgroundColor, "#223344");
		var renderer = new PlantUmlDiagramRenderer(
			styleProvider: new DiagramStyleProvider(catalog, "tenant-sketch"));
		var model = new RaidFile(ExampleRoot(), "ScheduleRehearsal").LoadModel();
		var root = Os.TempDir / "RAIkeep" / "raidiagram-tests" /
			nameof(RenderAsync_InjectsDiagramKindStyleFromCatalog);
		Cleanup(root);
		try
		{
			var result = await renderer.RenderAsync(
				model,
				new DiagramDestination
				{
					ImageTreeRoot = root / "images",
					Subscriber = "AfricaStage",
					ItemId = "ScheduleRehearsal"
				},
				new DiagramRenderOptions { ManifestUri = ExampleManifestUri },
				TestContext.Current.CancellationToken);

			var source = new TextFile(result.PlantUmlSource.FullName).ReadAllText();
			var config = new TextFile(result.PlantUmlConfig.FullName).ReadAllText();
			var svg = new TextFile(result.Svg.FullName).ReadAllText();
			Assert.DoesNotContain("#445566", source);
			Assert.Contains("componentDiagram", config);
			Assert.Contains("#445566", config);
			Assert.DoesNotContain("classDiagram", config);
			Assert.Contains("fill=\"#445566\"", svg, StringComparison.OrdinalIgnoreCase);
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public async Task RenderAsync_UsesExplicitSubscriberStyleLocationsWithRealPlantUml()
	{
		ConfigureRealPlantUml();
		var model = new RaidFile(ExampleRoot(), "ScheduleRehearsal").LoadModel();
		model.Manifest.Presentation.Theme = RaiDiagramDefaults.SketchProfileId;
		model.Manifest.Presentation.Handwritten = false;
		model.Manifest.Presentation.FontName = null;
		var root = Os.TempDir / "RAIkeep" / "raidiagram-tests" /
			nameof(RenderAsync_UsesExplicitSubscriberStyleLocationsWithRealPlantUml);
		Cleanup(root);
		try
		{
			var imageTreeRoot = root / "images";
			var defaults = new DiagramStyleLocation(imageTreeRoot, "RAIkeep");
			var subscriber = new DiagramStyleLocation(imageTreeRoot, "AfricaStage");
			RaiDiagramDefaults.SeedTo(imageTreeRoot, defaults.Subscriber);
			PumlStyleFile.FromSubscriberProfile(
					subscriber.SubscriberRoot,
					RaiDiagramDefaults.SketchProfileId,
					"usecase")
				.Write(new PumlStyleSheet().Set(
					"componentDiagram usecase",
					PumlStyleProperty.BackgroundColor,
					"#445566"));

			var result = await new PlantUmlDiagramRenderer().RenderAsync(
				model,
				new DiagramDestination
				{
					ImageTreeRoot = imageTreeRoot,
					Subscriber = subscriber.Subscriber,
					ItemId = "ScheduleRehearsal"
				},
				new DiagramRenderOptions
				{
					ManifestUri = ExampleManifestUri,
					StyleProfileId = RaiDiagramDefaults.SketchProfileId,
					StyleLocations = [defaults, subscriber],
					RequireSubscriberStyleProfile = true
				},
				TestContext.Current.CancellationToken);

			var source = result.Artifacts.PlantUmlSource.ReadAllText();
			var config = result.Artifacts.PlantUmlConfig.ReadAllText();
			var svg = new TextFile(result.Svg.FullName).ReadAllText();
			var provenance = SvgProvenanceMetadata.Read(svg);
			Assert.DoesNotContain("!theme", source);
			Assert.DoesNotContain("<style>", source);
			Assert.Contains("subscriber=RAIkeep", config);
			Assert.Contains("subscriber=AfricaStage", config);
			Assert.Contains("#445566", config);
			Assert.Contains("fill=\"#445566\"", svg, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain("Please use '!option handwritten true'", svg, StringComparison.Ordinal);
			Assert.Contains("font-family=\"Chalkduster, Comic Sans MS\"", svg, StringComparison.Ordinal);
			Assert.Equal(result.StyleHash, provenance.StyleHash);
			Assert.Equal(result.StyleLayers, provenance.StyleLayers);
			Assert.Equal(2, result.StyleLayers.Count);
		}
		finally
		{
			Cleanup(root);
		}
	}

	private static RaiPath ExampleRoot()
		=> new RaiPath(AppContext.BaseDirectory) / "Examples" / "ScheduleRehearsal";

	private static void ConfigureRealPlantUml()
	{
		var configuredJar = Environment.GetEnvironmentVariable("RAIDIAGRAM_PLANTUML_JAR");
		if (string.IsNullOrWhiteSpace(configuredJar))
			return;

		var jar = new RaiFile(configuredJar);
		if (!jar.Exists())
			throw new RaiPathNotFoundException(
				$"The configured PlantUML CLI jar does not exist: {jar.FullName}",
				jar.FullName);
		PlantUml.PlantUmlPath = jar.Path;
		PlantUml.CommandName = jar.NameWithExtension;
		PlantUml.JavaCommand = "java";
	}

	private static void AssertProvenance(
		DiagramModel model,
		SvgProvenance provenance,
		string configHash,
		string renderHash)
	{
		Assert.Equal("ScheduleRehearsal", provenance.RaidId);
		Assert.Equal(model.SemanticHash, provenance.SemanticHash);
		Assert.Equal(DiagramManifest.CurrentSchemaVersion, provenance.SchemaVersion);
		Assert.Equal(ExampleManifestUri, provenance.ManifestUri);
		Assert.Equal("r1", provenance.ModelRevision);
		Assert.Equal(configHash, provenance.ConfigHash);
		Assert.Equal(renderHash, provenance.RenderHash);
	}

	private static string NormalizeLines(string value)
		=> value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

	private static void Cleanup(RaiPath root)
	{
		if (root.Exists())
			root.rmdir(depth: 7, deleteFiles: true);
	}
}
