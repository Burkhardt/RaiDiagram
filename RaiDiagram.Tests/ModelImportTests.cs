using OsLib;

namespace RaiDiagram.Tests;

public sealed class ModelImportTests
{
	[Fact]
	public void PlantUmlImporter_ImportsCr036Workflow_AndEmitsHydratableAimSvg()
	{
		var fixtureRoot = new RaiPath(AppContext.BaseDirectory) / "Fixtures";
		var source = new TextFile(fixtureRoot, "ChangeRequestWorkflow", "puml").ReadAllText();
		var importer = new PlantUmlModelImporter();

		var imported = importer.Import(new StringReader(source));
		var manifest = imported.Manifest;
		var svg = AimSvg.Emit(manifest);

		Assert.Equal("ChangeRequestWorkflow", manifest.Diagram.Id);
		Assert.Equal(DiagramKind.Activity, manifest.Diagram.Kind);
		Assert.Contains(manifest.Projection.Elements,
			item => item.DisplayName == "Run Independent Consumer Verification Tests (Receiver-Led Definition of Done)");
		Assert.Contains(manifest.Projection.Elements, item => item.Kind == DiagramElementKinds.Decision);
		Assert.Contains(manifest.Projection.Relationships,
			item => item.Kind == DiagramRelationshipKinds.ControlFlow && item.Guard == "Yes - Accepted");
		Assert.NotNull(manifest.Presentation.Canvas);
		Assert.All(manifest.Presentation.Canvas!.Nodes,
			item => Assert.True(item.Width > 0 && item.Height > 0));
		Assert.Contains("aim-node=\"true\"", svg);
		Assert.Contains("aim-act=\"true\"", svg);
		Assert.Contains("aim-edge=\"true\"", svg);
		Assert.DoesNotContain("NaN", svg, StringComparison.OrdinalIgnoreCase);
		AimSvg.Validate(svg);
	}

	[Fact]
	public void PlantUmlImporter_ImportsClassMembersAndSupportedRelationships()
	{
		const string source = """
		@startuml Domain
		abstract class Actor {
		  +Name : string
		  +Perform() : void
		}
		interface Schedulable
		class Agent
		class Calendar
		class Entry
		Actor <|-- Agent
		Agent --> Schedulable : implements behavior
		Calendar *-- Entry : entries
		Agent o-- Calendar : calendars
		@enduml
		""";

		var manifest = new PlantUmlModelImporter().Import(new StringReader(source)).Manifest;

		Assert.Equal(DiagramKind.Class, manifest.Diagram.Kind);
		var actor = Assert.Single(manifest.Projection.Elements, item => item.DisplayName == "Actor");
		Assert.Equal(DiagramElementKinds.Class, actor.Kind);
		Assert.Equal("true", actor.RelevantFacts["abstract"].Text);
		Assert.Contains(actor.RelevantFacts, item => item.Key.Contains("attribute") && item.Value.Text == "+Name : string");
		Assert.Contains(actor.RelevantFacts, item => item.Key.Contains("method") && item.Value.Text == "+Perform() : void");
		Assert.Contains(manifest.Projection.Relationships, item => item.Kind == DiagramRelationshipKinds.Generalization);
		Assert.Contains(manifest.Projection.Relationships, item => item.Kind == DiagramRelationshipKinds.Association);
		Assert.Contains(manifest.Projection.Relationships, item => item.Kind == DiagramRelationshipKinds.Containment);
		Assert.Contains(manifest.Projection.Relationships, item => item.Kind == DiagramRelationshipKinds.Aggregation);
		AimSvg.Validate(AimSvg.Emit(manifest));
	}

	[Fact]
	public void PlantUmlImporter_ParsesForksAndDeterministicCanvasRoundTrip()
	{
		const string source = """
		@startuml ParallelWork
		start
		:Prepare;
		fork
		  :Build;
		fork again
		  :Test;
		end fork
		:Publish;
		stop
		@enduml
		""";

		var first = new PlantUmlModelImporter().Import(new StringReader(source)).Manifest;
		var second = new PlantUmlModelImporter().Import(new StringReader(source)).Manifest;

		Assert.Equal(RaidJson5.Serialize(first), RaidJson5.Serialize(second));
		Assert.Equal(AimSvg.Emit(first), AimSvg.Emit(second));
		Assert.Equal(2, first.Projection.Relationships.Count(item =>
			first.Projection.Elements.Single(element => element.Id == item.TargetId).DisplayName == "Publish"));
	}

	[Fact]
	public void AimSvgValidator_RejectsMissingAndNonFiniteHydrationGeometry()
	{
		const string missingNodes = "<svg xmlns=\"http://www.w3.org/2000/svg\" />";
		const string nonFinite = """
		<svg xmlns="http://www.w3.org/2000/svg">
		  <g aim-node="true" aim-id="a" aim-kind="act" transform="translate(NaN,0)" />
		</svg>
		""";

		Assert.Throws<RaidSchemaException>(() => AimSvg.Validate(missingNodes));
		Assert.Throws<RaidSchemaException>(() => AimSvg.Validate(nonFinite));
	}
}
