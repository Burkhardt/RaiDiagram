using System;
using System.IO;
using System.Linq;
using RaiDiagram;
using Xunit;

namespace RaiDiagram.Tests;

public sealed class Cr052AcceptanceTests
{
    private static DiagramManifest Import(string source) =>
        new PlantUmlModelImporter().Import(new StringReader(source)).Manifest;

    [Fact]
    public void PureObjectSource_HasObjectKind_WithoutRoundTripMetadata()
    {
        var manifest = Import("""
            @startuml Sample
            object "Order42 : Order" as record
            @enduml
            """);

        Assert.Equal(DiagramKind.Object, manifest.Diagram.Kind);
        Assert.Equal(DiagramElementKinds.Object,
            Assert.Single(manifest.Projection.Elements).Kind);
    }

    [Fact]
    public void ObjectsAndActor_RetainKindsAndReverseArrowEndpoints()
    {
        var manifest = Import("""
            @startuml Sample
            allowmixing
            object "<u>Order42</u> : Order" as session
            actor "Customer Seven" as visitor
            object "InvoiceBlue : Invoice" as room
            session <-- visitor : attends
            session ..> room : location
            @enduml
            """);

        Assert.Equal(3, manifest.Projection.Elements.Count);
        Assert.Equal(2, manifest.Projection.Elements.Count(e => e.Kind == DiagramElementKinds.Object));
        var actor = Assert.Single(manifest.Projection.Elements, e => e.Kind == DiagramElementKinds.Role);
        var session = Assert.Single(manifest.Projection.Elements, e => e.DisplayName.Contains("Order42"));
        var room = Assert.Single(manifest.Projection.Elements, e => e.DisplayName.Contains("InvoiceBlue"));
        Assert.Equal(2, manifest.Projection.Relationships.Count);
        var attends = Assert.Single(manifest.Projection.Relationships, e => e.Label == "attends");
        Assert.Equal(actor.Id, attends.SourceId);
        Assert.Equal(session.Id, attends.TargetId);
        var location = Assert.Single(manifest.Projection.Relationships, e => e.Label == "location");
        Assert.Equal(session.Id, location.SourceId);
        Assert.Equal(room.Id, location.TargetId);
        Assert.Equal(DiagramRelationshipKinds.Dependency, location.Kind);
        AimSvg.Validate(new DiagramArtifactManager().Derive(manifest).Svg);
    }

    [Fact]
    public void SlotText_SurvivesManifestPumlSvgAndRoundTrip()
    {
        var manifest = Import("""
            @startuml Sample
            object "Order42 : Order" as record {
              Caption = Cedar Ω
              Identifier = 123456789012345678901234567890
              Payload = { 'Key': 'demo', 'Values': [1, 2] }
              Link = https://example.invalid/view?a=1&b=2
            }
            @enduml
            """);

        var item = Assert.Single(manifest.Projection.Elements);
        var fields = item.ObjectProperties.Select(p => $"{p.Name} = {p.Value}").ToArray();
        Assert.Equal(new[]
        {
            "Caption = Cedar Ω",
            "Identifier = 123456789012345678901234567890",
            "Payload = { 'Key': 'demo', 'Values': [1, 2] }",
            "Link = https://example.invalid/view?a=1&b=2"
        }, fields);
        var artifacts = new DiagramArtifactManager().Derive(manifest);
        Assert.Contains("Cedar Ω", artifacts.Raid);
        Assert.Contains("Caption = Cedar Ω", artifacts.PlantUml);
        // Presence in JSON alone is insufficient: the attribute must be visible.
        var svg = System.Xml.Linq.XDocument.Parse(artifacts.Svg);
        Assert.Contains(svg.Descendants(), e => e.Name.LocalName == "text" && e.Value.Contains("Cedar Ω"));
        AimSvg.Validate(artifacts.Svg);
        var restored = Import(artifacts.PlantUml);
        Assert.Equal(DiagramSemanticHasher.Compute(manifest), DiagramSemanticHasher.Compute(restored));
    }

    [Fact]
    public void ClassAndObject_DoNotCollapseIntoTwoClasses()
    {
        var manifest = Import("""
            @startuml Sample
            allowmixing
            class Order
            object "Order42 : Order" as instance
            instance ..> Order : refers to type
            @enduml
            """);
        Assert.Equal(DiagramKind.Mixed, manifest.Diagram.Kind);
        Assert.Equal(2, manifest.Projection.Elements.Count);
        Assert.Single(manifest.Projection.Elements, e => e.Kind == DiagramElementKinds.Class);
        Assert.Single(manifest.Projection.Elements, e => e.Kind == DiagramElementKinds.Object);
        Assert.Single(manifest.Projection.Relationships);
    }

    [Fact]
    public void ClassMembers_AreEmitted_UsingSharedMemberContract()
    {
        var manifest = Import("""
            @startuml Sample
            class Order {
              +Caption : string
              +Describe() : string
            }
            @enduml
            """);
        var puml = new DiagramArtifactManager().Derive(manifest).PlantUml;
        Assert.Contains("+Caption : string", puml);
        Assert.Contains("+Describe() : string", puml);
    }

    [Theory]
    [InlineData("@startuml\n@enduml")]
    [InlineData("!theme plain\n!option handwritten true")]
    [InlineData("@startuml\nobject Sample {\nValue = demo\n@enduml")]
    [InlineData("@startuml\nclass Order\nmysterybox Unknown\n@enduml")]
    public void EmptyMalformedAndPartiallyUnsupportedSources_FailDuringImport(string source)
    {
        // Extend with exact code/line assertions after diagnostic API ratification.
        var error = Assert.Throws<PlantUmlImportException>(() => Import(source));
        Assert.DoesNotContain("aim-node", error.Message);
    }

    [Fact]
    public void UseCases_AreNotReplacedByImplicitClasses()
    {
        // Delivery B: baseline regression for a source that can silently misimport.
        var manifest = Import("""
            @startuml Sample
            actor "Operator Seven" as operator7
            usecase "Open crate" as open
            usecase "Check seal" as check
            operator7 --> open
            open ..> check : <<include>>
            @enduml
            """);
        Assert.Equal(DiagramKind.UseCase, manifest.Diagram.Kind);
        Assert.Equal(3, manifest.Projection.Elements.Count);
        Assert.Equal(2, manifest.Projection.Elements.Count(e => e.Kind == DiagramElementKinds.UseCase));
        Assert.Single(manifest.Projection.Elements, e => e.Kind == DiagramElementKinds.Role);
        Assert.DoesNotContain(manifest.Projection.Elements, e => e.Kind == DiagramElementKinds.Class);
        Assert.Contains(manifest.Projection.Relationships, e => e.Kind == DiagramRelationshipKinds.Include);
    }
}
