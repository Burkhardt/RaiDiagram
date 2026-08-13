using System.Text;
using System.Text.RegularExpressions;
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

public sealed partial class PlantUmlDiagramCompiler
{
	private static readonly HashSet<string> SupportedElements =
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

	private static readonly HashSet<string> SupportedRelationships =
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
		DiagramRelationshipKinds.Message
	];

	public DiagramCapabilityReport Validate(DiagramManifest manifest)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		manifest.Validate();
		var unsupported = manifest.Projection.Elements
			.Select(item => item.Kind)
			.Where(kind => !SupportedElements.Contains(kind))
			.Concat(manifest.Projection.Relationships
				.Select(item => item.Kind)
				.Where(kind => !SupportedRelationships.Contains(kind)))
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

		options ??= new PlantUmlCompileOptions();
		var aliases = manifest.Projection.Elements.ToDictionary(
			item => item.Id,
			item => Alias(item.Id),
			StringComparer.Ordinal);
		var children = manifest.Projection.Elements
			.Where(item => item.ParentId is not null)
			.GroupBy(item => item.ParentId!, StringComparer.Ordinal)
			.ToDictionary(group => group.Key, group => group.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

		var source = new StringBuilder();
		source.Append("@startuml ").AppendLine(Alias(manifest.Diagram.Id));
		AppendPresentation(source, manifest.Presentation, options);

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

	private static void AppendPresentation(
		StringBuilder source,
		DiagramPresentation presentation,
		PlantUmlCompileOptions options)
	{
		if (!string.IsNullOrWhiteSpace(presentation.Theme))
		{
			if (!SafeThemeName().IsMatch(presentation.Theme))
				throw new RaidSchemaException($"Theme name '{presentation.Theme}' is not a safe PlantUML theme identifier.");

			source.Append("!theme ").Append(presentation.Theme);
			if (options.LocalThemeRoot is not null)
			{
				if (!options.LocalThemeRoot.Exists())
					throw new RaiPathNotFoundException(
						$"The approved PlantUML theme root does not exist: {options.LocalThemeRoot.FullPath}",
						options.LocalThemeRoot.FullPath);
				var themeFile = new RaiFile(options.LocalThemeRoot, $"puml-theme-{presentation.Theme}.puml");
				if (!themeFile.Exists())
					throw new DiagramRenderingException($"PlantUML theme '{presentation.Theme}' was not found in the approved theme root.");
				source.Append(" from ").Append(EscapePreprocessorPath(options.LocalThemeRoot.FullPath));
			}
			source.AppendLine();
		}

		if (presentation.Handwritten)
			source.AppendLine("skinparam handwritten true");
		if (!string.IsNullOrWhiteSpace(presentation.FontName))
		{
			if (presentation.FontName.IndexOfAny(['\r', '\n']) >= 0)
				throw new RaidSchemaException("A PlantUML font name cannot contain a line break.");
			source.Append("skinparam defaultFontName \"")
				.Append(EscapeLabel(presentation.FontName))
				.AppendLine("\"");
		}
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
			DiagramRelationshipKinds.Include => (" ..> ", "<<include>>"),
			DiagramRelationshipKinds.Extend => (" ..> ", "<<extend>>"),
			DiagramRelationshipKinds.Association => (" -- ", (string?)null),
			DiagramRelationshipKinds.Attribute => (" --> ", "attribute"),
			DiagramRelationshipKinds.Generalization => (" --|> ", (string?)null),
			DiagramRelationshipKinds.Realization => (" ..|> ", (string?)null),
			DiagramRelationshipKinds.Dependency => (" ..> ", (string?)null),
			DiagramRelationshipKinds.Containment => (" *-- ", (string?)null),
			DiagramRelationshipKinds.ControlFlow => (" --> ", relationship.Guard),
			DiagramRelationshipKinds.ObjectFlow => (" --> ", "object flow"),
			DiagramRelationshipKinds.Message => (" -> ", (string?)null),
			_ => throw new UnsupportedDiagramConstructException(relationship.Kind)
		};
		var label = relationship.Label ?? defaultLabel;
		source.Append(aliases[relationship.SourceId]).Append(arrow).Append(aliases[relationship.TargetId]);
		if (!string.IsNullOrWhiteSpace(label))
			source.Append(" : ").Append(EscapeLabel(label));
		source.AppendLine();
	}

	private static string Alias(string id)
		=> "raid_" + CanonicalJson.Sha256Hex(id)[..16];

	private static string EscapeLabel(string value)
		=> value.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal)
			.Replace("\r", string.Empty, StringComparison.Ordinal)
			.Replace("\n", "\\n", StringComparison.Ordinal);

	private static string EscapePreprocessorPath(string path)
	{
		if (path.IndexOfAny(['\r', '\n', '\'', '"']) >= 0)
			throw new DiagramRenderingException("The approved PlantUML theme root contains unsupported quoting characters.");
		return path;
	}

	[GeneratedRegex("^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)]
	private static partial Regex SafeThemeName();
}
