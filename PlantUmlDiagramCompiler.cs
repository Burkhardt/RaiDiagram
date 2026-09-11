using System.Text;
using OsLib;

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
		DiagramElementKinds.Swimlane
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
		if (manifest.Diagram.Kind is DiagramKind.Mixed or DiagramKind.ActivityObject or DiagramKind.Activity or DiagramKind.Sequence)
			source.AppendLine("allowmixing");

		var framedIds = manifest.Presentation.Frames
			.SelectMany(frame => frame.ElementIds)
			.ToHashSet(StringComparer.Ordinal);
		foreach (var frame in manifest.Presentation.Frames.OrderBy(item => item.Id, StringComparer.Ordinal))
		{
			source.Append("rectangle \"").Append(EscapeLabel(frame.Title)).Append("\" as ")
				.Append(Alias("presentation-frame:" + frame.Id)).AppendLine(" {");
			foreach (var elementId in frame.ElementIds.OrderBy(item => item, StringComparer.Ordinal))
				AppendElement(source, manifest.Projection.Elements.Single(item => item.Id == elementId), aliases, children, 1);
			source.AppendLine("}");
		}

		foreach (var element in manifest.Projection.Elements
			.Where(item => item.ParentId is null && !framedIds.Contains(item.Id))
			.OrderBy(item => item.Id, StringComparer.Ordinal))
			AppendElement(source, element, aliases, children, 0);

		foreach (var relationship in manifest.Projection.Relationships.OrderBy(item => item.Id, StringComparer.Ordinal))
			AppendRelationship(source, relationship, aliases);

		source.AppendLine("@enduml");
		return new PlantUmlCompilation { Source = source.ToString(), Capabilities = capabilities };
	}

	private static void AppendElement(
		StringBuilder source,
		DiagramElement element,
		IReadOnlyDictionary<string, string> aliases,
		IReadOnlyDictionary<string, DiagramElement[]> children,
		int depth)
	{
		var indent = new string('\t', depth);
		var alias = aliases[element.Id];
		var label = EscapeLabel(element.DisplayName);

		if (element.Kind is DiagramElementKinds.Frame or DiagramElementKinds.Swimlane)
		{
			var keyword = element.Kind == DiagramElementKinds.Frame ? "package" : "partition";
			source.Append(indent).Append(keyword).Append(" \"").Append(label).Append("\" as ").Append(alias).AppendLine(" {");
			if (children.TryGetValue(element.Id, out var nested))
				foreach (var child in nested)
					AppendElement(source, child, aliases, children, depth + 1);
			source.Append(indent).AppendLine("}");
			return;
		}

		var declaration = element.Kind switch
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
			_ => throw new UnsupportedDiagramConstructException(element.Kind)
		};
		source.Append(indent).Append(declaration).Append(" \"").Append(label).Append("\" as ").AppendLine(alias);
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
			DiagramRelationshipKinds.Generalization or "Generalization" or "Inheritance" => (" --|> ", (string?)null),
			DiagramRelationshipKinds.Realization or "Realization" => (" ..|> ", (string?)null),
			DiagramRelationshipKinds.Dependency or "Dependency" or "Uses" or "Reference" => (" ..> ", (string?)null),
			DiagramRelationshipKinds.Containment or "Composition" => (" *-- ", (string?)null),
			"Aggregation" => (" o-- ", (string?)null),
			DiagramRelationshipKinds.ControlFlow or "Flow" => (" --> ", relationship.Guard),
			DiagramRelationshipKinds.ObjectFlow => (" --> ", "object flow"),
			DiagramRelationshipKinds.Message or "Message" => (" -> ", (string?)null),
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

	private static string Alias(string id)
		=> "raid_" + CanonicalJson.Sha256Hex(id)[..16];

	private static string EscapeLabel(string value)
		=> value.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal)
			.Replace("\r", string.Empty, StringComparison.Ordinal)
			.Replace("\n", "\\n", StringComparison.Ordinal);

}
