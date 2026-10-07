using System.Xml.Linq;

namespace RaiDiagram.Tests;

public sealed class XmiDeploymentImportTests
{
	[Fact]
	public void RepeatedViewsRelativeBoundsResidencyAndExactWaypointsSurviveRoundTrip()
	{
		var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xmi");
		try
		{
			File.WriteAllText(path, Fixture());
			var importer = new XmiDeploymentImporter();
			Assert.Equal("DeploymentDiagram", Assert.Single(importer.ListDiagrams(path)).Type);
			var manifest = importer.Import(path).Manifest;
			Assert.Equal(2, manifest.Projection.Elements.Count);
			Assert.Equal("host", manifest.Projection.Elements.Single(e => e.Id == "component").ParentId);
			var canvas = manifest.Presentation.Canvas!;
			Assert.Equal(3, canvas.Nodes.Count);
			Assert.Equal("host-view", canvas.Nodes[1].ParentViewId);
			Assert.Equal(-10.125m, canvas.Nodes[0].X);
			Assert.Equal(40.123456m, canvas.Nodes[1].X);
			Assert.Equal("component-second", Assert.Single(canvas.Edges).SourceViewId);
			var derived = new DiagramArtifactManager().Derive(manifest);
			Assert.Contains("M 700.123456 250 L 650 150 L 260 150", derived.Svg);
			Assert.Contains("translate(29.998456,140)", derived.Svg);
			Assert.Contains("aim-model-id=\"component\"", derived.Svg);
			Assert.Equal(derived.Svg, new DiagramArtifactManager().Derive(RaidJson5.Parse(derived.Raid)).Svg);
			Assert.Equal(derived.Raid, new DiagramArtifactManager().Derive(importer.Import(path).Manifest).Raid);
			Assert.Contains("component \"OrderService\"", derived.PlantUml);
		}
		finally { File.Delete(path); }
	}

	[Fact]
	public void ExternalEntitiesAreRejected()
	{
		var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xmi");
		try
		{
			File.WriteAllText(path, "<!DOCTYPE XMI [<!ENTITY value SYSTEM 'file:///does-not-exist'>]><XMI>&value;</XMI>");
			Assert.Throws<System.Xml.XmlException>(() => new XmiDeploymentImporter().ListDiagrams(path));
		}
		finally { File.Delete(path); }
	}

	private static string Fixture()
	{
		XNamespace uml = "org.omg.xmi.namespace.UML";
		XElement U(string name, params object[] content) => new(uml + name, content);
		XAttribute Id(string id) => new("xmi.id", id);
		XElement Ref(string kind, string id) => U(kind, new XAttribute("xmi.idref", id));
		XElement Pair(string name, string x, string y) => U(name, new XElement("XMI.field", x), new XElement("XMI.field", y));
		XElement Semantic(string kind, string id) => U("GraphElement.semanticModel", U("Uml1SemanticModelBridge", U("Uml1SemanticModelBridge.element", Ref(kind, id))));
		XElement Node(string id, string kind, string semantic, string x, string y, params object[] content) => U("GraphNode", Id(id),
			Pair("GraphElement.position", x, y), Pair("GraphNode.size", "300", "200"), Semantic(kind, semantic), content);
		var graph = U("Diagram", Id("deployment"), new XAttribute("name", "Server Overview"),
			U("GraphElement.semanticModel", U("SimpleSemanticModelElement", new XAttribute("typeInfo", "DeploymentDiagram"))),
			U("GraphElement.contained",
				Node("host-view", "Node", "host", "-10.125", "100",
					U("GraphElement.contained", Node("component-first", "Component", "component", "40.123456", "40")),
					U("GraphElement.anchorage", U("GraphConnector", Id("target-anchor")))),
				Node("component-second", "Component", "component", "700", "200",
					U("GraphElement.anchorage", U("GraphConnector", Id("source-anchor")))),
				U("GraphEdge", Id("edge-view"), Semantic("Dependency", "dependency"),
					U("GraphEdge.anchor", Ref("GraphConnector", "source-anchor"), Ref("GraphConnector", "target-anchor")),
					U("GraphEdge.waypoints", Pair("XMI.field", "700.123456", "250"), Pair("XMI.field", "650", "150"), Pair("XMI.field", "260", "150")))));
		return new XElement("XMI", new XAttribute("xmi.version", "1.2"),
			U("Node", Id("host"), new XAttribute("name", "ServerNode"), U("Node.deployedComponent", Ref("Component", "component"))),
			U("Component", Id("component"), new XAttribute("name", "OrderService")),
			U("Dependency", Id("dependency"), U("Dependency.client", Ref("Component", "component")), U("Dependency.supplier", Ref("Node", "host"))), graph).ToString();
	}
}
