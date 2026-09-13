using System.Text;
using OsLib;
using RaiDiagram.Builders;

namespace RaiDiagram;

public sealed class PlantUmlCompileOptions
{
	/// <summary>
	/// Explicitly approved local directory containing puml-theme-&lt;name&gt;.puml.
	/// Null selects PlantUML's built-in theme lookup only.
	/// </summary>
	public RaiPath? LocalThemeRoot { get; init; }
}

public sealed class DiagramCapabilityReport
{
	public IReadOnlyList<string> UnsupportedConstructs { get; init; } = [];
	public IReadOnlyList<string> Warnings { get; init; } = [];
	public bool CanRender => UnsupportedConstructs.Count == 0;
}

public sealed class PlantUmlCompilation
{
	public required string Source { get; init; }
	public required DiagramCapabilityReport Capabilities { get; init; }
}

public sealed class PlantUmlDiagramCompiler
{
	private static readonly string[] SupportedElements =
	[
		DiagramElementKinds.Role,
		DiagramElementKinds.UseCase,
		DiagramElementKinds.Class,
		DiagramElementKinds.Interface,
		DiagramElementKinds.Enumeration,
		DiagramElementKinds.Object,
		DiagramElementKinds.Activity,
		DiagramElementKinds.ObjectNode,
		DiagramElementKinds.Lifeline,
		DiagramElementKinds.State,
		DiagramElementKinds.Event,
		DiagramElementKinds.Note,
		DiagramElementKinds.Frame,
		DiagramElementKinds.BoundaryFrame,
		DiagramElementKinds.Swimlane,
		DiagramElementKinds.Decision,
		DiagramElementKinds.Divider
	];

	private static readonly string[] SupportedRelationships =
	[
		DiagramRelationshipKinds.RoleUseCase,
		DiagramRelationshipKinds.RoleFilling,
		DiagramRelationshipKinds.Include,
		DiagramRelationshipKinds.Extend,
		DiagramRelationshipKinds.Association,
		DiagramRelationshipKinds.Attribute,
		DiagramRelationshipKinds.Generalization,
		DiagramRelationshipKinds.Realization,
		DiagramRelationshipKinds.Dependency,
		DiagramRelationshipKinds.Containment,
		DiagramRelationshipKinds.ControlFlow,
		DiagramRelationshipKinds.ObjectFlow,
		DiagramRelationshipKinds.Message,
		DiagramRelationshipKinds.AsyncMessage,
		DiagramRelationshipKinds.ReturnMessage,
		DiagramRelationshipKinds.InstanceOf,
		"Association",
		"Composition",
		"Aggregation",
		"Dependency",
		"Generalization",
		"Realization",
		"Inheritance",
		"Uses",
		"Includes",
		"Extends",
		"Flow",
		"Message",
		"Link",
		"Reference",
		"Role"
	];

	/// <summary>The exact, case-sensitive element-kind values accepted by this compiler.</summary>
	public static IReadOnlyList<string> AcceptedElementKinds { get; } =
		Array.AsReadOnly(SupportedElements);

	/// <summary>
	/// The exact, case-sensitive relationship-kind values accepted by this compiler.
	/// This includes the typed <see cref="DiagramRelationshipKinds"/> values and the
	/// established concise PlantUML-facing compatibility spellings.
	/// </summary>
	public static IReadOnlyList<string> AcceptedRelationshipKinds { get; } =
		Array.AsReadOnly(SupportedRelationships);

	public DiagramCapabilityReport Validate(DiagramManifest manifest)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		manifest.Validate();
		var unsupported = manifest.Projection.Elements
			.Select(item => item.Kind)
			.Where(kind => !SupportedElements.Contains(kind, StringComparer.Ordinal))
			.Concat(manifest.Projection.Relationships
				.Select(item => item.Kind)
				.Where(kind => !SupportedRelationships.Contains(kind, StringComparer.Ordinal)))
			.Distinct(StringComparer.Ordinal)
			.OrderBy(kind => kind, StringComparer.Ordinal)
			.ToArray();

		return new DiagramCapabilityReport { UnsupportedConstructs = unsupported };
	}

	public PlantUmlCompilation Compile(
		DiagramManifest manifest,
		PlantUmlCompileOptions? options = null)
	{
		var capabilities = Validate(manifest);
		if (!capabilities.CanRender)
			throw new UnsupportedDiagramConstructException(capabilities.UnsupportedConstructs[0]);

		_ = options;
		if (manifest.Presentation.LayoutHints.TryGetValue(BuilderMetadata.CompilerProfile, out var profile))
		{
			if (string.Equals(profile, BuilderMetadata.ActivityProfile, StringComparison.Ordinal))
				return CompileActivity(manifest, capabilities);
			if (string.Equals(profile, BuilderMetadata.SequenceProfile, StringComparison.Ordinal))
				return CompileSequence(manifest, capabilities);
		}

		var aliases = manifest.Projection.Elements.ToDictionary(
			item => item.Id,
			item => Alias(item.Id),
			StringComparer.Ordinal);
		var children = manifest.Projection.Elements
			.Where(item => item.ParentId is not null)
			.GroupBy(item => item.ParentId!, StringComparer.Ordinal)
			.ToDictionary(group => group.Key, group => group.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

		var source = new StringBuilder();
		// PlantUML uses an optional @startuml name as the output basename. The
		// renderer requires the SVG to remain a sibling of its staged .puml file,
		// so the source filename must remain authoritative.
		source.AppendLine("@startuml");
		if (RequiresAllowMixing(manifest))
			source.AppendLine("allowmixing");

		var framedIds = manifest.Presentation.Frames
			.SelectMany(frame => frame.ElementIds)
			.ToHashSet(StringComparer.Ordinal);
		foreach (var frame in manifest.Presentation.Frames.OrderBy(item => item.Id, StringComparer.Ordinal))
		{
			source.Append("rectangle \"").Append(EscapeLabel(frame.Title)).Append("\" as ")
				.Append(Alias("presentation-frame:" + frame.Id)).AppendLine(" {");
			foreach (var elementId in frame.ElementIds.OrderBy(item => item, StringComparer.Ordinal))
				AppendElement(source, manifest, manifest.Projection.Elements.Single(item => item.Id == elementId), aliases, children, 1);
			source.AppendLine("}");
		}

		foreach (var element in manifest.Projection.Elements
			.Where(item => item.ParentId is null && !framedIds.Contains(item.Id))
			.OrderBy(item => item.Id, StringComparer.Ordinal))
			AppendElement(source, manifest, element, aliases, children, 0);

		foreach (var relationship in manifest.Projection.Relationships.OrderBy(item => item.Id, StringComparer.Ordinal))
			AppendRelationship(source, relationship, aliases);
		AppendAnnotations(source, manifest, aliases);

		source.AppendLine("@enduml");
		return new PlantUmlCompilation { Source = source.ToString(), Capabilities = capabilities };
	}

	private static void AppendElement(
		StringBuilder source,
		DiagramManifest manifest,
		DiagramElement element,
		IReadOnlyDictionary<string, string> aliases,
		IReadOnlyDictionary<string, DiagramElement[]> children,
		int depth)
	{
		var indent = new string('\t', depth);
		var alias = aliases[element.Id];
		var label = EscapeLabel(element.DisplayName);

		if (element.Kind is DiagramElementKinds.Frame or DiagramElementKinds.BoundaryFrame or DiagramElementKinds.Swimlane)
		{
			var keyword = element.Kind switch
			{
				DiagramElementKinds.Frame => "package",
				DiagramElementKinds.BoundaryFrame => "frame",
				_ => "partition"
			};
			source.Append(indent).Append(keyword).Append(" \"").Append(label).Append("\" as ").Append(alias).AppendLine(" {");
			if (children.TryGetValue(element.Id, out var nested))
				foreach (var child in nested)
					AppendElement(source, manifest, child, aliases, children, depth + 1);
			source.Append(indent).AppendLine("}");
			return;
		}

		var declaration = manifest.Presentation.LayoutHints.TryGetValue(
			$"{BuilderMetadata.ShapePrefix}{element.Id}", out var requestedShape)
			? requestedShape
			: element.Kind switch
		{
			DiagramElementKinds.Role => "actor",
			DiagramElementKinds.UseCase => "usecase",
			DiagramElementKinds.Class => "class",
			DiagramElementKinds.Interface => "interface",
			DiagramElementKinds.Enumeration => "enum",
			DiagramElementKinds.Object => "object",
			DiagramElementKinds.Activity => "rectangle",
			DiagramElementKinds.ObjectNode => "artifact",
			DiagramElementKinds.Lifeline => "participant",
			DiagramElementKinds.State => "state",
			DiagramElementKinds.Event => "queue",
			DiagramElementKinds.Note => "note",
			DiagramElementKinds.Decision => "diamond",
			DiagramElementKinds.Divider => "rectangle",
			_ => throw new UnsupportedDiagramConstructException(element.Kind)
		};
		var members = MemberLines(element).ToArray();
		source.Append(indent).Append(declaration).Append(" \"").Append(label).Append("\" as ").Append(alias);
		if (members.Length == 0)
		{
			source.AppendLine();
			return;
		}
		source.AppendLine(" {");
		foreach (var member in members)
			source.Append(indent).Append('\t').AppendLine(EscapeLabel(member));
		source.Append(indent).AppendLine("}");
	}

	private static void AppendRelationship(
		StringBuilder source,
		DiagramRelationship relationship,
		IReadOnlyDictionary<string, string> aliases)
	{
		var (arrow, defaultLabel) = relationship.Kind switch
		{
			DiagramRelationshipKinds.RoleUseCase => (" --> ", (string?)null),
			DiagramRelationshipKinds.RoleFilling => (" ..> ", "fills"),
			DiagramRelationshipKinds.Include or "Includes" => (" ..> ", "<<include>>"),
			DiagramRelationshipKinds.Extend or "Extends" => (" ..> ", "<<extend>>"),
			DiagramRelationshipKinds.Association or "Association" or "Link" => (" -- ", (string?)null),
			DiagramRelationshipKinds.Attribute => (" --> ", "attribute"),
			DiagramRelationshipKinds.Generalization or "Generalization" or "Inheritance" => (" <|-- ", (string?)null),
			DiagramRelationshipKinds.Realization or "Realization" => (" ..|> ", (string?)null),
			DiagramRelationshipKinds.Dependency or "Dependency" or "Uses" or "Reference" => (" ..> ", (string?)null),
			DiagramRelationshipKinds.Containment or "Composition" => (" *-- ", (string?)null),
			"Aggregation" => (" o-- ", (string?)null),
			DiagramRelationshipKinds.ControlFlow or "Flow" => (" --> ", relationship.Guard),
			DiagramRelationshipKinds.ObjectFlow => (" --> ", "object flow"),
			DiagramRelationshipKinds.Message or "Message" => (" -> ", (string?)null),
			DiagramRelationshipKinds.AsyncMessage => (" ->> ", (string?)null),
			DiagramRelationshipKinds.ReturnMessage => (" --> ", (string?)null),
			DiagramRelationshipKinds.InstanceOf => (" <|.. ", "«instanceOf»"),
			"Role" => (" --> ", (string?)null),
			_ => throw new UnsupportedDiagramConstructException(relationship.Kind)
		};
		var label = relationship.Label ?? defaultLabel;
		source.Append(aliases[relationship.SourceId]).Append(arrow).Append(aliases[relationship.TargetId]);
		if (!string.IsNullOrWhiteSpace(label) || !string.IsNullOrWhiteSpace(relationship.Cardinality))
		{
			source.Append(" : ");
			if (!string.IsNullOrWhiteSpace(label))
				source.Append(EscapeLabel(label));
			if (!string.IsNullOrWhiteSpace(relationship.Cardinality))
			{
				if (!string.IsNullOrWhiteSpace(label)) source.Append(' ');
				source.Append('[').Append(EscapeLabel(relationship.Cardinality)).Append(']');
			}
		}
		source.AppendLine();
	}

	private static PlantUmlCompilation CompileActivity(
		DiagramManifest manifest,
		DiagramCapabilityReport capabilities)
	{
		var source = new StringBuilder();
		source.AppendLine("@startuml");
		source.Append("title ").AppendLine(EscapeLabel(manifest.Diagram.Title));
		var elements = manifest.Projection.Elements.ToDictionary(item => item.Id, StringComparer.Ordinal);
		var outgoing = manifest.Projection.Relationships
			.Where(item => item.Kind == DiagramRelationshipKinds.ControlFlow)
			.GroupBy(item => item.SourceId, StringComparer.Ordinal)
			.ToDictionary(group => group.Key, group => group.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
		var terminalIds = manifest.Presentation.LayoutHints
			.Where(item => item.Key.StartsWith(BuilderMetadata.TerminalPrefix, StringComparison.Ordinal))
			.Select(item => item.Value)
			.ToHashSet(StringComparer.Ordinal);
		var orderedIds = OrderedStatementIds(manifest).ToArray();
		var initialId = manifest.Presentation.LayoutHints.GetValueOrDefault(BuilderMetadata.InitialStep)
			?? orderedIds.FirstOrDefault(id => elements.GetValueOrDefault(id)?.Kind == DiagramElementKinds.Activity);
		var emitted = new HashSet<string>(StringComparer.Ordinal);
		string? currentLane = null;
		var wroteStop = false;

		void SetLane(DiagramElement element)
		{
			if (element.ParentId is null || !elements.TryGetValue(element.ParentId, out var lane))
				return;
			if (string.Equals(currentLane, lane.Id, StringComparison.Ordinal))
				return;
			source.Append('|').Append(EscapeLabel(lane.DisplayName)).AppendLine("|");
			currentLane = lane.Id;
		}

		void EmitNode(string id, HashSet<string> path)
		{
			if (!elements.TryGetValue(id, out var element) || !path.Add(id))
				return;
			if (element.Kind == DiagramElementKinds.Decision)
			{
				SetLane(element);
				var branches = outgoing.GetValueOrDefault(id) ?? [];
				var whenTrue = branches.FirstOrDefault(item => string.Equals(item.Guard, "true", StringComparison.OrdinalIgnoreCase));
				var whenFalse = branches.FirstOrDefault(item => string.Equals(item.Guard, "false", StringComparison.OrdinalIgnoreCase));
				if (whenTrue is null || whenFalse is null)
					throw new RaidSchemaException($"Activity decision '{id}' requires true and false branches.");
				source.Append("if (").Append(EscapeLabel(element.DisplayName)).AppendLine(") then (true)");
				currentLane = null;
				EmitNode(whenTrue.TargetId, new HashSet<string>(path, StringComparer.Ordinal));
				source.AppendLine("else (false)");
				currentLane = null;
				EmitNode(whenFalse.TargetId, new HashSet<string>(path, StringComparer.Ordinal));
				source.AppendLine("endif");
				emitted.Add(id);
				return;
			}
			if (element.Kind != DiagramElementKinds.Activity)
				return;
			SetLane(element);
			source.Append(':').Append(EscapeActivity(element.DisplayName)).AppendLine(";");
			emitted.Add(id);
			if (terminalIds.Contains(id))
			{
				wroteStop = true;
				return;
			}
			var next = outgoing.GetValueOrDefault(id) ?? [];
			if (next.Length > 1)
				throw new RaidSchemaException($"Activity step '{id}' has multiple outgoing transitions but is not a decision.");
			if (next.Length == 1)
				EmitNode(next[0].TargetId, path);
		}

		if (initialId is not null)
		{
			if (elements.TryGetValue(initialId, out var initialElement))
				SetLane(initialElement);
			source.AppendLine("start");
			EmitNode(initialId, new HashSet<string>(StringComparer.Ordinal));
		}
		foreach (var id in orderedIds.Where(id => !emitted.Contains(id)))
			EmitNode(id, new HashSet<string>(StringComparer.Ordinal));
		if (terminalIds.Count > 0 || wroteStop)
			source.AppendLine("stop");
		source.AppendLine("@enduml");
		return new PlantUmlCompilation { Source = source.ToString(), Capabilities = capabilities };
	}

	private static PlantUmlCompilation CompileSequence(
		DiagramManifest manifest,
		DiagramCapabilityReport capabilities)
	{
		var source = new StringBuilder();
		source.AppendLine("@startuml");
		source.Append("title ").AppendLine(EscapeLabel(manifest.Diagram.Title));
		var aliases = manifest.Projection.Elements.ToDictionary(item => item.Id, item => Alias(item.Id), StringComparer.Ordinal);
		foreach (var participant in manifest.Projection.Elements
			.Where(item => item.Kind == DiagramElementKinds.Lifeline)
			.OrderBy(item => item.Id, StringComparer.Ordinal))
		{
			var type = FactText(participant, BuilderMetadata.ParticipantType) ?? "participant";
			source.Append(type).Append(" \"").Append(EscapeLabel(participant.DisplayName)).Append("\" as ")
				.AppendLine(aliases[participant.Id]);
		}

		var relationships = manifest.Projection.Relationships.ToDictionary(item => item.Id, StringComparer.Ordinal);
		var elements = manifest.Projection.Elements.ToDictionary(item => item.Id, StringComparer.Ordinal);
		foreach (var statementId in OrderedStatementIds(manifest))
		{
			if (relationships.TryGetValue(statementId, out var relationship))
			{
				var arrow = relationship.Kind switch
				{
					DiagramRelationshipKinds.AsyncMessage => " ->> ",
					DiagramRelationshipKinds.ReturnMessage => " --> ",
					_ => " -> "
				};
				source.Append(aliases[relationship.SourceId]).Append(arrow).Append(aliases[relationship.TargetId]);
				if (!string.IsNullOrWhiteSpace(relationship.Label))
					source.Append(" : ").Append(EscapeLabel(relationship.Label));
				source.AppendLine();
				continue;
			}
			if (!elements.TryGetValue(statementId, out var statement))
				continue;
			if (statement.Kind == DiagramElementKinds.Divider)
			{
				source.Append("== ").Append(EscapeLabel(statement.DisplayName)).AppendLine(" ==");
				continue;
			}
			if (statement.Kind == DiagramElementKinds.Note && statement.SelectedBy.Count == 1)
			{
				var targetAlias = aliases[statement.SelectedBy[0]];
				var position = statement.Description ?? "right";
				source.Append("note ").Append(position);
				if (position == "over") source.Append(' '); else source.Append(" of ");
				source.AppendLine(targetAlias);
				foreach (var line in NormalizeLines(statement.DisplayName))
					source.AppendLine(line);
				source.AppendLine("end note");
			}
		}
		source.AppendLine("@enduml");
		return new PlantUmlCompilation { Source = source.ToString(), Capabilities = capabilities };
	}

	private static void AppendAnnotations(
		StringBuilder source,
		DiagramManifest manifest,
		IReadOnlyDictionary<string, string> aliases)
	{
		foreach (var annotation in manifest.Annotations.OrderBy(item => item.Id, StringComparer.Ordinal))
		{
			if (annotation.ElementId is null || !aliases.TryGetValue(annotation.ElementId, out var alias))
				continue;
			source.Append("note right of ").AppendLine(alias);
			foreach (var line in NormalizeLines(annotation.Text))
				source.AppendLine(line);
			source.AppendLine("end note");
		}
	}

	private static bool RequiresAllowMixing(DiagramManifest manifest)
	{
		var declarations = manifest.Projection.Elements
			.Where(item => item.Kind is not (DiagramElementKinds.Note or DiagramElementKinds.Divider))
			.Select(item => DeclarationFamily(manifest, item))
			.Concat(manifest.Presentation.Frames.Count > 0 ? ["rectangle"] : Array.Empty<string>())
			.Distinct(StringComparer.Ordinal)
			.ToHashSet(StringComparer.Ordinal);

		// Actors, use cases and their package/frame boundaries are one native
		// PlantUML use-case vocabulary. Object declarations cross that grammar
		// boundary and require allowmixing when combined with any other family.
		// This keeps ordinary use-case styles in their native componentDiagram
		// scope while protecting UCD/RFD and class-instance projections.
		return declarations.Contains("object") && declarations.Count > 1;
	}

	private static string DeclarationFamily(DiagramManifest manifest, DiagramElement element)
	{
		if (manifest.Presentation.LayoutHints.TryGetValue($"{BuilderMetadata.ShapePrefix}{element.Id}", out var shape))
			return shape;
		return element.Kind switch
		{
			DiagramElementKinds.Role => "actor",
			DiagramElementKinds.UseCase => "usecase",
			DiagramElementKinds.Class or DiagramElementKinds.Interface or DiagramElementKinds.Enumeration => "class",
			DiagramElementKinds.Object => "object",
			DiagramElementKinds.Lifeline => "sequence",
			DiagramElementKinds.State or DiagramElementKinds.Decision => "state",
			DiagramElementKinds.Frame => "package",
			DiagramElementKinds.BoundaryFrame => "frame",
			DiagramElementKinds.Swimlane => "partition",
			_ => "rectangle"
		};
	}

	private static IEnumerable<string> MemberLines(DiagramElement element)
	{
		var prefixes = new[]
		{
			BuilderMetadata.ClassAttributePrefix,
			BuilderMetadata.ClassRolePrefix,
			BuilderMetadata.ClassMethodPrefix,
			BuilderMetadata.ObjectAttributePrefix
		};
		return element.RelevantFacts
			.Where(item => prefixes.Any(prefix => item.Key.StartsWith(prefix, StringComparison.Ordinal)))
			.OrderBy(item => item.Key, StringComparer.Ordinal)
			.Select(item => item.Value.Text ?? string.Empty);
	}

	private static string? FactText(DiagramElement element, string key) =>
		element.RelevantFacts.TryGetValue(key, out var value) ? value.Text : null;

	private static IEnumerable<string> OrderedStatementIds(DiagramManifest manifest) =>
		manifest.Presentation.LayoutHints
			.Where(item => item.Key.StartsWith(BuilderMetadata.StatementPrefix, StringComparison.Ordinal))
			.OrderBy(item => item.Key, StringComparer.Ordinal)
			.Select(item => item.Value);

	private static IEnumerable<string> NormalizeLines(string value) =>
		value.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');

	private static string EscapeActivity(string value) =>
		EscapeLabel(value).Replace(";", "\\;", StringComparison.Ordinal);

	private static string Alias(string id)
		=> "raid_" + CanonicalJson.Sha256Hex(id)[..16];

	private static string EscapeLabel(string value)
		=> value.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal)
			.Replace("\r", string.Empty, StringComparison.Ordinal)
			.Replace("\n", "\\n", StringComparison.Ordinal);

}
