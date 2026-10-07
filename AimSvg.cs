using System.Globalization;
using System.Xml.Linq;

namespace RaiDiagram;

public enum AimSvgProfile
{
	Hydratable,
	Plain
}

/// <summary>Emits and validates the public <c>aim-*</c> SVG hydration contract.</summary>
public static class AimSvg
{
	private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

	public static string Emit(DiagramManifest manifest)
		=> Emit(manifest, AimSvgProfile.Hydratable);

	public static string Emit(DiagramManifest manifest, AimSvgProfile profile)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		manifest.Validate();
		var canvas = manifest.Presentation.Canvas
			?? throw new RaidSchemaException("The diagram has no canvas presentation to emit.");
		var nodes = canvas.Nodes.ToDictionary(item => item.Identity, StringComparer.Ordinal);
		var positions = new Dictionary<string, (decimal X, decimal Y)>(StringComparer.Ordinal);
		(decimal X, decimal Y) Position(DiagramCanvasNode node)
		{
			if (positions.TryGetValue(node.Identity, out var cached)) return cached;
			var parent = node.ParentViewId is null ? (X: 0m, Y: 0m) : Position(nodes[node.ParentViewId]);
			return positions[node.Identity] = (node.X + parent.X, node.Y + parent.Y);
		}
		foreach (var node in canvas.Nodes) Position(node);
		var minX = Math.Min(0, nodes.Count == 0 ? 0 : positions.Values.Min(p => p.X) - 40);
		var minY = Math.Min(0, nodes.Count == 0 ? 0 : positions.Values.Min(p => p.Y) - 40);
		var width = nodes.Count == 0 ? 1 : nodes.Values.Max(item => Position(item).X + item.Width) + 40;
		var height = nodes.Count == 0 ? 1 : nodes.Values.Max(item => Position(item).Y + item.Height) + 40;

		var hydratable = profile == AimSvgProfile.Hydratable;
		var root = new XElement(Svg + "svg",
			new XAttribute("viewBox", $"{Number(minX)} {Number(minY)} {Number(width - minX)} {Number(height - minY)}"),
			new XAttribute("id", manifest.Diagram.Id));
		if (hydratable)
		{
			root.Add(
				new XAttribute("aim-archetype", manifest.Diagram.Kind.ToString().ToLowerInvariant()),
				new XAttribute("aim-routing", canvas.Routing),
				new XAttribute("aim-show-expressions", "true"));
		}

		var definitions = new XElement(Svg + "defs",
			new XElement(Svg + "marker",
				new XAttribute("id", hydratable ? "aim-arrow" : "diagram-arrow"),
				new XAttribute("markerWidth", "10"),
				new XAttribute("markerHeight", "10"),
				new XAttribute("refX", "9"),
				new XAttribute("refY", "3"),
				new XAttribute("orient", "auto"),
				new XAttribute("markerUnits", "strokeWidth"),
				new XElement(Svg + "path",
					new XAttribute("d", "M0,0 L0,6 L9,3 z"),
					new XAttribute("fill", "#4b5563"))));
		root.Add(definitions);

		var edgeLayer = new XElement(Svg + "g", new XAttribute("class", hydratable ? "aim-edges-layer" : "edges-layer"));
		foreach (var edgeLayout in canvas.Edges)
		{
			var relationship = manifest.Projection.Relationships.Single(
				item => item.Id == edgeLayout.RelationshipId);
			if (!nodes.TryGetValue(edgeLayout.SourceViewId ?? relationship.SourceId, out var source)
				|| !nodes.TryGetValue(edgeLayout.TargetViewId ?? relationship.TargetId, out var target))
				continue;
			var startX = Position(source).X + source.Width / 2;
			var startY = Position(source).Y + source.Height;
			var endX = Position(target).X + target.Width / 2;
			var endY = Position(target).Y;
			var points = edgeLayout.Waypoints.Count > 0 ? edgeLayout.Waypoints :
				new List<DiagramCanvasPoint> { new() { X = startX, Y = startY } }
				.Concat(edgeLayout.BendPoints).Append(new() { X = endX, Y = endY }).ToList();
			startX = points[0].X; startY = points[0].Y; endX = points[^1].X; endY = points[^1].Y;
			var path = string.Join(" ", points.Select((point, i) => $"{(i == 0 ? "M" : "L")} {Number(point.X)} {Number(point.Y)}"));
			var group = new XElement(Svg + "g",
				new XAttribute("class", hydratable ? "aim-edge" : "edge"),
				new XElement(Svg + "path",
					new XAttribute("class", hydratable ? "aim-edge-path" : "edge-path"),
					new XAttribute("d", path),
					new XAttribute("fill", "none"),
					new XAttribute("stroke", "#4b5563"),
					new XAttribute("stroke-width", "2"),
					new XAttribute("marker-end", relationship.Kind == DiagramRelationshipKinds.Association && !relationship.Directed ? "none" : hydratable ? "url(#aim-arrow)" : "url(#diagram-arrow)")));
			if (hydratable)
			{
				group.Add(
					new XAttribute("aim-edge", "true"),
					new XAttribute("aim-id", edgeLayout.Identity),
					new XAttribute("aim-model-id", relationship.Id),
					new XAttribute("aim-edge-kind", ToAimEdgeKind(relationship.Kind)),
					new XAttribute("aim-source", source.Identity),
					new XAttribute("aim-target", target.Identity),
					new XAttribute("aim-source-port", "port-bottom"),
					new XAttribute("aim-target-port", "port-top"),
					new XAttribute("aim-routing", edgeLayout.Routing));
				if (edgeLayout.BendPoints.Count > 0)
					group.Add(new XAttribute("aim-bends", string.Join(' ', edgeLayout.BendPoints
						.Select(point => $"{Number(point.X)},{Number(point.Y)}"))));
				if (!string.IsNullOrWhiteSpace(relationship.Guard))
					group.Add(new XAttribute("aim-expression", relationship.Guard));
			}
			var label = relationship.Label ?? relationship.Guard;
			if (!string.IsNullOrWhiteSpace(label))
				group.Add(new XElement(Svg + "text",
					new XAttribute("x", Number((startX + endX) / 2 + 5)),
					new XAttribute("y", Number((startY + endY) / 2 - 5)),
					new XAttribute("font-size", "12"),
					label));
			edgeLayer.Add(group);
		}


		var nodeLayer = new XElement(Svg + "g", new XAttribute("class", hydratable ? "aim-nodes-layer" : "nodes-layer"));
		foreach (var node in canvas.Nodes)
		{
			var element = manifest.Projection.Elements.Single(item => item.Id == node.ElementId);
			var group = new XElement(Svg + "g",
				new XAttribute("class", hydratable ? $"aim-node aim-{node.Archetype}" : $"node {node.Archetype}"),
				new XAttribute("transform", $"translate({Number(Position(node).X)},{Number(Position(node).Y)})"));
			if (hydratable)
				group.Add(
					new XAttribute("aim-node", "true"),
					new XAttribute($"aim-{node.Archetype}", "true"),
					new XAttribute("aim-id", node.Identity),
					new XAttribute("aim-model-id", element.Id),
					new XAttribute("aim-kind", node.Archetype),
					new XAttribute("aim-display-name", element.DisplayName));

			if (element.Kind == DiagramElementKinds.Decision)
			{
				var halfWidth = node.Width / 2;
				var halfHeight = node.Height / 2;
				group.Add(new XElement(Svg + "polygon",
					new XAttribute("points",
						$"{Number(halfWidth)},0 {Number(node.Width)},{Number(halfHeight)} " +
						$"{Number(halfWidth)},{Number(node.Height)} 0,{Number(halfHeight)}"),
					new XAttribute("fill", "#fef3c7"),
					new XAttribute("stroke", "#4b5563")));
			}
			else
			{
				group.Add(new XElement(Svg + "rect",
					new XAttribute("x", "0"),
					new XAttribute("y", "0"),
					new XAttribute("width", Number(node.Width)),
					new XAttribute("height", Number(node.Height)),
					new XAttribute("rx", node.Archetype == "act" ? "14" : "4"),
					new XAttribute("fill", node.Archetype == "cls" ? "#eef2ff" : "#ecfeff"),
					new XAttribute("stroke", "#334155")));
			}
			var objectSlots = element.ObjectProperties;
			group.Add(new XElement(Svg + "text",
				new XAttribute("x", Number(node.Width / 2)),
				new XAttribute("y", Number(objectSlots.Count > 0 || DiagramManifest.IsDeploymentContainer(element.Kind) ? 22 : node.Height / 2 + 5)),
				new XAttribute("text-anchor", "middle"),
				new XAttribute("font-family", "sans-serif"),
				new XAttribute("font-size", "14"),
				element.Kind == DiagramElementKinds.Object ? ObjectLabel(element.DisplayName) : element.DisplayName));
			if (element.Kind == DiagramElementKinds.Object)
				group.Elements(Svg + "text").Last().SetAttributeValue("text-decoration", "underline");
			if (objectSlots.Count > 0)
			{
				group.Add(new XElement(Svg + "line", new XAttribute("x1", "0"), new XAttribute("y1", "32"),
					new XAttribute("x2", Number(node.Width)), new XAttribute("y2", "32"), new XAttribute("stroke", "#334155")));
				for (var i = 0; i < objectSlots.Count; i++)
					group.Add(new XElement(Svg + "text", new XAttribute("x", "8"), new XAttribute("y", 50 + i * 20),
						new XAttribute("font-family", "sans-serif"), new XAttribute("font-size", "12"),
						$"{objectSlots[i].Name} = {objectSlots[i].Value}"));
			}
			nodeLayer.Add(group);
		}
		root.Add(nodeLayer);
		root.Add(edgeLayer);

		var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
		var svg = document.ToString(SaveOptions.DisableFormatting);
		Validate(svg, profile);
		return svg;
	}

	private static string ObjectLabel(string text) => text.Replace("<u>", string.Empty, StringComparison.Ordinal)
		.Replace("</u>", string.Empty, StringComparison.Ordinal);

	public static void Validate(string svg)
		=> Validate(svg, AimSvgProfile.Hydratable);

	public static void Validate(string svg, AimSvgProfile profile)
	{
		if (string.IsNullOrWhiteSpace(svg))
			throw new RaidSchemaException("An SVG document cannot be empty.");
		XDocument document;
		try
		{
			document = XDocument.Parse(svg, LoadOptions.SetLineInfo);
		}
		catch (Exception exception)
		{
			throw new RaidSchemaException("The SVG is not well-formed XML.", exception);
		}

		var root = document.Root;
		if (root?.Name != Svg + "svg")
			throw new RaidSchemaException("The aim SVG requires the SVG namespace on its root element.");
		if (profile == AimSvgProfile.Plain)
		{
			var hydrationAttribute = root.DescendantsAndSelf()
				.Attributes()
				.FirstOrDefault(item => item.Name.LocalName.StartsWith("aim-", StringComparison.Ordinal));
			if (hydrationAttribute is not null)
				throw new RaidSchemaException(
					$"A plain SVG cannot contain hydration attribute '{hydrationAttribute.Name.LocalName}'.");
			if (!root.Descendants().Any(item => item.Name == Svg + "rect" || item.Name == Svg + "polygon"))
				throw new RaidSchemaException("The plain SVG contains no visible diagram nodes.");
			return;
		}
		var nodes = root.Descendants(Svg + "g")
			.Where(item => string.Equals((string?)item.Attribute("aim-node"), "true", StringComparison.Ordinal))
			.ToList();
		if (nodes.Count == 0)
			throw new RaidSchemaException("The aim SVG contains no hydratable aim-node groups.");
		var nodeIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var node in nodes)
		{
			var id = RequiredAttribute(node, "aim-id");
			if (!nodeIds.Add(id))
				throw new RaidSchemaException($"The aim SVG contains duplicate node id '{id}'.");
			var kind = RequiredAttribute(node, "aim-kind");
			if (kind is not ("act" or "uc" or "cls" or "obj" or "per" or "plc" or "rol" or "rf"))
				throw new RaidSchemaException($"The aim SVG node '{id}' has unsupported kind '{kind}'.");
			var transform = RequiredAttribute(node, "transform");
			if (transform.Contains("NaN", StringComparison.OrdinalIgnoreCase)
				|| transform.Contains("Infinity", StringComparison.OrdinalIgnoreCase))
				throw new RaidSchemaException($"The aim SVG node '{id}' has non-finite coordinates.");
		}

		foreach (var edge in root.Descendants(Svg + "g")
			.Where(item => string.Equals((string?)item.Attribute("aim-edge"), "true", StringComparison.Ordinal)))
		{
			var id = RequiredAttribute(edge, "aim-id");
			var source = RequiredAttribute(edge, "aim-source");
			var target = RequiredAttribute(edge, "aim-target");
			if (!nodeIds.Contains(source) || !nodeIds.Contains(target))
				throw new RaidSchemaException(
					$"The aim SVG edge '{id}' refers to missing endpoint '{source}' or '{target}'.");
			var path = edge.Descendants(Svg + "path").FirstOrDefault()?.Attribute("d")?.Value;
			if (string.IsNullOrWhiteSpace(path)
				|| path.Contains("NaN", StringComparison.OrdinalIgnoreCase)
				|| path.Contains("Infinity", StringComparison.OrdinalIgnoreCase))
				throw new RaidSchemaException($"The aim SVG edge '{id}' has invalid path geometry.");
		}
	}

	private static string RequiredAttribute(XElement element, string name)
		=> element.Attribute(name)?.Value is { Length: > 0 } value
			? value
			: throw new RaidSchemaException($"The aim SVG element is missing '{name}'.");

	private static string Number(decimal value)
		=> value.ToString("0.############################", CultureInfo.InvariantCulture);

	private static string ToAimEdgeKind(string relationshipKind) => relationshipKind switch
	{
		DiagramRelationshipKinds.Dependency => "dependency",
		DiagramRelationshipKinds.Generalization => "generalization",
		DiagramRelationshipKinds.Realization => "realization",
		DiagramRelationshipKinds.Containment => "composition",
		DiagramRelationshipKinds.Aggregation => "aggregation",
		_ => "association"
	};
}
