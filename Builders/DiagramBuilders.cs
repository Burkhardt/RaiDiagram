using RaiDiagram;
using RaiImage;

namespace RaiDiagram.Builders;

/// <summary>Canonical suffixes used by the typed diagram archetype builders.</summary>
public enum DiagramArchetype
{
	OneUseCase,
	RoleFiller,
	Object,
	Class,
	Activity,
	Sequence
}

/// <summary>Defines a KL-ONE structural role restriction for class diagrams.</summary>
public sealed record KlOneRoleDef(
	string RoleName,
	string TargetType,
	string Cardinality = "0..*",
	string? DefaultValue = null);

internal static class BuilderMetadata
{
	internal const string CompilerProfile = "raidiagram.compiler.profile";
	internal const string ActivityProfile = "activity-builder";
	internal const string SequenceProfile = "sequence-builder";
	internal const string StatementPrefix = "raidiagram.statement.";
	internal const string ShapePrefix = "raidiagram.shape.";
	internal const string InitialStep = "raidiagram.activity.initial";
	internal const string TerminalPrefix = "raidiagram.activity.terminal.";
	internal const string ParticipantType = "raidiagram.sequence.participantType";
	internal const string DividerMarker = "raidiagram.sequence.divider";
	internal const string NoteMarker = "raidiagram.sequence.note";
	internal const string ClassAttributePrefix = "raidiagram.class.attribute.";
	internal const string ClassRolePrefix = "raidiagram.class.role.";
	internal const string ClassMethodPrefix = "raidiagram.class.method.";
	internal const string ObjectAttributePrefix = "raidiagram.object.attribute.";
}

/// <summary>Shared deterministic manifest construction for typed diagram archetypes.</summary>
public abstract class DiagramBuilder
{
	private int relationshipCounter;
	private int annotationCounter;
	private int statementCounter;

	protected DiagramBuilder(
		string baseItemId,
		DiagramArchetype archetype,
		DiagramKind kind,
		DiagramModelIdentity model,
		int itemNumber = ItemTreeTextFile.NoItemNumber)
	{
		BaseItemId = ValidateBaseItemId(baseItemId);
		ItemNumber = ValidateItemNumber(itemNumber);
		ArgumentNullException.ThrowIfNull(model);
		model.Validate();
		Archetype = archetype;
		NameExt = Suffix(archetype);
		var numberedItemId = ItemNumber == ItemTreeTextFile.NoItemNumber
			? BaseItemId
			: $"{BaseItemId}_{ItemNumber:D2}";
		DiagramItemId = $"{numberedItemId}_{NameExt}";
		Manifest = new DiagramManifest
		{
			Diagram = new DiagramIdentity
			{
				Id = DiagramItemId,
				Title = BaseItemId,
				Kind = kind
			},
			Model = new DiagramModelIdentity
			{
				ProviderScheme = model.ProviderScheme,
				ModelId = model.ModelId,
				CapturedRevision = model.CapturedRevision
			}
		};
	}

	public string BaseItemId { get; }
	/// <summary>The base domain ItemId used for ItemTree bucketing.</summary>
	public string ItemId => BaseItemId;
	public int ItemNumber { get; }
	public DiagramArchetype Archetype { get; }
	/// <summary>The diagram archetype stored as the ItemTree artifact NameExt.</summary>
	public string NameExt { get; }
	public string DiagramItemId { get; }
	protected DiagramManifest Manifest { get; }

	protected DiagramElement AddElement(
		string id,
		string kind,
		string displayName,
		string? parentId = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentException.ThrowIfNullOrWhiteSpace(kind);
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
		if (Manifest.Projection.Elements.Any(item => string.Equals(item.Id, id, StringComparison.Ordinal)))
			throw new RaidSchemaException($"Diagram element '{id}' already exists.");
		var element = new DiagramElement
		{
			Id = id,
			Kind = kind,
			DisplayName = displayName,
			ParentId = parentId
		};
		Manifest.Projection.Elements.Add(element);
		return element;
	}

	protected DiagramRelationship AddRelationship(
		string kind,
		string sourceId,
		string targetId,
		string? label = null,
		string? cardinality = null,
		string? guard = null,
		bool orderedStatement = false)
	{
		var relationship = new DiagramRelationship
		{
			Id = $"relationship-{++relationshipCounter:D6}",
			Kind = kind,
			SourceId = sourceId,
			TargetId = targetId,
			Label = label,
			Cardinality = cardinality,
			Guard = guard
		};
		Manifest.Projection.Relationships.Add(relationship);
		if (orderedStatement)
			RegisterStatement(relationship.Id);
		return relationship;
	}

	protected DiagramAnnotation AddAnnotation(string text, string? elementId = null, bool semantic = false)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(text);
		var annotation = new DiagramAnnotation
		{
			Id = $"annotation-{++annotationCounter:D6}",
			Text = text,
			ElementId = elementId,
			Semantic = semantic
		};
		Manifest.Annotations.Add(annotation);
		return annotation;
	}

	protected void RegisterStatement(string id) =>
		Manifest.Presentation.LayoutHints[$"{BuilderMetadata.StatementPrefix}{++statementCounter:D6}"] = id;

	protected DiagramElement RequireElement(string id)
	{
		var element = Manifest.Projection.Elements.SingleOrDefault(
			item => string.Equals(item.Id, id, StringComparison.Ordinal));
		return element ?? throw new RaidSchemaException($"Diagram element '{id}' has not been defined.");
	}

	protected DiagramManifest BuildSnapshot()
	{
		Manifest.Validate();
		return DiagramModel.FromManifest(Manifest).Manifest;
	}

	protected static string Stereotype(string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		var trimmed = value.Trim();
		return trimmed.StartsWith('«') && trimmed.EndsWith('»') ? trimmed : $"«{trimmed}»";
	}

	private static string ValidateBaseItemId(string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		var trimmed = value.Trim();
		if (trimmed.Any(character => !char.IsLetterOrDigit(character)))
			throw new RaidSchemaException("A diagram BaseItemId may contain letters and digits only; the builder appends the archetype suffix.");
		return trimmed;
	}

	private static int ValidateItemNumber(int value) => value < ItemTreeTextFile.NoItemNumber
		? throw new ArgumentOutOfRangeException(nameof(value), "ItemNumber must be -1 (unset) or zero and greater.")
		: value;

	private static string Suffix(DiagramArchetype archetype) => archetype switch
	{
		DiagramArchetype.OneUseCase => "UCD",
		DiagramArchetype.RoleFiller => "RFD",
		DiagramArchetype.Object => "OD",
		DiagramArchetype.Class => "CD",
		DiagramArchetype.Activity => "AD",
		DiagramArchetype.Sequence => "SD",
		_ => throw new ArgumentOutOfRangeException(nameof(archetype), archetype, "Unknown diagram archetype.")
	};
}

public sealed class OneUseCaseDiagramBuilder : DiagramBuilder
{
	private DiagramElement? mainUseCase;
	private int roleCounter;
	private int referenceCounter;
	private int dependencyCounter;

	public OneUseCaseDiagramBuilder(
		string baseItemId,
		DiagramModelIdentity model,
		int itemNumber = ItemTreeTextFile.NoItemNumber)
		: base(baseItemId, DiagramArchetype.OneUseCase, DiagramKind.UseCase, model, itemNumber) { }

	public OneUseCaseDiagramBuilder SetMainUseCase(string name, string? frame = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		if (mainUseCase is not null)
			throw new RaidSchemaException("The main UseCase has already been defined.");
		string? frameId = null;
		if (!string.IsNullOrWhiteSpace(frame))
		{
			frameId = "boundary-main";
			AddElement(frameId, DiagramElementKinds.BoundaryFrame, frame.Trim());
		}
		mainUseCase = AddElement("usecase-main", DiagramElementKinds.UseCase, name.Trim(), frameId);
		Manifest.Diagram.Title = name.Trim();
		return this;
	}

	public OneUseCaseDiagramBuilder AddInitiatingRole(
		string roleName,
		string stereotype = "executes",
		string cardinality = "1..1")
	{
		var useCase = RequireMainUseCase();
		var role = AddElement($"initiating-role-{++roleCounter:D4}", DiagramElementKinds.Role, roleName.Trim());
		AddRelationship(DiagramRelationshipKinds.RoleUseCase, role.Id, useCase.Id,
			Stereotype(stereotype), cardinality);
		return this;
	}

	public OneUseCaseDiagramBuilder AddDefinedRole(string roleName, string cardinality = "0..1")
	{
		var useCase = RequireMainUseCase();
		var role = AddElement($"defined-role-{++roleCounter:D4}", DiagramElementKinds.Role, roleName.Trim());
		AddRelationship(DiagramRelationshipKinds.Dependency, useCase.Id, role.Id,
			Stereotype("defines role"), cardinality);
		return this;
	}

	public OneUseCaseDiagramBuilder AddObjectReference(
		string targetName,
		string bucket = "What",
		string cardinality = "0..1",
		string? stereotype = null)
	{
		var useCase = RequireMainUseCase();
		var target = AddElement($"reference-{++referenceCounter:D4}", DiagramElementKinds.Object, targetName.Trim());
		var relationshipStereotype = stereotype is null
			? $"references {bucket.Trim()}"
			: stereotype;
		AddRelationship(DiagramRelationshipKinds.Dependency, useCase.Id, target.Id,
			Stereotype(relationshipStereotype), cardinality);
		return this;
	}

	public OneUseCaseDiagramBuilder AddDependency(string dependentUseCaseName)
	{
		var useCase = RequireMainUseCase();
		var dependency = AddElement($"dependency-{++dependencyCounter:D4}", DiagramElementKinds.UseCase,
			dependentUseCaseName.Trim());
		AddRelationship(DiagramRelationshipKinds.Dependency, useCase.Id, dependency.Id, Stereotype("DependsOn"));
		return this;
	}

	public OneUseCaseDiagramBuilder SetNarrativeNote(string noteText)
	{
		AddAnnotation(noteText, RequireMainUseCase().Id);
		return this;
	}

	public DiagramManifest BuildManifest() => BuildSnapshot();

	private DiagramElement RequireMainUseCase() =>
		mainUseCase ?? throw new RaidSchemaException("SetMainUseCase must be called before adding related elements.");
}

public sealed class RoleFillerDiagramBuilder : DiagramBuilder
{
	private DiagramElement? instance;
	private int fillerCounter;

	public RoleFillerDiagramBuilder(
		string baseItemId,
		DiagramModelIdentity model,
		DiagramArchetype archetype = DiagramArchetype.RoleFiller,
		int itemNumber = ItemTreeTextFile.NoItemNumber)
		: base(baseItemId, ValidateArchetype(archetype), DiagramKind.Object, model, itemNumber) { }

	public RoleFillerDiagramBuilder SetInstance(
		string id,
		string className,
		IDictionary<string, string>? attributes = null)
	{
		if (instance is not null)
			throw new RaidSchemaException("The central instance has already been defined.");
		instance = AddElement("instance-main", DiagramElementKinds.Object,
			$"<u>{id.Trim()}</u> : {className.Trim()}");
		var index = 0;
		foreach (var attribute in attributes?.OrderBy(item => item.Key, StringComparer.Ordinal)
			?? Enumerable.Empty<KeyValuePair<string, string>>())
			instance.RelevantFacts[$"{BuilderMetadata.ObjectAttributePrefix}{++index:D4}"] =
				ModelFactValue.String($"{attribute.Key} = {attribute.Value}");
		return this;
	}

	public RoleFillerDiagramBuilder AddWhoFiller(string roleName, string personName)
	{
		var self = RequireInstance();
		var filler = AddElement($"who-filler-{++fillerCounter:D4}", DiagramElementKinds.Object, personName.Trim());
		Manifest.Presentation.LayoutHints[$"{BuilderMetadata.ShapePrefix}{filler.Id}"] = "actor";
		AddRelationship("Role", filler.Id, self.Id, roleName.Trim());
		return this;
	}

	public RoleFillerDiagramBuilder AddWhatFiller(string roleName, string targetId, string targetClass)
	{
		var self = RequireInstance();
		var filler = AddElement($"what-filler-{++fillerCounter:D4}", DiagramElementKinds.Object,
			$"<u>{targetId.Trim()}</u> : {targetClass.Trim()}");
		AddRelationship(DiagramRelationshipKinds.Dependency, self.Id, filler.Id, roleName.Trim());
		return this;
	}

	public RoleFillerDiagramBuilder AddWhereFiller(string roleName, string placeId)
	{
		var self = RequireInstance();
		var filler = AddElement($"where-filler-{++fillerCounter:D4}", DiagramElementKinds.Object,
			$"<u>{placeId.Trim()}</u> : Place");
		AddRelationship(DiagramRelationshipKinds.Dependency, self.Id, filler.Id, roleName.Trim());
		return this;
	}

	public DiagramManifest BuildManifest() => BuildSnapshot();

	private DiagramElement RequireInstance() =>
		instance ?? throw new RaidSchemaException("SetInstance must be called before adding role fillers.");

	private static DiagramArchetype ValidateArchetype(DiagramArchetype archetype) =>
		archetype is DiagramArchetype.RoleFiller or DiagramArchetype.Object
			? archetype
			: throw new ArgumentOutOfRangeException(nameof(archetype), archetype,
				"RoleFillerDiagramBuilder supports only the RFD and OD archetypes.");
}

public sealed class ClassDiagramBuilder : DiagramBuilder
{
	private DiagramElement? classElement;
	private DiagramElement? superClassElement;
	private int instanceCounter;

	public ClassDiagramBuilder(
		string baseItemId,
		DiagramModelIdentity model,
		int itemNumber = ItemTreeTextFile.NoItemNumber)
		: base(baseItemId, DiagramArchetype.Class, DiagramKind.Class, model, itemNumber) { }

	public ClassDiagramBuilder SetClass(
		string name,
		IEnumerable<string>? attributes = null,
		IEnumerable<KlOneRoleDef>? roles = null,
		IEnumerable<string>? methods = null)
	{
		if (classElement is not null)
			throw new RaidSchemaException("The class has already been defined.");
		classElement = AddElement("class-main", DiagramElementKinds.Class, name.Trim());
		var index = 0;
		foreach (var attribute in attributes ?? [])
			classElement.RelevantFacts[$"{BuilderMetadata.ClassAttributePrefix}{++index:D4}"] =
				ModelFactValue.String(attribute);
		index = 0;
		foreach (var role in roles ?? [])
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(role.RoleName);
			ArgumentException.ThrowIfNullOrWhiteSpace(role.TargetType);
			var line = $"{role.RoleName} : {role.TargetType} [{role.Cardinality}]";
			if (role.DefaultValue is not null)
				line += $" = {role.DefaultValue}";
			classElement.RelevantFacts[$"{BuilderMetadata.ClassRolePrefix}{++index:D4}"] = ModelFactValue.String(line);
		}
		index = 0;
		foreach (var method in methods ?? [])
			classElement.RelevantFacts[$"{BuilderMetadata.ClassMethodPrefix}{++index:D4}"] =
				ModelFactValue.String(method);
		Manifest.Diagram.Title = name.Trim();
		return this;
	}

	/// <summary>
	/// Declares the single base class inherited by the class being built and emits
	/// a UML generalization from the base class to the derived class.
	/// </summary>
	public ClassDiagramBuilder SetSuperClass(string superClassName, string? stereotype = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(superClassName);
		var derivedClass = classElement ??
			throw new RaidSchemaException("SetClass must be called before setting the superclass.");
		if (superClassElement is not null)
			throw new RaidSchemaException("The superclass has already been defined.");

		superClassElement = AddElement("class-super", DiagramElementKinds.Class, superClassName.Trim());
		AddRelationship(
			DiagramRelationshipKinds.Generalization,
			superClassElement.Id,
			derivedClass.Id,
			stereotype is null ? null : Stereotype(stereotype));
		return this;
	}

	public ClassDiagramBuilder AddInstance(string objectId, string className)
	{
		var type = classElement ?? throw new RaidSchemaException("SetClass must be called before adding instances.");
		var instance = AddElement($"instance-{++instanceCounter:D4}", DiagramElementKinds.Object,
			$"<u>{objectId.Trim()}</u> : {className.Trim()}");
		AddRelationship(DiagramRelationshipKinds.InstanceOf, type.Id, instance.Id, Stereotype("instanceOf"));
		return this;
	}

	public DiagramManifest BuildManifest() => BuildSnapshot();
}

public sealed class ActivityDiagramBuilder : DiagramBuilder
{
	private readonly Dictionary<string, DiagramElement> lanes = new(StringComparer.Ordinal);
	private bool activityDefined;
	private int laneCounter;

	public ActivityDiagramBuilder(
		string baseItemId,
		DiagramModelIdentity model,
		int itemNumber = ItemTreeTextFile.NoItemNumber)
		: base(baseItemId, DiagramArchetype.Activity, DiagramKind.Activity, model, itemNumber)
	{
		Manifest.Presentation.LayoutHints[BuilderMetadata.CompilerProfile] = BuilderMetadata.ActivityProfile;
	}

	public ActivityDiagramBuilder SetActivity(string activityName, string? description = null)
	{
		Manifest.Diagram.Title = activityName.Trim();
		Manifest.Diagram.Purpose = description;
		activityDefined = true;
		return this;
	}

	public ActivityDiagramBuilder AddSwimlane(string roleName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
		if (lanes.ContainsKey(roleName))
			throw new RaidSchemaException($"Swimlane '{roleName}' already exists.");
		lanes[roleName] = AddElement($"swimlane-{++laneCounter:D4}", DiagramElementKinds.Swimlane, roleName.Trim());
		return this;
	}

	public ActivityDiagramBuilder AddStep(string stepId, string roleName, string actionDescription)
	{
		var lane = RequireLane(roleName);
		AddElement(stepId, DiagramElementKinds.Activity, actionDescription.Trim(), lane.Id);
		RegisterStatement(stepId);
		return this;
	}

	public ActivityDiagramBuilder AddTransition(
		string fromStepId,
		string toStepId,
		string? guardExpression = null)
	{
		AddRelationship(DiagramRelationshipKinds.ControlFlow, fromStepId, toStepId,
			guard: guardExpression);
		return this;
	}

	public ActivityDiagramBuilder AddDecision(
		string decisionId,
		string roleName,
		string condition,
		string trueStepId,
		string falseStepId)
	{
		var lane = RequireLane(roleName);
		AddElement(decisionId, DiagramElementKinds.Decision, condition.Trim(), lane.Id);
		RegisterStatement(decisionId);
		AddRelationship(DiagramRelationshipKinds.ControlFlow, decisionId, trueStepId, guard: "true");
		AddRelationship(DiagramRelationshipKinds.ControlFlow, decisionId, falseStepId, guard: "false");
		return this;
	}

	public ActivityDiagramBuilder SetInitialStep(string stepId)
	{
		Manifest.Presentation.LayoutHints[BuilderMetadata.InitialStep] = stepId;
		return this;
	}

	public ActivityDiagramBuilder AddTerminalStep(string stepId)
	{
		Manifest.Presentation.LayoutHints[$"{BuilderMetadata.TerminalPrefix}{stepId}"] = stepId;
		return this;
	}

	public DiagramManifest BuildManifest()
	{
		if (!activityDefined)
			throw new RaidSchemaException("SetActivity must be called before BuildManifest.");
		if (Manifest.Presentation.LayoutHints.TryGetValue(BuilderMetadata.InitialStep, out var initial))
			RequireElement(initial);
		foreach (var terminal in Manifest.Presentation.LayoutHints
			.Where(item => item.Key.StartsWith(BuilderMetadata.TerminalPrefix, StringComparison.Ordinal))
			.Select(item => item.Value))
			RequireElement(terminal);
		return BuildSnapshot();
	}

	private DiagramElement RequireLane(string roleName) =>
		lanes.TryGetValue(roleName, out var lane)
			? lane
			: throw new RaidSchemaException($"Swimlane '{roleName}' has not been defined.");
}

public sealed class SequenceDiagramBuilder : DiagramBuilder
{
	private static readonly string[] ParticipantTypes = ["participant", "actor", "boundary", "control", "entity"];
	private readonly Dictionary<string, DiagramElement> participants = new(StringComparer.Ordinal);
	private int participantCounter;
	private int statementElementCounter;

	public SequenceDiagramBuilder(
		string baseItemId,
		DiagramModelIdentity model,
		int itemNumber = ItemTreeTextFile.NoItemNumber)
		: base(baseItemId, DiagramArchetype.Sequence, DiagramKind.Sequence, model, itemNumber)
	{
		Manifest.Presentation.LayoutHints[BuilderMetadata.CompilerProfile] = BuilderMetadata.SequenceProfile;
	}

	public SequenceDiagramBuilder SetSequenceTitle(string title)
	{
		Manifest.Diagram.Title = title.Trim();
		return this;
	}

	public SequenceDiagramBuilder AddParticipant(string name, string roleType = "Role", string? alias = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		var lookup = string.IsNullOrWhiteSpace(alias) ? name.Trim() : alias.Trim();
		if (participants.ContainsKey(lookup) || participants.ContainsKey(name.Trim()))
			throw new RaidSchemaException($"Sequence participant '{lookup}' already exists.");
		var normalizedType = roleType.Equals("Role", StringComparison.OrdinalIgnoreCase)
			? "participant"
			: roleType.Trim().ToLowerInvariant();
		if (!ParticipantTypes.Contains(normalizedType, StringComparer.Ordinal))
			throw new RaidSchemaException($"Unsupported sequence participant roleType '{roleType}'.");
		var participant = AddElement($"participant-{++participantCounter:D4}", DiagramElementKinds.Lifeline, name.Trim());
		participant.RelevantFacts[BuilderMetadata.ParticipantType] = ModelFactValue.String(normalizedType);
		participants[lookup] = participant;
		participants[name.Trim()] = participant;
		return this;
	}

	public SequenceDiagramBuilder AddMessage(
		string from,
		string to,
		string messageText,
		bool isAsync = false,
		string? returnMessage = null)
	{
		var source = RequireParticipant(from);
		var target = RequireParticipant(to);
		AddRelationship(
			isAsync ? DiagramRelationshipKinds.AsyncMessage : DiagramRelationshipKinds.Message,
			source.Id,
			target.Id,
			messageText.Trim(),
			orderedStatement: true);
		if (!string.IsNullOrWhiteSpace(returnMessage))
			AddRelationship(DiagramRelationshipKinds.ReturnMessage, target.Id, source.Id,
				returnMessage.Trim(), orderedStatement: true);
		return this;
	}

	public SequenceDiagramBuilder AddSelfMessage(string participant, string actionText)
	{
		var target = RequireParticipant(participant);
		AddRelationship(DiagramRelationshipKinds.Message, target.Id, target.Id,
			actionText.Trim(), orderedStatement: true);
		return this;
	}

	public SequenceDiagramBuilder AddDivider(string label)
	{
		var divider = AddElement($"sequence-element-{++statementElementCounter:D6}",
			DiagramElementKinds.Divider, label.Trim());
		divider.Namespace = BuilderMetadata.DividerMarker;
		RegisterStatement(divider.Id);
		return this;
	}

	public SequenceDiagramBuilder AddNote(string participant, string noteText, string position = "right")
	{
		var target = RequireParticipant(participant);
		var normalizedPosition = position.Trim().ToLowerInvariant();
		if (normalizedPosition is not ("left" or "right" or "over"))
			throw new RaidSchemaException("A sequence note position must be left, right, or over.");
		var note = AddElement($"sequence-element-{++statementElementCounter:D6}",
			DiagramElementKinds.Note, noteText.Trim());
		note.Namespace = BuilderMetadata.NoteMarker;
		note.ParentId = null;
		note.Description = normalizedPosition;
		note.SelectedBy.Add(target.Id);
		RegisterStatement(note.Id);
		return this;
	}

	public DiagramManifest BuildManifest() => BuildSnapshot();

	private DiagramElement RequireParticipant(string key) =>
		participants.TryGetValue(key.Trim(), out var participant)
			? participant
			: throw new RaidSchemaException($"Sequence participant '{key}' has not been defined.");
}
