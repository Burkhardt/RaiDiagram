using System.Globalization;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace RaiDiagram;

public sealed record XmiDiagramInfo(string Id, string Name, string Type);

/// <summary>
/// Reads Poseidon/OTW XMI 1.2 UML 1.4 deployment diagrams. XML is scanned forward;
/// only the selected diagram's drawing subtree and referenced semantic records are retained.
/// No external entities are resolved. Imported geometry never passes through a layout engine.
/// </summary>
public sealed class XmiDeploymentImporter
{
	private static XmlReader Open(string path) => XmlReader.Create(path, new XmlReaderSettings
	{
		DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true
	});

	public IReadOnlyList<XmiDiagramInfo> ListDiagrams(string path)
	{
		using var reader = Open(path);
		var result = new List<XmiDiagramInfo>();
		string? id = null;
		var name = string.Empty;
		var type = string.Empty;
		var depth = -1;
		while (reader.Read())
		{
			if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Diagram" && reader.GetAttribute("xmi.id") is { } nextId)
			{
				id = nextId; name = reader.GetAttribute("name") ?? nextId;
				type = reader.GetAttribute("typeInfo") ?? string.Empty; depth = reader.Depth;
			}
			else if (id is not null && reader.NodeType == XmlNodeType.Element && reader.Depth == depth + 2
				&& reader.LocalName == "SimpleSemanticModelElement") type = reader.GetAttribute("typeInfo") ?? type;
			else if (id is not null && reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
			{
				result.Add(new(id, name, type)); id = null;
			}
		}
		return result;
	}

	public RaidDiagramModel Import(string path, string? diagram = null)
	{
		var diagrams = ListDiagrams(path);
		var matches = diagrams.Where(d => diagram is null ? d.Type == "DeploymentDiagram" : d.Id == diagram || d.Name == diagram).ToArray();
		if (matches.Length != 1)
			throw new RaidSchemaException("XMI001: Select exactly one deployment diagram with --diagram <id|name>; use --list-diagrams to inspect choices.");
		var selected = matches[0];
		if (selected.Type != "DeploymentDiagram") throw new RaidSchemaException($"XMI002: Unsupported diagram type '{selected.Type}'. Expected DeploymentDiagram.");
		XElement? drawing = null;
		using (var reader = Open(path))
			while (reader.Read())
				if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Diagram" && reader.GetAttribute("xmi.id") == selected.Id)
				{
					using var subtree = reader.ReadSubtree();
					drawing = XElement.Load(subtree, LoadOptions.SetLineInfo);
					break;
				}
		if (drawing is null) throw new RaidSchemaException("XMI001: The selected diagram is no longer present.");
		var graphNodes = drawing.Descendants().Where(e => e.Name.LocalName == "GraphNode" && Id(e) is not null && Semantic(e) is { } semantic
			&& semantic.Name.LocalName is "Node" or "Component").ToArray();
		var graphEdges = drawing.Descendants().Where(e => e.Name.LocalName == "GraphEdge" && Id(e) is not null && Semantic(e) is not null).ToArray();
		var unsupported = drawing.Descendants().Where(e => e.Name.LocalName is "GraphNode" or "GraphEdge" && Id(e) is not null)
			.Select(Semantic).FirstOrDefault(e => e is not null && e.Name.LocalName is not ("Node" or "Component" or "Dependency" or "Stereotype"));
		if (unsupported is not null) throw new RaidSchemaException($"XMI002: Unsupported visible semantic element '{unsupported.Name.LocalName}'.");
		if (graphNodes.Length == 0) throw new RaidSchemaException("XMI003: The selected diagram contains no visible host or component nodes.");
		var needed = graphNodes.Concat(graphEdges).Select(e => Ref(Semantic(e)!)!).ToHashSet(StringComparer.Ordinal);
		var semantics = ReadSemantics(path, needed);
		using var stream = File.OpenRead(path);
		var revision = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
		var manifest = new DiagramManifest
		{
			SchemaVersion = DiagramManifest.ExtendedSchemaVersion,
			Diagram = new() { Id = selected.Id, Title = selected.Name, Kind = DiagramKind.Deployment },
			Model = new() { ProviderScheme = "xmi", ModelId = selected.Id, CapturedRevision = "sha256:" + revision },
			Presentation = new() { Canvas = new() { Routing = "authored" } }
		};
		var canvas = manifest.Presentation.Canvas!;
		var visible = graphNodes.Select(n => Ref(Semantic(n)!)!).ToHashSet(StringComparer.Ordinal);
		foreach (var id in visible.Order(StringComparer.Ordinal))
		{
			if (!semantics.TryGetValue(id, out var item)) throw new RaidSchemaException($"XMI004: Missing semantic definition '{id}'.");
			var element = new DiagramElement { Id = id, DisplayName = item.Name, Kind = item.Kind,
				ParentId = item.Parent is not null && visible.Contains(item.Parent) ? item.Parent : null };
			foreach (var stereotype in item.Stereotypes.Order(StringComparer.Ordinal))
				element.RelevantFacts["uml.stereotype." + stereotype] = ModelFactValue.String(semantics.TryGetValue(stereotype, out var definition) ? definition.Name : stereotype);
			manifest.Projection.Elements.Add(element);
		}
		var nodeIds = graphNodes.Select(n => Id(n)!).ToHashSet(StringComparer.Ordinal);
		foreach (var graph in graphNodes)
		{
			var parent = graph.Ancestors().FirstOrDefault(n => n.Name.LocalName == "GraphNode" && Id(n) is { } pid && nodeIds.Contains(pid));
			var (x, y) = Pair(Child(graph, "GraphElement.position"), "node position");
			// Positions may be relative to intervening nonsemantic compartments.
			foreach (var intermediary in graph.Ancestors().TakeWhile(n => n != parent && n != drawing).Where(n => n.Name.LocalName == "GraphNode"))
			{
				var (dx, dy) = Pair(Child(intermediary, "GraphElement.position"), "compartment position"); x += dx; y += dy;
			}
			var (width, height) = Pair(Child(graph, "GraphNode.size"), "node size");
			canvas.Nodes.Add(new() { ViewId = Id(graph), ElementId = Ref(Semantic(graph)!)!, ParentViewId = parent is null ? null : Id(parent),
				Archetype = "obj", X = x, Y = y, Width = width, Height = height });
		}
		var connectors = drawing.Descendants().Where(e => e.Name.LocalName == "GraphConnector" && Id(e) is not null)
			.ToDictionary(e => Id(e)!, e => e.Ancestors().FirstOrDefault(n => n.Name.LocalName == "GraphNode" && Id(n) is { } id && nodeIds.Contains(id)), StringComparer.Ordinal);
		foreach (var graph in graphEdges)
		{
			var reference = Ref(Semantic(graph)!)!;
			if (!semantics.TryGetValue(reference, out var relation) || relation.Source is null || relation.Target is null)
				throw new RaidSchemaException($"XMI004: Incomplete relationship '{reference}'.");
			if (!visible.Contains(relation.Source) || !visible.Contains(relation.Target)) throw new RaidSchemaException("XMI004: Relationship endpoints are not visible in the selected diagram.");
			if (!manifest.Projection.Relationships.Any(r => r.Id == reference))
				manifest.Projection.Relationships.Add(new() { Id = reference, Kind = DiagramRelationshipKinds.Dependency, SourceId = relation.Source, TargetId = relation.Target,
					Label = relation.Stereotypes.Count > 0 ? "<<" + string.Join(", ", relation.Stereotypes.Select(id => semantics.TryGetValue(id, out var st) ? st.Name : id)) + ">>" : string.IsNullOrWhiteSpace(relation.Name) ? null : relation.Name });
			var anchorNodes = Child(graph, "GraphEdge.anchor")?.Elements().Select(Ref).Where(id => id is not null)
				.Select(id => connectors.GetValueOrDefault(id!)).Where(n => n is not null).ToArray() ?? [];
			string Endpoint(string modelId)
			{
				var anchored = anchorNodes.Where(n => Ref(Semantic(n!)!) == modelId).Distinct().ToArray();
				if (anchored.Length == 1) return Id(anchored[0]!)!;
				var candidates = canvas.Nodes.Where(n => n.ElementId == modelId).ToArray();
				if (candidates.Length == 1) return candidates[0].Identity;
				throw new RaidSchemaException("XMI004: Ambiguous repeated endpoint; a drawing anchor is required.");
			}
			var points = Child(graph, "GraphEdge.waypoints")?.Elements().Select(e =>
			{
				var (x, y) = Pair(e, "edge waypoint"); return new DiagramCanvasPoint { X = x, Y = y };
			}).ToList() ?? [];
			if (points.Count < 2) throw new RaidSchemaException("XMI005: An authored edge requires at least two waypoints.");
			canvas.Edges.Add(new() { ViewId = Id(graph), RelationshipId = reference, SourceViewId = Endpoint(relation.Source), TargetViewId = Endpoint(relation.Target), Routing = "authored", Waypoints = points });
		}
		return RaidDiagramModel.FromManifest(manifest);
	}

	private sealed class SemanticRecord(string kind, string name)
	{
		internal string Kind = kind, Name = name;
		internal string? Source, Target, Parent;
		internal List<string> Stereotypes = [];
	}

	private static Dictionary<string, SemanticRecord> ReadSemantics(string path, HashSet<string> needed)
	{
		var records = new Dictionary<string, SemanticRecord>(StringComparer.Ordinal);
		var stack = new Stack<(int Depth, string LocalName, string? Owner)>();
		var residences = new Dictionary<string, string>(StringComparer.Ordinal);
		using var reader = Open(path);
		while (reader.Read())
		{
			if (reader.NodeType != XmlNodeType.Element) continue;
			while (stack.TryPeek(out var old) && old.Depth >= reader.Depth) stack.Pop();
			var owner = stack.TryPeek(out var parent) ? parent.Owner : null;
			var local = reader.LocalName;
			var id = reader.GetAttribute("xmi.id");
			if (id is not null && local is "Node" or "Component" or "Dependency" or "Stereotype")
			{
				owner = id;
				if (needed.Contains(id) || local == "Stereotype") records[id] = new(local, reader.GetAttribute("name") ?? id);
			}
			if (reader.GetAttribute("xmi.idref") is { } reference && owner is not null)
			{
				var property = stack.TryPeek(out var wrapper) ? wrapper.LocalName : string.Empty;
				if (records.TryGetValue(owner, out var record))
				{
					if (property == "Dependency.client") record.Source = reference;
					if (property == "Dependency.supplier") record.Target = reference;
					if (property == "ModelElement.stereotype") record.Stereotypes.Add(reference);
					if (property == "Component.deploymentLocation") record.Parent = reference;
				}
				if (needed.Contains(reference) && property is "Node.deployedComponent" or "ElementResidence.resident") residences[reference] = owner;
			}
			if (!reader.IsEmptyElement) stack.Push((reader.Depth, local, owner));
		}
		foreach (var (child, parent) in residences)
			if (records.TryGetValue(child, out var record)) record.Parent = parent;
		return records;
	}

	private static string? Id(XElement element) => (string?)element.Attribute("xmi.id");
	private static string? Ref(XElement element) => (string?)element.Attribute("xmi.idref");
	private static XElement? Child(XElement element, string name) => element.Elements().FirstOrDefault(e => e.Name.LocalName == name);
	private static XElement? Semantic(XElement graph) => Child(graph, "GraphElement.semanticModel")?.Elements()
		.FirstOrDefault(e => e.Name.LocalName == "Uml1SemanticModelBridge")?.Elements()
		.FirstOrDefault(e => e.Name.LocalName == "Uml1SemanticModelBridge.element")?.Elements().FirstOrDefault();
	private static (decimal X, decimal Y) Pair(XElement? element, string context)
	{
		var fields = element?.Elements().Where(e => e.Name.LocalName == "XMI.field").ToArray() ?? [];
		if (fields.Length != 2 || !decimal.TryParse(fields[0].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
			|| !decimal.TryParse(fields[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
			throw new RaidSchemaException($"XMI005: Missing or invalid {context}.");
		return (x, y);
	}
}
