using RaiDiagram.Builders;

namespace RaiDiagram.Tests;

public sealed class StructuralImportTests
{
	[Fact]
	public void ObjectSlotsAreOrderedText_AndVisibleInSvg()
	{
		const string source = """
		@startuml Orders
		object "<u>Order42</u> : Order" as order {
		  Identifier = 123456789012345678901234567890
		  Data = { 'Values': [1, 2], 'Text': '}' }
		  Link = https://example.invalid/order?a=1&b=2
		}
		actor "Customer" as customer
		object "Invoice42 : Invoice" as invoice { Status = Open }
		order <-- customer : places
		order ..> invoice : billed by
		order --> invoice : references
		@enduml
		""";
		var manifest = new PlantUmlModelImporter().Import(new StringReader(source)).Manifest;
		Assert.Equal(DiagramKind.Object, manifest.Diagram.Kind);
		var order = Assert.Single(manifest.Projection.Elements, e => e.Id == "order");
		Assert.Equal(new[] { "Identifier", "Data", "Link" }, order.ObjectProperties.Select(p => p.Name));
		Assert.Equal("123456789012345678901234567890", order.ObjectProperties[0].Value);
		Assert.Equal(3, manifest.Projection.Relationships.Count);
		Assert.Equal("customer", manifest.Projection.Relationships[0].SourceId);
		Assert.Equal("order", manifest.Projection.Relationships[0].TargetId);
		var derived = new DiagramArtifactManager().Derive(manifest);
		Assert.Contains("Identifier = 123456789012345678901234567890", derived.PlantUml);
		Assert.Contains("Identifier = 123456789012345678901234567890", derived.Svg);
		Assert.Contains("text-decoration=\"underline\"", derived.Svg);
		AimSvg.Validate(derived.Svg);
		var restored = RaidJson5.Parse(derived.Raid);
		Assert.Equal(order.ObjectProperties[1].Value, restored.Projection.Elements.Single(e => e.Id == "order").ObjectProperties[1].Value);
	}

	[Theory]
	[InlineData("@startuml\n@enduml", "PUML003", 1)]
	[InlineData("!theme plain", "PUML003", 1)]
	[InlineData("@startuml\nobject \"Order\n@enduml", "PUML001", 2)]
	[InlineData("@startuml\nstart\nelse\nstop\n@enduml", "PUML001", 3)]
	[InlineData("@startuml\nobject Order {\nValue = 1\n@enduml", "PUML001", 4)]
	[InlineData("@startuml\nobject Order\nunknown Thing\n@enduml", "PUML002", 3)]
	[InlineData("@startuml\nobject Order <<unknown>>\n@enduml", "PUML002", 2)]
	[InlineData("@startuml\nobject Order\nOrder --> Missing\n@enduml", "PUML004", 3)]
	[InlineData("@startuml\n!include external.puml\n@enduml", "PUML005", 2)]
	public void ErrorsCarryStableSourceLocations(string source, string code, int line)
	{
		var error = Assert.Throws<PlantUmlImportException>(() => new PlantUmlModelImporter().Import(new StringReader(source), "sample.puml"));
		Assert.Equal(code, error.Diagnostic.Code);
		Assert.Equal(line, error.Diagnostic.Line);
		Assert.Equal("sample.puml", error.Diagnostic.Source);
		Assert.DoesNotContain("aim-node", error.Message);
	}

	[Fact]
	public void RelationshipDirectionAndLiteralSlotsSurvivePlainTextReimport()
	{
		const string source = "@startuml\nclass Order\ninterface Customer\nobject Invoice { Text = \"a\\b\" }\nOrder ..|> Customer\nOrder --> Invoice\n@enduml";
		var manifest = new PlantUmlModelImporter().Import(new StringReader(source)).Manifest;
		var puml = new PlantUmlDiagramCompiler().Compile(manifest).Source;
		// Remove embedded manifest comments so this exercises the text parser, not metadata recovery.
		puml = string.Join('\n', puml.Split('\n').Where(l => !l.TrimStart().StartsWith("'", StringComparison.Ordinal)));
		var restored = new PlantUmlModelImporter().Import(new StringReader(puml)).Manifest;
		var realization = Assert.Single(restored.Projection.Relationships, r => r.Kind == DiagramRelationshipKinds.Realization);
		Assert.Equal("Order", restored.Projection.Elements.Single(e => e.Id == realization.SourceId).DisplayName);
		Assert.Equal("Customer", restored.Projection.Elements.Single(e => e.Id == realization.TargetId).DisplayName);
		Assert.True(Assert.Single(restored.Projection.Relationships, r => r.Kind == DiagramRelationshipKinds.Association).Directed);
		Assert.Equal(manifest.Projection.Elements.Single(e => e.Kind == DiagramElementKinds.Object).ObjectProperties[0].Value,
			restored.Projection.Elements.Single(e => e.Kind == DiagramElementKinds.Object).ObjectProperties[0].Value);
	}

	[Fact]
	public void DistributionBuilderAndTextImportRetainContainment()
	{
		const string source = """
		@startuml Servers
		node "ServerNode" as server {
		  component "OrderService" as service
		  database "OrderStore" as store
		}
		service --> store : <<TCP>>
		@enduml
		""";
		var manifest = new PlantUmlModelImporter().Import(new StringReader(source)).Manifest;
		Assert.Equal(DiagramKind.Deployment, manifest.Diagram.Kind);
		Assert.Equal("server", manifest.Projection.Elements.Single(e => e.Id == "service").ParentId);
		var puml = new PlantUmlDiagramCompiler().Compile(manifest).Source;
		Assert.Contains("node \"ServerNode\"", puml);
		Assert.Contains("component \"OrderService\"", puml);
		var built = new DistributionDiagramBuilder("Servers", new DiagramModelIdentity
		{
			ProviderScheme = "test", ModelId = "servers", CapturedRevision = "1"
		}).AddNode("server", "ServerNode").AddComponent("service", "OrderService", "server").BuildManifest();
		Assert.Equal("Servers_VD", built.Diagram.Id);
	}
}
