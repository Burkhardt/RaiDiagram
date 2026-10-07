namespace RaiDiagram;

public enum DiagramKind
{
	Mixed,
	UseCase,
	Class,
	Object,
	ActivityObject,
	Activity,
	Sequence,
	Deployment
}

public static class DiagramElementKinds
{
	public const string Node = "Node";
	public const string Cloud = "Cloud";
	public const string Database = "Database";
	public const string Component = "Component";
	public const string Artifact = "Artifact";
	public const string Folder = "Folder";
	public const string Role = "Role";
	public const string UseCase = "UseCase";
	public const string Class = "Class";
	public const string Interface = "Interface";
	public const string Enumeration = "Enumeration";
	public const string Object = "Object";
	public const string Activity = "Activity";
	public const string ObjectNode = "ObjectNode";
	public const string Lifeline = "Lifeline";
	public const string State = "State";
	public const string Event = "Event";
	public const string Note = "Note";
	public const string Frame = "Frame";
	public const string BoundaryFrame = "BoundaryFrame";
	public const string Swimlane = "Swimlane";
	public const string Decision = "Decision";
	public const string Divider = "Divider";
}

public static class DiagramRelationshipKinds
{
	public const string RoleUseCase = "RoleUseCaseConnector";
	public const string RoleFilling = "RoleFillingConnector";
	public const string Include = "IncludeConnector";
	public const string Extend = "ExtendConnector";
	public const string Association = "AssociationConnector";
	public const string Attribute = "AttributeConnector";
	public const string Generalization = "GeneralizationConnector";
	public const string Realization = "RealizationConnector";
	public const string Dependency = "DependencyConnector";
	public const string Containment = "ContainmentConnector";
	public const string Aggregation = "AggregationConnector";
	public const string ControlFlow = "ControlFlowConnector";
	public const string ObjectFlow = "ObjectFlowConnector";
	public const string Message = "MessageConnector";
	public const string AsyncMessage = "AsyncMessageConnector";
	public const string ReturnMessage = "ReturnMessageConnector";
	public const string InstanceOf = "InstanceOfConnector";
}

public sealed class DiagramIdentity
{
	public string Id { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public DiagramKind Kind { get; set; }
	public string? Purpose { get; set; }
}

public sealed class DiagramElement
{
	public string Id { get; set; } = string.Empty;
	public string Kind { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string? Namespace { get; set; }
	public string? Description { get; set; }
	public string? ParentId { get; set; }
	public ModelElementReference? Source { get; set; }
	public Dictionary<string, ModelFactValue> RelevantFacts { get; set; } = new(StringComparer.Ordinal);
	public List<ModelElementReference> SourceRelationships { get; set; } = [];
	public string? SourceSemanticHash { get; set; }
	public List<string> SelectedBy { get; set; } = [];
	/// <summary>Ordered object slots. Values are source text, never inferred CLR numbers or dates.</summary>
	public List<DiagramObjectProperty> ObjectProperties { get; set; } = [];
	public bool ShouldSerializeObjectProperties() => ObjectProperties.Count != 0;
}

public sealed class DiagramObjectProperty
{
	public string Name { get; set; } = string.Empty;
	public string Value { get; set; } = string.Empty;
}

public sealed class DiagramRelationship
{
	public string Id { get; set; } = string.Empty;
	public string Kind { get; set; } = string.Empty;
	public string SourceId { get; set; } = string.Empty;
	public string TargetId { get; set; } = string.Empty;
	public string? Label { get; set; }
	public string? Cardinality { get; set; }
	public bool Directed { get; set; }
	public bool ShouldSerializeDirected() => Directed;
	public string? Guard { get; set; }
	public ModelElementReference? SourceReference { get; set; }
}

public sealed class DiagramProjection
{
	public List<DiagramElement> Elements { get; set; } = [];
	public List<DiagramRelationship> Relationships { get; set; } = [];
	public List<DiagramSelectionRule> SelectionRules { get; set; } = [];
}

public sealed class DiagramPresentation
{
	public string? Theme { get; set; }
	public string? FontName { get; set; }
	public bool Handwritten { get; set; }
	public List<DiagramPresentationFrame> Frames { get; set; } = [];
	public Dictionary<string, string> LayoutHints { get; set; } = new(StringComparer.Ordinal);
	public DiagramCanvasPresentation? Canvas { get; set; }
}

/// <summary>Deterministic canvas geometry used by interactive SVG consumers.</summary>
public sealed class DiagramCanvasPresentation
{
	public string Routing { get; set; } = "manhattan";
	public List<DiagramCanvasNode> Nodes { get; set; } = [];
	public List<DiagramCanvasEdge> Edges { get; set; } = [];
}

public sealed class DiagramCanvasNode
{
	/// <summary>Optional drawing identity for repeated views of one semantic element (schema 1.1).</summary>
	public string? ViewId { get; set; }
	/// <summary>Parent drawing identity. X/Y remain relative to this drawing when specified.</summary>
	public string? ParentViewId { get; set; }
	[Newtonsoft.Json.JsonIgnore]
	public string Identity => ViewId ?? ElementId;
	public string ElementId { get; set; } = string.Empty;
	public string Archetype { get; set; } = string.Empty;
	public decimal X { get; set; }
	public decimal Y { get; set; }
	public decimal Width { get; set; }
	public decimal Height { get; set; }
}

public sealed class DiagramCanvasEdge
{
	public string? ViewId { get; set; }
	public string? SourceViewId { get; set; }
	public string? TargetViewId { get; set; }
	/// <summary>Complete absolute path including endpoints; never relaid out (schema 1.1).</summary>
	public List<DiagramCanvasPoint> Waypoints { get; set; } = [];
	public bool ShouldSerializeWaypoints() => Waypoints.Count > 0;
	[Newtonsoft.Json.JsonIgnore]
	public string Identity => ViewId ?? RelationshipId;
	public string RelationshipId { get; set; } = string.Empty;
	public string Routing { get; set; } = "manhattan";
	public List<DiagramCanvasPoint> BendPoints { get; set; } = [];
}

public sealed class DiagramCanvasPoint
{
	public decimal X { get; set; }
	public decimal Y { get; set; }
}

/// <summary>A visual-only grouping frame. It is deliberately excluded from the semantic hash.</summary>
public sealed class DiagramPresentationFrame
{
	public string Id { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public List<string> ElementIds { get; set; } = [];
	public string? StyleRole { get; set; }
}

public sealed class DiagramAnnotation
{
	public string Id { get; set; } = string.Empty;
	public string Text { get; set; } = string.Empty;
	public bool Semantic { get; set; }
	public string? ElementId { get; set; }
}

public sealed class DiagramManifest
{
	public const string CurrentSchemaVersion = "1.0";
	public const string ExtendedSchemaVersion = "1.1";

	public string SchemaVersion { get; set; } = CurrentSchemaVersion;
	public DiagramIdentity Diagram { get; set; } = new();
	public DiagramModelIdentity Model { get; set; } = new();
	public DiagramProjection Projection { get; set; } = new();
	public DiagramPresentation Presentation { get; set; } = new();
	public List<DiagramAnnotation> Annotations { get; set; } = [];

	public void Validate()
	{
		if (SchemaVersion is not (CurrentSchemaVersion or ExtendedSchemaVersion))
			throw new RaidSchemaException(
				$"Unsupported .raid schema version '{SchemaVersion}'. Expected '{CurrentSchemaVersion}' or '{ExtendedSchemaVersion}'.");
		if (string.IsNullOrWhiteSpace(Diagram.Id))
			throw new RaidSchemaException("The diagram requires an id.");
		if (string.IsNullOrWhiteSpace(Diagram.Title))
			throw new RaidSchemaException($"Diagram '{Diagram.Id}' requires a title.");
		Model.Validate();
		if (SchemaVersion != ExtendedSchemaVersion && (Diagram.Kind == DiagramKind.Deployment
			|| Projection.Elements.Any(e => IsDeploymentContainer(e.Kind)) || Projection.Relationships.Any(r => r.Directed)))
			throw new RaidSchemaException("Deployment elements and directed associations require schema 1.1.");

		EnsureUnique(Projection.Elements.Select(item => item.Id), "diagram element");
		EnsureUnique(Projection.Relationships.Select(item => item.Id), "diagram relationship");
		EnsureUnique(Projection.SelectionRules.Select(item => item.Id), "selection rule");
		EnsureUnique(Annotations.Select(item => item.Id), "annotation");

		var elements = Projection.Elements.ToDictionary(item => item.Id, StringComparer.Ordinal);
		foreach (var element in Projection.Elements)
		{
			if (string.IsNullOrWhiteSpace(element.Id))
				throw new RaidSchemaException("Every diagram element requires an id.");
			if (string.IsNullOrWhiteSpace(element.Kind))
				throw new RaidSchemaException($"Diagram element '{element.Id}' requires a kind.");
			if (string.IsNullOrWhiteSpace(element.DisplayName))
				throw new RaidSchemaException($"Diagram element '{element.Id}' requires a displayName.");
			if (element.Source is not null)
				element.Source.Validate();
			foreach (var fact in element.RelevantFacts)
				fact.Value.Validate(fact.Key);
			if (element.ObjectProperties.Count > 0)
			{
				if (SchemaVersion != ExtendedSchemaVersion || element.Kind != DiagramElementKinds.Object)
					throw new RaidSchemaException("ObjectProperties require an Object element and schema 1.1.");
				EnsureUnique(element.ObjectProperties.Select(property => property.Name), "object property");
				if (element.ObjectProperties.Any(property => string.IsNullOrWhiteSpace(property.Name) || property.Value is null))
					throw new RaidSchemaException("Object properties require a name and a non-null text value.");
			}
			foreach (var relation in element.SourceRelationships)
				relation.Validate();
			if (element.ParentId is not null && !elements.ContainsKey(element.ParentId))
				throw new RaidSchemaException(
					$"Element '{element.Id}' refers to missing parent '{element.ParentId}'.");
			if (element.ParentId is not null
				&& elements[element.ParentId].Kind is not (
					DiagramElementKinds.Frame
					or DiagramElementKinds.BoundaryFrame
					or DiagramElementKinds.Swimlane)
				&& !(SchemaVersion == ExtendedSchemaVersion && IsDeploymentContainer(elements[element.ParentId].Kind)))
				throw new RaidSchemaException(
					$"Element '{element.Id}' can only be nested inside a Frame or Swimlane.");
		}

		ValidateContainmentCycles(elements);

		foreach (var relationship in Projection.Relationships)
		{
			if (string.IsNullOrWhiteSpace(relationship.Id) || string.IsNullOrWhiteSpace(relationship.Kind))
				throw new RaidSchemaException("Every diagram relationship requires an id and kind.");
			if (!elements.TryGetValue(relationship.SourceId, out var source))
				throw new RaidSchemaException(
					$"Relationship '{relationship.Id}' refers to missing source '{relationship.SourceId}'.");
			if (!elements.TryGetValue(relationship.TargetId, out var target))
				throw new RaidSchemaException(
					$"Relationship '{relationship.Id}' refers to missing target '{relationship.TargetId}'.");
			if (relationship.Kind == DiagramRelationshipKinds.RoleUseCase
				&& (source.Kind != DiagramElementKinds.Role || target.Kind != DiagramElementKinds.UseCase))
				throw new RaidSchemaException(
					$"RoleUseCaseConnector '{relationship.Id}' requires Role -> UseCase.");
			if (relationship.Kind == DiagramRelationshipKinds.RoleFilling
				&& source.Kind != DiagramElementKinds.Role)
				throw new RaidSchemaException(
					$"RoleFillingConnector '{relationship.Id}' requires a Role source.");
			relationship.SourceReference?.Validate();
		}

		foreach (var rule in Projection.SelectionRules)
			rule.Validate();

		foreach (var annotation in Annotations)
		{
			if (string.IsNullOrWhiteSpace(annotation.Id))
				throw new RaidSchemaException("Every annotation requires an id.");
			if (annotation.ElementId is not null && !elements.ContainsKey(annotation.ElementId))
				throw new RaidSchemaException(
					$"Annotation '{annotation.Id}' refers to missing element '{annotation.ElementId}'.");
		}

		EnsureUnique(Presentation.Frames.Select(item => item.Id), "presentation frame");
		var presented = new HashSet<string>(StringComparer.Ordinal);
		foreach (var frame in Presentation.Frames)
		{
			if (string.IsNullOrWhiteSpace(frame.Id) || string.IsNullOrWhiteSpace(frame.Title))
				throw new RaidSchemaException("Every presentation frame requires an id and title.");
			foreach (var elementId in frame.ElementIds)
			{
				if (!elements.ContainsKey(elementId))
					throw new RaidSchemaException(
						$"Presentation frame '{frame.Id}' refers to missing element '{elementId}'.");
				if (!presented.Add(elementId))
					throw new RaidSchemaException(
						$"Element '{elementId}' belongs to more than one presentation frame.");
				if (elements[elementId].ParentId is not null)
					throw new RaidSchemaException(
						$"Presentation frame '{frame.Id}' can only group root elements; '{elementId}' is already semantically nested.");
			}
		}

		ValidateCanvas(Presentation.Canvas, elements, Projection.Relationships);
	}

	internal static bool IsDeploymentContainer(string kind) => kind is
		DiagramElementKinds.Node or DiagramElementKinds.Cloud or DiagramElementKinds.Component
		or DiagramElementKinds.Database or DiagramElementKinds.Folder or DiagramElementKinds.Artifact;

	private void ValidateCanvas(
		DiagramCanvasPresentation? canvas,
		IReadOnlyDictionary<string, DiagramElement> elements,
		IReadOnlyCollection<DiagramRelationship> relationships)
	{
		if (canvas is null)
			return;
		if (string.IsNullOrWhiteSpace(canvas.Routing))
			throw new RaidSchemaException("Canvas routing cannot be empty.");

		if (SchemaVersion != ExtendedSchemaVersion && (canvas.Nodes.Any(n => n.ViewId is not null || n.ParentViewId is not null)
			|| canvas.Edges.Any(e => e.ViewId is not null || e.SourceViewId is not null || e.TargetViewId is not null || e.Waypoints.Count > 0)))
			throw new RaidSchemaException("Drawing identities and complete waypoints require schema 1.1.");
		EnsureUnique(canvas.Nodes.Select(item => item.Identity), "canvas node view");
		var views = canvas.Nodes.ToDictionary(n => n.Identity, StringComparer.Ordinal);
		foreach (var node in canvas.Nodes)
		{
			if (!elements.ContainsKey(node.ElementId))
				throw new RaidSchemaException($"Canvas node refers to missing element '{node.ElementId}'.");
			if (string.IsNullOrWhiteSpace(node.Identity)) throw new RaidSchemaException("Canvas node requires an identity.");
			var parents = new HashSet<string>(StringComparer.Ordinal) { node.Identity };
			var parent = node.ParentViewId;
			while (parent is not null)
			{
				if (!views.TryGetValue(parent, out var parentNode)) throw new RaidSchemaException($"Missing parent view '{parent}'.");
				if (!parents.Add(parent)) throw new RaidSchemaException("Canvas containment cycle.");
				parent = parentNode.ParentViewId;
			}
			if (string.IsNullOrWhiteSpace(node.Archetype))
				throw new RaidSchemaException($"Canvas node '{node.ElementId}' requires an archetype.");
			if (node.Width <= 0 || node.Height <= 0)
				throw new RaidSchemaException($"Canvas node '{node.ElementId}' requires positive dimensions.");
		}

		var relationshipIds = relationships.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
		EnsureUnique(canvas.Edges.Select(item => item.Identity), "canvas edge view");
		foreach (var edge in canvas.Edges)
		{
			if (!relationshipIds.Contains(edge.RelationshipId))
				throw new RaidSchemaException(
					$"Canvas edge refers to missing relationship '{edge.RelationshipId}'.");
			var relation = relationships.Single(r => r.Id == edge.RelationshipId);
			if (!views.TryGetValue(edge.SourceViewId ?? relation.SourceId, out var sourceView) || sourceView.ElementId != relation.SourceId
				|| !views.TryGetValue(edge.TargetViewId ?? relation.TargetId, out var targetView) || targetView.ElementId != relation.TargetId)
				throw new RaidSchemaException($"Canvas edge '{edge.Identity}' has missing or mismatched endpoint views.");
			if (edge.Waypoints.Count == 1) throw new RaidSchemaException("Complete edge paths require at least two waypoints.");
			if (string.IsNullOrWhiteSpace(edge.Routing))
				throw new RaidSchemaException(
					$"Canvas edge '{edge.RelationshipId}' requires a routing strategy.");
		}
	}

	private static void EnsureUnique(IEnumerable<string> ids, string label)
	{
		var duplicate = ids
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.GroupBy(id => id, StringComparer.Ordinal)
			.FirstOrDefault(group => group.Count() > 1);
		if (duplicate is not null)
			throw new RaidSchemaException($"Duplicate {label} id '{duplicate.Key}'.");
	}

	private static void ValidateContainmentCycles(IReadOnlyDictionary<string, DiagramElement> elements)
	{
		foreach (var element in elements.Values)
		{
			var visited = new HashSet<string>(StringComparer.Ordinal) { element.Id };
			var parentId = element.ParentId;
			while (parentId is not null)
			{
				if (!visited.Add(parentId))
					throw new RaidSchemaException($"Element '{element.Id}' participates in a containment cycle.");
				parentId = elements[parentId].ParentId;
			}
		}
	}
}

public sealed class DiagramModel
{
	internal DiagramModel(DiagramManifest manifest)
	{
		Manifest = manifest;
		SemanticHash = DiagramSemanticHasher.Compute(manifest);
	}

	public DiagramManifest Manifest { get; }
	public string SemanticHash { get; }

	/// <summary>
	/// Validates and snapshots an authoritative manifest as an immutable diagram model.
	/// </summary>
	public static DiagramModel FromManifest(DiagramManifest manifest)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		var snapshot = RaidJson5.Parse(RaidJson5.Serialize(manifest));
		return new DiagramModel(snapshot);
	}
}

public sealed class DiagramDraft
{
	private readonly DiagramManifest manifest;
	private int nextRelationship;

	private DiagramDraft(string id, string title, DiagramKind kind, DiagramModelIdentity model)
	{
		manifest = new DiagramManifest
		{
			Diagram = new DiagramIdentity { Id = id, Title = title, Kind = kind },
			Model = model
		};
	}

	public static DiagramDraft Create(
		string id,
		string title,
		DiagramKind kind,
		DiagramModelIdentity model)
		=> new(id, title, kind, model ?? throw new ArgumentNullException(nameof(model)));

	public DiagramManifest Manifest => manifest;

	public DiagramElement AddRole(string id, string displayName, string? parentId = null)
		=> AddElement(id, DiagramElementKinds.Role, displayName, parentId);
	public DiagramElement AddUseCase(string id, string displayName, string? parentId = null)
		=> AddElement(id, DiagramElementKinds.UseCase, displayName, parentId);
	public DiagramElement AddClass(string id, string displayName, string? parentId = null)
		=> AddElement(id, DiagramElementKinds.Class, displayName, parentId);
	public DiagramElement AddObject(string id, string displayName, string? parentId = null)
		=> AddElement(id, DiagramElementKinds.Object, displayName, parentId);
	public DiagramElement AddActivity(string id, string displayName, string? parentId = null)
		=> AddElement(id, DiagramElementKinds.Activity, displayName, parentId);
	public DiagramElement AddFrame(string id, string displayName, string? parentId = null)
		=> AddElement(id, DiagramElementKinds.Frame, displayName, parentId);
	public DiagramElement AddSwimlane(string id, string displayName, string? parentId = null)
		=> AddElement(id, DiagramElementKinds.Swimlane, displayName, parentId);

	public DiagramRelationship ConnectRoleToUseCase(
		DiagramElement role,
		DiagramElement useCase,
		string? label = null)
		=> AddRelationship(DiagramRelationshipKinds.RoleUseCase, role, useCase, label);

	public DiagramRelationship AddRoleFilling(
		DiagramElement role,
		DiagramElement filler,
		string? label = null)
		=> AddRelationship(DiagramRelationshipKinds.RoleFilling, role, filler, label);

	public DiagramRelationship Connect(
		string kind,
		DiagramElement source,
		DiagramElement target,
		string? label = null)
		=> AddRelationship(kind, source, target, label);

	public DiagramModel ValidateAndFreeze()
	{
		return DiagramModel.FromManifest(manifest);
	}

	private DiagramElement AddElement(string id, string kind, string displayName, string? parentId)
	{
		var element = new DiagramElement
		{
			Id = id,
			Kind = kind,
			DisplayName = displayName,
			ParentId = parentId
		};
		manifest.Projection.Elements.Add(element);
		return element;
	}

	private DiagramRelationship AddRelationship(
		string kind,
		DiagramElement source,
		DiagramElement target,
		string? label)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(target);
		var relationship = new DiagramRelationship
		{
			Id = $"relationship-{++nextRelationship}",
			Kind = kind,
			SourceId = source.Id,
			TargetId = target.Id,
			Label = label
		};
		manifest.Projection.Relationships.Add(relationship);
		return relationship;
	}
}
