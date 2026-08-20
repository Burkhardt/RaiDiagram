namespace RaiDiagram;

public enum DiagramKind
{
	Mixed,
	UseCase,
	Class,
	Object,
	ActivityObject,
	Activity,
	Sequence
}

public static class DiagramElementKinds
{
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
	public const string Swimlane = "Swimlane";
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
	public const string ControlFlow = "ControlFlowConnector";
	public const string ObjectFlow = "ObjectFlowConnector";
	public const string Message = "MessageConnector";
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
}

public sealed class DiagramRelationship
{
	public string Id { get; set; } = string.Empty;
	public string Kind { get; set; } = string.Empty;
	public string SourceId { get; set; } = string.Empty;
	public string TargetId { get; set; } = string.Empty;
	public string? Label { get; set; }
	public string? Cardinality { get; set; }
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

	public string SchemaVersion { get; set; } = CurrentSchemaVersion;
	public DiagramIdentity Diagram { get; set; } = new();
	public DiagramModelIdentity Model { get; set; } = new();
	public DiagramProjection Projection { get; set; } = new();
	public DiagramPresentation Presentation { get; set; } = new();
	public List<DiagramAnnotation> Annotations { get; set; } = [];

	public void Validate()
	{
		if (!string.Equals(SchemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
			throw new RaidSchemaException(
				$"Unsupported .raid schema version '{SchemaVersion}'. Expected '{CurrentSchemaVersion}'.");
		if (string.IsNullOrWhiteSpace(Diagram.Id))
			throw new RaidSchemaException("The diagram requires an id.");
		if (string.IsNullOrWhiteSpace(Diagram.Title))
			throw new RaidSchemaException($"Diagram '{Diagram.Id}' requires a title.");
		Model.Validate();

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
			foreach (var relation in element.SourceRelationships)
				relation.Validate();
			if (element.ParentId is not null && !elements.ContainsKey(element.ParentId))
				throw new RaidSchemaException(
					$"Element '{element.Id}' refers to missing parent '{element.ParentId}'.");
			if (element.ParentId is not null
				&& elements[element.ParentId].Kind is not (DiagramElementKinds.Frame or DiagramElementKinds.Swimlane))
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
