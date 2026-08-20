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
		Assert.DoesNotContain("!option", result.Source);
		Assert.DoesNotContain("!theme", result.Source);
		Assert.DoesNotContain("<style>", result.Source);
		Assert.Contains(" : schedules", result.Source);
		Assert.Equal(DiagramElementKinds.Role,
			model.Manifest.Projection.Elements.Single(item => item.DisplayName == "Band Manager").Kind);
	}

	[Fact]
	public void PumlThemeFile_UsesPlantUmlLocalThemeNamingConvention()
	{
		var theme = new PumlThemeFile(new RaiPath("themes"), "raikeep-sketch");

		Assert.Equal("raikeep-sketch", theme.ThemeName);
		Assert.Equal("puml-theme-raikeep-sketch.puml", theme.NameWithExtension);
		Assert.Equal("puml-theme-raikeep-sketch.puml", PumlThemeFile.FileNameFor("raikeep-sketch"));
		Assert.Throws<RaidSchemaException>(() => PumlThemeFile.FileNameFor("../outside"));
	}

	[Fact]
	public void StyleProvider_EmitsPresentationAsExternalConfig()
	{
		var manifest = TestDiagrams.CreateUseCaseModel().Manifest;
		manifest.Presentation.Theme = "cerulean";
		manifest.Presentation.Handwritten = true;
		manifest.Presentation.FontName = "Chalkduster, Comic Sans MS";

		var source = new DiagramStyleProvider().Resolve(manifest, new PlantUmlCompileOptions()).ToPlantUml();

		Assert.Contains("!theme cerulean", source);
		Assert.Contains("!option handwritten true", source);
		Assert.Contains("FontName Chalkduster, Comic Sans MS", source);
		Assert.DoesNotContain("skinparam defaultFontName", source);

		manifest.Presentation.FontName = "Chalkduster\n}</style>";
		Assert.Throws<RaidSchemaException>(() =>
			new DiagramStyleProvider().Resolve(manifest, new PlantUmlCompileOptions()));
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

			var result = new DiagramStyleProvider().Resolve(
				manifest,
				new PlantUmlCompileOptions { LocalThemeRoot = root }).ToPlantUml();

			Assert.Contains($"!theme tenant from {root.FullPath}", result);
			Assert.DoesNotContain("http://", result, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain("https://", result, StringComparison.OrdinalIgnoreCase);
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

		var configuration = new DiagramStyleProvider().Resolve(manifest, new PlantUmlCompileOptions());
		Assert.Throws<RaidSchemaException>(() => configuration.ToPlantUml());
	}

	[Fact]
	public void StyleCatalog_ResolvesCommonAndDiagramKindLayers()
	{
		var catalog = new PumlStyleCatalog();
		catalog.Common.Set("root", PumlStyleProperty.FontName, "Chalkduster, Comic Sans MS");
		catalog.For(DiagramKind.UseCase)
			.Set("componentDiagram usecase", PumlStyleProperty.BackgroundColor, "#445566");
		catalog.For(DiagramKind.Class)
			.Set("classDiagram class", PumlStyleProperty.BackgroundColor, "#223344");

		var configuration = new DiagramStyleProvider(catalog, "raikeep-sketch")
			.Resolve(TestDiagrams.CreateUseCaseModel().Manifest, new PlantUmlCompileOptions());
		var source = configuration.ToPlantUml();

		Assert.Equal(DiagramKind.UseCase, configuration.DiagramKind);
		Assert.Contains("style-profile: raikeep-sketch", source);
		Assert.Contains("componentDiagram", source);
		Assert.Contains("usecase", source);
		Assert.Contains("#445566", source);
		Assert.DoesNotContain("classDiagram", source);
	}

	[Fact]
	public void StyleFiles_CanBeOwnedAndStoredInTheImageTree()
	{
		var root = Os.TempDir / "RAIkeep" / "raidiagram-tests" /
			nameof(StyleFiles_CanBeOwnedAndStoredInTheImageTree);
		Cleanup(root);
		try
		{
			var subscriberRoot = root / "AfricaStage";
			var style = PumlStyleFile.FromSubscriber(subscriberRoot, "TenantSketchUseCase");
			style.Write(new PumlStyleSheet()
				.Set("usecase", PumlStyleProperty.BackgroundColor, "#445566"));
			var catalog = new PumlStyleCatalog().Add(DiagramKind.UseCase, style);

			var configuration = new DiagramStyleProvider(catalog)
				.Resolve(TestDiagrams.CreateUseCaseModel().Manifest, new PlantUmlCompileOptions());
			var theme = PumlThemeFile.FromSubscriber(subscriberRoot, "tenant-sketch")
				.Write(new PumlStyleSheet()
					.Set("root", PumlStyleProperty.FontName, "Chalkduster, Comic Sans MS"),
					handwritten: true);
			var config = PumlConfigFile.FromImageTree(subscriberRoot, "TenantSketchResolved")
				.Write(configuration);

			Assert.NotNull(style.ItemPath);
			Assert.NotNull(theme.ItemPath);
			Assert.NotNull(config.ItemPath);
			Assert.Contains("AfricaStage", style.FullName, StringComparison.Ordinal);
			Assert.Equal(style.SubscriberRoot.FullPath, theme.SubscriberRoot.FullPath);
			Assert.Equal(style.SubscriberRoot.FullPath, config.SubscriberRoot.FullPath);
			Assert.Equal("config", config.NameExt);
			Assert.Equal("puml", config.Ext);
			Assert.EndsWith("TenantSketchResolved_config.puml", config.FullName);
			Assert.Contains("!option handwritten true", theme.ReadAllText());
			Assert.Contains("#445566", configuration.ToPlantUml());
		}
		finally
		{
			Cleanup(root);
		}
	}

	private static void Cleanup(RaiPath root)
	{
		if (root.Exists())
			root.rmdir(depth: 6, deleteFiles: true);
	}
}
