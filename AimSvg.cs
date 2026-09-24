using System.Globalization;
using System.Xml.Linq;

namespace RaiDiagram;

/// <summary>Emits and validates the public <c>aim-*</c> SVG hydration contract.</summary>
public static class AimSvg
{
	private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

	public static string Emit(DiagramManifest manifest)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		manifest.Validate();
		var canvas = manifest.Presentation.Canvas
			?? throw new RaidSchemaException("The diagram has no canvas presentation to emit.");
		var nodes = canvas.Nodes.ToDictionary(item => item.ElementId, StringComparer.Ordinal);
		var width = nodes.Count == 0 ? 1 : nodes.Values.Max(item => item.X + item.Width) + 40;
		var height = nodes.Count == 0 ? 1 : nodes.Values.Max(item => item.Y + item.Height) + 40;

		var root = new XElement(Svg + "svg",
			new XAttribute("viewBox", $"0 0 {Number(width)} {Number(height)}"),
			new XAttribute("id", manifest.Diagram.Id),
			new XAttribute("aim-archetype", manifest.Diagram.Kind.ToString().ToLowerInvariant()),
			new XAttribute("aim-routing", canvas.Routing),
			new XAttribute("aim-show-expressions", "true"));

		var definitions = new XElement(Svg + "defs",
			new XElement(Svg + "marker",
				new XAttribute("id", "aim-arrow"),
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

		var edgeLayer = new XElement(Svg + "g", new XAttribute("class", "aim-edges-layer"));
		foreach (var edgeLayout in canvas.Edges)
		{
			var relationship = manifest.Projection.Relationships.Single(
				item => item.Id == edgeLayout.RelationshipId);
			if (!nodes.TryGetValue(relationship.SourceId, out var source)
				|| !nodes.TryGetValue(relationship.TargetId, out var target))
				continue;
			var startX = source.X + source.Width / 2;
			var startY = source.Y + source.Height;
			var endX = target.X + target.Width / 2;
			var endY = target.Y;
			var path = $"M {Number(startX)} {Number(startY)} L {Number(endX)} {Number(endY)}";
			var group = new XElement(Svg + "g",
				new XAttribute("class", "aim-edge"),
				new XAttribute("aim-edge", "true"),
				new XAttribute("aim-id", relationship.Id),
				new XAttribute("aim-edge-kind", ToAimEdgeKind(relationship.Kind)),
				new XAttribute("aim-source", relationship.SourceId),
				new XAttribute("aim-target", relationship.TargetId),
				new XAttribute("aim-routing", edgeLayout.Routing),
				new XElement(Svg + "path",
					new XAttribute("class", "aim-edge-path"),
					new XAttribute("d", path),
					new XAttribute("fill", "none"),
					new XAttribute("stroke", "#4b5563"),
					new XAttribute("stroke-width", "2"),
					new XAttribute("marker-end", "url(#aim-arrow)")));
			var label = relationship.Label ?? relationship.Guard;
			if (!string.IsNullOrWhiteSpace(label))
				group.Add(new XElement(Svg + "text",
					new XAttribute("x", Number((startX + endX) / 2 + 5)),
					new XAttribute("y", Number((startY + endY) / 2 - 5)),
					new XAttribute("font-size", "12"),
					label));
			edgeLayer.Add(group);
		}
		root.Add(edgeLayer);

		var nodeLayer = new XElement(Svg + "g", new XAttribute("class", "aim-nodes-layer"));
		foreach (var node in canvas.Nodes)
		{
			var element = manifest.Projection.Elements.Single(item => item.Id == node.ElementId);
			var group = new XElement(Svg + "g",
				new XAttribute("class", $"aim-node aim-{node.Archetype}"),
				new XAttribute("aim-node", "true"),
				new XAttribute($"aim-{node.Archetype}", "true"),
				new XAttribute("aim-id", element.Id),
				new XAttribute("aim-kind", node.Archetype),
				new XAttribute("aim-display-name", element.DisplayName),
				new XAttribute("transform", $"translate({Number(node.X)},{Number(node.Y)})"));

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
			group.Add(new XElement(Svg + "text",
				new XAttribute("x", Number(node.Width / 2)),
				new XAttribute("y", Number(node.Height / 2 + 5)),
				new XAttribute("text-anchor", "middle"),
				new XAttribute("font-family", "sans-serif"),
				new XAttribute("font-size", "14"),
				element.DisplayName));
			nodeLayer.Add(group);
		}
		root.Add(nodeLayer);

		var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
		var svg = document.ToString(SaveOptions.DisableFormatting);
		Validate(svg);
		return svg;
	}

	public static void Validate(string svg)
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
			if (kind is not ("act" or "cls" or "obj"))
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
		=> value.ToString("0.###", CultureInfo.InvariantCulture);

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
