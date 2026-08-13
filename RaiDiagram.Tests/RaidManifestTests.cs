using OsLib;

namespace RaiDiagram.Tests;

public class RaidManifestTests
{
	[Fact]
	public void Parse_AcceptsAgentFriendlyJson5Profile()
	{
		const string source = """
		{
		  // JSON5 comments and unquoted property names are intentional.
		  schemaVersion: '1.0',
		  diagram: {
		    id: 'ScheduleRehearsal',
		    title: 'Schedule rehearsal',
		    kind: 'UseCase',
		    purpose: 'agent-readable\
		manifest',
		  },
		  model: {
		    providerScheme: 'test-model',
		    modelId: 'theatre',
		  },
		  projection: {
		    elements: [],
		    relationships: [],
		    selectionRules: [],
		  },
		  presentation: {
		    layoutHints: {
		      hexValue: 0x10,
		      leadingDecimal: .5,
		      trailingDecimal: 5.,
		      explicitlyPositive: +1,
		    },
		  },
		  annotations: [],
		}
		""";

		var manifest = RaidJson5.Parse(source);

		Assert.Equal("ScheduleRehearsal", manifest.Diagram.Id);
		Assert.Equal(DiagramKind.UseCase, manifest.Diagram.Kind);
		Assert.Equal("test-model", manifest.Model.ProviderScheme);
		Assert.Equal("agent-readablemanifest", manifest.Diagram.Purpose);
		Assert.Equal("16", manifest.Presentation.LayoutHints["hexValue"]);
		Assert.Equal("0.5", manifest.Presentation.LayoutHints["leadingDecimal"]);
	}

	[Fact]
	public void Serialize_ProducesStrictJsonThatRemainsValidJson5()
	{
		var manifest = TestDiagrams.CreateUseCaseModel().Manifest;

		var serialized = RaidJson5.Serialize(manifest);
		var reparsed = RaidJson5.Parse(serialized);

		Assert.Contains("\"schemaVersion\"", serialized);
		Assert.Equal(manifest.Diagram.Id, reparsed.Diagram.Id);
		Assert.Equal(DiagramSemanticHasher.Compute(manifest), DiagramSemanticHasher.Compute(reparsed));
	}

	[Fact]
	public void SemanticHash_IgnoresPresentationAndCapturedRevision()
	{
		var original = TestDiagrams.CreateUseCaseModel().Manifest;
		var changed = RaidJson5.Parse(RaidJson5.Serialize(original));
		changed.Presentation.Theme = "cerulean";
		changed.Presentation.FontName = "Aptos";
		changed.Presentation.Handwritten = true;
		changed.Presentation.Frames.Add(new DiagramPresentationFrame
		{
			Id = "roles",
			Title = "Participants",
			ElementIds = ["band-manager"]
		});
		changed.Model.CapturedRevision = "r2";

		Assert.Equal(DiagramSemanticHasher.Compute(original), DiagramSemanticHasher.Compute(changed));
		Assert.NotEqual(DiagramSemanticHasher.ComputePresentation(original), DiagramSemanticHasher.ComputePresentation(changed));
	}

	[Fact]
	public void SemanticHash_ChangesWithSemanticRelationship()
	{
		var original = TestDiagrams.CreateUseCaseModel().Manifest;
		var changed = RaidJson5.Parse(RaidJson5.Serialize(original));
		changed.Projection.Relationships.Single().Label = "coordinates";

		Assert.NotEqual(DiagramSemanticHasher.Compute(original), DiagramSemanticHasher.Compute(changed));
	}

	[Fact]
	public void Parse_RejectsUnsupportedVersionAndDuplicateProperties()
	{
		var unsupported = RaidJson5.Serialize(TestDiagrams.CreateUseCaseModel().Manifest)
			.Replace("\"1.0\"", "\"2.0\"", StringComparison.Ordinal);
		Assert.Throws<RaidSchemaException>(() => RaidJson5.Parse(unsupported));

		Assert.Throws<RaidSchemaException>(() => RaidJson5.Parse("""
		{
		  schemaVersion: '1.0',
		  schemaVersion: '1.0',
		  diagram: {}, model: {}, projection: {}, presentation: {}, annotations: []
		}
		"""));
	}

	[Fact]
	public void Parse_RejectsNonFiniteJson5Numbers()
	{
		var source = RaidJson5.Serialize(TestDiagrams.CreateUseCaseModel().Manifest)
			.Replace("\"layoutHints\": {}", "\"layoutHints\": { \"weight\": NaN }", StringComparison.Ordinal);

		Assert.Throws<RaidSchemaException>(() => RaidJson5.Parse(source));
	}

	[Fact]
	public void RaidFile_RoundTripsThroughOsLib()
	{
		var root = Os.TempDir / "RAIkeep" / "raidiagram-tests" / nameof(RaidFile_RoundTripsThroughOsLib);
		Cleanup(root);
		try
		{
			var file = new RaidFile(root, "ScheduleRehearsal");
			file.SaveManifest(TestDiagrams.CreateUseCaseModel().Manifest);

			Assert.True(file.Exists());
			Assert.EndsWith(".raid", file.FullName, StringComparison.OrdinalIgnoreCase);
			Assert.Equal("ScheduleRehearsal", file.LoadManifest().Diagram.Id);
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public void Assembly_HasNoAiaWwwaOrJsonPitDependency()
	{
		var names = typeof(DiagramManifest).Assembly.GetReferencedAssemblies().Select(item => item.Name).ToArray();
		Assert.DoesNotContain("AIA.Core", names);
		Assert.DoesNotContain("AIA", names);
		Assert.DoesNotContain("WWWA", names);
		Assert.DoesNotContain("JsonPit", names);
	}

	private static void Cleanup(RaiPath root)
	{
		if (root.Exists())
			root.rmdir(depth: 6, deleteFiles: true);
	}
}
