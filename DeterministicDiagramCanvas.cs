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
		var classLike = manifest.Diagram.Kind == DiagramKind.Class;
		for (var index = 0; index < manifest.Projection.Elements.Count; index++)
		{
			var element = manifest.Projection.Elements[index];
			var column = classLike ? index % 3 : 0;
			var row = classLike ? index / 3 : index;
			canvas.Nodes.Add(new DiagramCanvasNode
			{
				ElementId = element.Id,
				Archetype = ToArchetype(element.Kind),
				X = 40 + column * 280,
				Y = 40 + row * 120,
				Width = classLike ? 220 : 260,
				Height = classLike ? 90 : 70
			});
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
