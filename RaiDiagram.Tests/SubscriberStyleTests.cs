using OsLib;
using RaiImage;

namespace RaiDiagram.Tests;

public sealed class SubscriberStyleTests
{
	[Fact]
	public void Repository_ResolvesSubscriberAndDiagramKindLayersInDeterministicOrder()
	{
		var root = NewRoot();
		try
		{
			var defaults = new DiagramStyleLocation(root, "RAIkeep");
			var tenant = new DiagramStyleLocation(root, "AfricaStage-Tenant-A");
			var seeded = RaiDiagramDefaults.SeedTo(root, defaults.Subscriber);
			PumlStyleFile.FromSubscriberProfile(tenant.SubscriberRoot, RaiDiagramDefaults.SketchProfileId, "common")
				.Write(new PumlStyleSheet().Set("root", PumlStyleProperty.FontColor, "#112233"));
			PumlStyleFile.FromSubscriberProfile(tenant.SubscriberRoot, RaiDiagramDefaults.SketchProfileId, "usecase")
				.Write(new PumlStyleSheet().Set(
					"componentDiagram usecase",
					PumlStyleProperty.BackgroundColor,
					"#445566"));
			var manifest = TestDiagrams.CreateUseCaseModel().Manifest;
			manifest.Presentation.Theme = RaiDiagramDefaults.SketchProfileId;
			var context = new DiagramStyleResolutionContext
			{
				Manifest = manifest,
				Locations = [defaults, tenant],
				ProfileId = RaiDiagramDefaults.SketchProfileId,
				RequireSubscriberProfile = true
			};
			var provider = new DiagramStyleProvider(
				repository: new ImageTreeDiagramStyleRepository());

			var first = provider.Resolve(context);
			var second = provider.Resolve(context);
			var config = first.Configuration.ToPlantUml();

			Assert.True(seeded.Theme.Exists());
			Assert.Equal(first.StyleHash, second.StyleHash);
			Assert.Equal(3, first.Layers.Count);
			Assert.Equal("RAIkeep", first.Layers[0].Location.Subscriber);
			Assert.Equal("theme", first.Layers[0].Scope);
			Assert.Equal("common", first.Layers[1].Scope);
			Assert.Equal("UseCase", first.Layers[2].Scope);
			Assert.Equal([-1, 2, 3], first.Layers.Select(layer => layer.Precedence));
			Assert.Contains("!option handwritten true", config);
			Assert.Contains("#112233", config);
			Assert.Contains("#445566", config);
			Assert.DoesNotContain(root.FullPath, config, StringComparison.Ordinal);
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public void Repository_KeepsTenantOverridesIsolated()
	{
		var root = NewRoot();
		try
		{
			var tenantA = new DiagramStyleLocation(root, "AfricaStage-Tenant-A");
			var tenantB = new DiagramStyleLocation(root, "AfricaStage-Tenant-B");
			PumlStyleFile.FromSubscriberProfile(tenantA.SubscriberRoot, "tenant", "common")
				.Write(new PumlStyleSheet().Set("root", PumlStyleProperty.FontColor, "#AAAAAA"));
			PumlStyleFile.FromSubscriberProfile(tenantB.SubscriberRoot, "tenant", "common")
				.Write(new PumlStyleSheet().Set("root", PumlStyleProperty.FontColor, "#BBBBBB"));
			var provider = new DiagramStyleProvider(repository: new ImageTreeDiagramStyleRepository());

			var configA = provider.Resolve(Context(tenantA)).Configuration.ToPlantUml();
			var configB = provider.Resolve(Context(tenantB)).Configuration.ToPlantUml();

			Assert.Contains("#AAAAAA", configA);
			Assert.DoesNotContain("#BBBBBB", configA);
			Assert.Contains("#BBBBBB", configB);
			Assert.DoesNotContain("#AAAAAA", configB);
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public void Defaults_SeedExplicitlyAndPreserveSubscriberChanges()
	{
		var root = NewRoot();
		try
		{
			const string subscriber = "AIA";
			var first = RaiDiagramDefaults.SeedTo(root, subscriber);
			first.Theme.DeleteAll().Append("' subscriber edit").Save();

			var preserved = RaiDiagramDefaults.SeedTo(root, subscriber);
			var preservedContent = preserved.Theme.ReadAllText().Trim();
			var replaced = RaiDiagramDefaults.SeedTo(
				root,
				subscriber,
				behavior: DiagramDefaultSeedBehavior.ReplaceExisting);

			Assert.True(first.Changed);
			Assert.False(preserved.Changed);
			Assert.Equal("' subscriber edit", preservedContent);
			Assert.True(replaced.Changed);
			Assert.Contains("Chalkduster, Comic Sans MS", replaced.Theme.ReadAllText());
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public void ExplicitMissingSubscriberProfileFailsFast()
	{
		var root = NewRoot();
		try
		{
			var provider = new DiagramStyleProvider(repository: new ImageTreeDiagramStyleRepository());
			var location = new DiagramStyleLocation(root, "MissingTenant");

			Assert.Throws<RaiPathNotFoundException>(() => provider.Resolve(Context(location)));
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public void DuplicateSubscribersAndTraversalAreRejected()
	{
		var root = NewRoot();
		try
		{
			Assert.Throws<ArgumentException>(() => new DiagramStyleLocation(root, "AfricaStage/Tenant-A"));
			Assert.Throws<ArgumentException>(() => new DiagramStyleLocation(root, ".."));
			var location = new DiagramStyleLocation(root, "AIA");
			var repository = new ImageTreeDiagramStyleRepository();
			Assert.Throws<RaidSchemaException>(() => repository.ResolveLayers(new DiagramStyleResolutionContext
			{
				Manifest = TestDiagrams.CreateUseCaseModel().Manifest,
				Locations = [location, location],
				ProfileId = "tenant"
			}));
		}
		finally
		{
			Cleanup(root);
		}
	}

	private static DiagramStyleResolutionContext Context(DiagramStyleLocation location)
		=> new()
		{
			Manifest = TestDiagrams.CreateUseCaseModel().Manifest,
			Locations = [location],
			ProfileId = "tenant",
			RequireSubscriberProfile = true
		};

	private static RaiPath NewRoot()
	{
		var root = Os.TempDir / "RAIkeep" / "raidiagram-tests" / nameof(SubscriberStyleTests) / Guid.NewGuid().ToString("N");
		root.mkdir();
		return root;
	}

	private static void Cleanup(RaiPath root)
	{
		if (root.Exists())
			root.rmdir(depth: 8, deleteFiles: true);
	}
}
