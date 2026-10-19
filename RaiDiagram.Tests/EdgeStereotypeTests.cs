using System.IO;
using RaiDiagram.Builders;

namespace RaiDiagram.Tests;

public sealed class EdgeStereotypeTests
{
	[Fact]
	public void RelationshipFluentContract_PreservesDomainSpelling()
	{
		var relationship = Sample().Projection.Relationships.Single();

		Assert.Same(relationship, relationship.WithStereotype("ReviewsOrder"));
		Assert.Equal("ReviewsOrder", relationship.Stereotype);
	}

	[Fact]
	public void RaidRoundTrip_PersistsStereotypeAndChangesSemanticHash()
	{
		var manifest = Sample();
		var before = DiagramSemanticHasher.Compute(manifest);
		manifest.Projection.Relationships.Single().WithStereotype("precedes");

		var json = RaidJson5.Serialize(manifest);
		var restored = RaidJson5.Parse(json);

		Assert.Contains("\"stereotype\": \"precedes\"", json);
		Assert.Equal("precedes", restored.Projection.Relationships.Single().Stereotype);
		Assert.NotEqual(before, DiagramSemanticHasher.Compute(restored));
	}

	[Fact]
	public void PlantUmlCompiler_EmitsStereotypeLabelAndNormalizesNotation()
	{
		var manifest = Sample();
		var relationship = manifest.Projection.Relationships.Single();
		relationship.WithStereotype("«spawns»");
		relationship.Label = "on conclude";

		var withLabel = new PlantUmlDiagramCompiler().Compile(manifest).Source;
		Assert.Contains("--> ", withLabel);
		Assert.Contains(" : <<spawns>>\\non conclude", withLabel);

		relationship.WithStereotype("invokes");
		relationship.Label = null;
		var withoutLabel = new PlantUmlDiagramCompiler().Compile(manifest).Source;
		Assert.Contains(" : <<invokes>>", withoutLabel);
		Assert.DoesNotContain("<<<<", withoutLabel);
	}

	[Fact]
	public void PlantUmlImport_PreservesSourceStereotypeAndLegacyManifests()
	{
		const string source = """
			@startuml OrderFlow
			object "Order" as order
			object "Invoice" as invoice
			order --> invoice : <<ApprovesInvoice>>\nreviewed
			@enduml
			""";
		var imported = new PlantUmlModelImporter().Import(new StringReader(source)).Manifest;
		var relationship = Assert.Single(imported.Projection.Relationships);
		Assert.Equal("ApprovesInvoice", relationship.Stereotype);
		Assert.Equal("reviewed", relationship.Label);

		var legacy = Sample();
		var legacyJson = RaidJson5.Serialize(legacy);
		Assert.DoesNotContain("\"stereotype\"", legacyJson);
		Assert.Null(RaidJson5.Parse(legacyJson).Projection.Relationships.Single().Stereotype);
	}

	[Fact]
	public void DerivedSvg_ShowsStereotypeAndExposesItToHydration()
	{
		var manifest = Sample();
		var relationship = manifest.Projection.Relationships.Single();
		relationship.WithStereotype("ReviewsOrder");
		relationship.Label = "approved";

		var artifacts = new DiagramArtifactManager().Derive(manifest);
		Assert.Contains("aim-stereotype=\"ReviewsOrder\"", artifacts.Svg);
		Assert.Contains("«ReviewsOrder» approved", artifacts.Svg);
		AimSvg.Validate(artifacts.Svg);
	}

	[Fact]
	public void DistributionBuilder_UsesTypedProtocolAndPreservesDomain()
	{
		var manifest = new DistributionDiagramBuilder("Servers", new DiagramModelIdentity
		{
			ProviderScheme = "example", ModelId = "Servers", Domain = "Examples"
		})
			.AddNode("host", "Server")
			.AddDatabase("store", "Database")
			.AddCommunication("host", "store", "TCP")
			.BuildManifest();

		Assert.Equal("Examples", manifest.Model.Domain);
		Assert.Equal("TCP", Assert.Single(manifest.Projection.Relationships).Stereotype);
		Assert.Contains("<<TCP>>", new PlantUmlDiagramCompiler().Compile(manifest).Source);
	}

	private static DiagramManifest Sample() => new()
	{
		SchemaVersion = DiagramManifest.ExtendedSchemaVersion,
		Diagram = new DiagramIdentity { Id = "OrderFlow", Title = "Order flow", Kind = DiagramKind.Mixed },
		Model = new DiagramModelIdentity { ProviderScheme = "test-model", ModelId = "OrderFlow" },
		Projection = new DiagramProjection
		{
			Elements =
			[
				new DiagramElement { Id = "prepare", Kind = DiagramElementKinds.Activity, DisplayName = "Prepare order" },
				new DiagramElement { Id = "ship", Kind = DiagramElementKinds.Activity, DisplayName = "Ship order" }
			],
			Relationships =
			[
				new DiagramRelationship
				{
					Id = "order-transition", Kind = DiagramRelationshipKinds.Association,
					SourceId = "prepare", TargetId = "ship", Directed = true
				}
			]
		}
	};
}
