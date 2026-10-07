namespace RaiDiagram;

/// <summary>Creates stable default canvas geometry when a manifest has no authored canvas.</summary>
public static class DeterministicDiagramCanvas
{
	public static DiagramManifest Ensure(DiagramManifest manifest)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		manifest.Validate();
		if (manifest.Presentation.Canvas is not null)
			return manifest;

		var canvas = new DiagramCanvasPresentation();
		var classLike = manifest.Diagram.Kind is DiagramKind.Class or DiagramKind.Object or DiagramKind.Mixed or DiagramKind.Deployment;
		var children = manifest.Projection.Elements.Where(e => e.ParentId is not null).ToLookup(e => e.ParentId!);
		var sizes = new Dictionary<string, (decimal Width, decimal Height)>(StringComparer.Ordinal);
		(decimal Width, decimal Height) Measure(DiagramElement element)
		{
			var width = Math.Max(classLike ? 220 : 260, 24 + element.DisplayName.Length * 8);
			foreach (var slot in element.ObjectProperties) width = Math.Max(width, 24 + (slot.Name.Length + slot.Value.Length + 3) * 8);
			var height = (decimal)Math.Max(classLike ? 90 : 70, 44 + element.ObjectProperties.Count * 20);
			var nested = children[element.Id].Select(Measure).ToArray();
			if (nested.Length > 0)
			{
				width = Math.Max(width, (int)nested.Max(n => n.Width) + 64);
				height = Math.Max(height, 48 + nested.Sum(n => n.Height + 24));
			}
			return sizes[element.Id] = (width, height);
		}
		void Place(DiagramElement element, decimal x, decimal y)
		{
			var size = sizes[element.Id];
			canvas.Nodes.Add(new() { ElementId = element.Id, Archetype = ToArchetype(element.Kind), X = x, Y = y, Width = size.Width, Height = size.Height });
			var childY = y + 48;
			foreach (var child in children[element.Id])
			{
				Place(child, x + 32, childY);
				childY += sizes[child.Id].Height + 24;
			}
		}
		var roots = manifest.Projection.Elements.Where(e => e.ParentId is null).ToArray();
		foreach (var root in roots) Measure(root);
		decimal rowY = 40, columnX = 40, rowHeight = 0;
		for (var index = 0; index < roots.Length; index++)
		{
			if (index > 0 && index % (classLike ? 3 : 1) == 0) { rowY += rowHeight + 40; columnX = 40; rowHeight = 0; }
			Place(roots[index], columnX, rowY);
			columnX += sizes[roots[index].Id].Width + 40;
			rowHeight = Math.Max(rowHeight, sizes[roots[index].Id].Height);
		}
		foreach (var relationship in manifest.Projection.Relationships)
			canvas.Edges.Add(new DiagramCanvasEdge { RelationshipId = relationship.Id });
		manifest.Presentation.Canvas = canvas;
		return manifest;
	}

	public static string ToArchetype(string elementKind) => elementKind switch
	{
		DiagramElementKinds.Role => "rol",
		DiagramElementKinds.UseCase => "uc",
		DiagramElementKinds.Activity or DiagramElementKinds.Decision or DiagramElementKinds.State => "act",
		DiagramElementKinds.Class or DiagramElementKinds.Interface or DiagramElementKinds.Enumeration => "cls",
		DiagramElementKinds.Object or DiagramElementKinds.ObjectNode or DiagramElementKinds.Lifeline => "obj",
		_ => "obj"
	};
}
