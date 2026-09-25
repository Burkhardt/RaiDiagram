using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RaiDiagram;

/// <summary>A model-import boundary that is independent of the CLI and filesystem.</summary>
public interface IModelImporter
{
	string FormatName { get; }
	bool CanImport(string filePath);
	RaidDiagramModel Import(TextReader reader);
}

/// <summary>The validated, immutable result returned by a model importer.</summary>
public sealed class RaidDiagramModel
{
	private RaidDiagramModel(DiagramModel model)
	{
		Manifest = model.Manifest;
		SemanticHash = model.SemanticHash;
	}

	public DiagramManifest Manifest { get; }
	public string SemanticHash { get; }

	public static RaidDiagramModel FromManifest(DiagramManifest manifest)
		=> new(DiagramModel.FromManifest(manifest));
}

/// <summary>Imports the phase-one PlantUML activity and class syntax defined by CR036.</summary>
public sealed class PlantUmlModelImporter : IModelImporter
{
	private static readonly Regex StartUmlPattern = new(
		"^@startuml(?:\\s+(?<name>[^\\s]+))?",
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
	private static readonly Regex IfPattern = new(
		"^if\\s*\\((?<condition>.*)\\)\\s*then(?:\\s*\\((?<guard>.*)\\))?",
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
	private static readonly Regex ElsePattern = new(
		"^else(?:\\s*\\((?<guard>.*)\\))?",
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
	private static readonly Regex ClassPattern = new(
		"^(?<abstract>abstract\\s+)?(?<kind>class|interface)\\s+(?<name>\"[^\"]+\"|[A-Za-z_][A-Za-z0-9_.-]*)(?:\\s+as\\s+(?<alias>[A-Za-z_][A-Za-z0-9_.-]*))?",
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
	private static readonly Regex RelationshipPattern = new(
		"^(?<left>\"[^\"]+\"|[A-Za-z_][A-Za-z0-9_.-]*)\\s*(?<arrow><\\|--|-->|\\*--|o--)\\s*(?<right>\"[^\"]+\"|[A-Za-z_][A-Za-z0-9_.-]*)(?:\\s*:\\s*(?<label>.*))?$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public string FormatName => "PlantUML";

	public bool CanImport(string filePath)
		=> !string.IsNullOrWhiteSpace(filePath)
			&& filePath.EndsWith(".puml", StringComparison.OrdinalIgnoreCase);

	public RaidDiagramModel Import(TextReader reader)
	{
		ArgumentNullException.ThrowIfNull(reader);
		var source = reader.ReadToEnd();
		if (string.IsNullOrWhiteSpace(source))
			throw new RaidSchemaException("A PlantUML source cannot be empty.");
		if (PlantUmlRoundTripMetadata.TryRead(source, out var roundTripManifest))
			return RaidDiagramModel.FromManifest(roundTripManifest);

		var lines = NormalizeLines(source);
		var name = ReadDiagramName(lines) ?? "ImportedDiagram";
		return LooksLikeClassDiagram(lines)
			? ImportClass(lines, name, source)
			: ImportActivity(lines, name, source);
	}

	private static RaidDiagramModel ImportActivity(
		IReadOnlyList<string> lines,
		string diagramId,
		string source)
	{
		var title = ReadTitle(lines) ?? diagramId;
		var manifest = CreateManifest(diagramId, title, DiagramKind.Activity, source);
		var currentTails = new List<FlowTail>();
		var conditions = new Stack<ConditionalFrame>();
		var forks = new Stack<ForkFrame>();
		var currentLane = string.Empty;
		var activityNumber = 0;
		var decisionNumber = 0;
		var relationshipNumber = 0;
		var noteBlock = false;
		var activityText = new StringBuilder();

		void AddRelationship(FlowTail tail, string target)
		{
			manifest.Projection.Relationships.Add(new DiagramRelationship
			{
				Id = $"flow-{++relationshipNumber:D4}",
				Kind = DiagramRelationshipKinds.ControlFlow,
				SourceId = tail.ElementId,
				TargetId = target,
				Guard = tail.Guard
			});
		}

		DiagramElement AddActivity(string displayName, string token = "activity")
		{
			var id = $"activity-{++activityNumber:D4}";
			var element = new DiagramElement
			{
				Id = id,
				Kind = DiagramElementKinds.Activity,
				DisplayName = displayName
			};
			element.RelevantFacts["plantUmlToken"] = ModelFactValue.String(token);
			if (!string.IsNullOrWhiteSpace(currentLane))
				element.RelevantFacts["swimlane"] = ModelFactValue.String(currentLane);
			manifest.Projection.Elements.Add(element);
			foreach (var tail in currentTails)
				AddRelationship(tail, id);
			currentTails = [new FlowTail(id, null)];
			return element;
		}

		foreach (var sourceLine in lines)
		{
			var line = sourceLine.Trim();
			if (noteBlock)
			{
				if (line.Equals("end note", StringComparison.OrdinalIgnoreCase))
					noteBlock = false;
				continue;
			}
			if (line.StartsWith("note ", StringComparison.OrdinalIgnoreCase)
				|| line.Equals("note", StringComparison.OrdinalIgnoreCase))
			{
				noteBlock = true;
				continue;
			}
			if (activityText.Length > 0)
			{
				activityText.Append(' ').Append(line);
				if (line.EndsWith(';'))
				{
					AddActivity(CleanActivity(activityText.ToString()));
					activityText.Clear();
				}
				continue;
			}
			if (line.StartsWith(':'))
			{
				if (line.EndsWith(';'))
					AddActivity(CleanActivity(line));
				else
					activityText.Append(line);
				continue;
			}
			if (line.Length >= 3 && line[0] == '|' && line[^1] == '|')
			{
				currentLane = line[1..^1].Trim();
				continue;
			}
			if (line.Equals("start", StringComparison.OrdinalIgnoreCase))
			{
				AddActivity("Start", "start");
				continue;
			}
			if (line.Equals("stop", StringComparison.OrdinalIgnoreCase)
				|| line.Equals("detach", StringComparison.OrdinalIgnoreCase))
			{
				AddActivity(line.Equals("stop", StringComparison.OrdinalIgnoreCase) ? "Stop" : "Detach",
					line.ToLowerInvariant());
				currentTails = [];
				continue;
			}

			var conditional = IfPattern.Match(line);
			if (conditional.Success)
			{
				var id = $"decision-{++decisionNumber:D4}";
				manifest.Projection.Elements.Add(new DiagramElement
				{
					Id = id,
					Kind = DiagramElementKinds.Decision,
					DisplayName = conditional.Groups["condition"].Value.Trim()
				});
				foreach (var tail in currentTails)
					AddRelationship(tail, id);
				var trueGuard = ValueOr(conditional.Groups["guard"].Value, "yes");
				conditions.Push(new ConditionalFrame(id, trueGuard));
				currentTails = [new FlowTail(id, trueGuard)];
				continue;
			}

			var otherwise = ElsePattern.Match(line);
			if (otherwise.Success && conditions.Count > 0)
			{
				var frame = conditions.Pop();
				frame.TrueTails.AddRange(currentTails);
				frame.FalseGuard = ValueOr(otherwise.Groups["guard"].Value, "no");
				conditions.Push(frame);
				currentTails = [new FlowTail(frame.DecisionId, frame.FalseGuard)];
				continue;
			}
			if (line.Equals("endif", StringComparison.OrdinalIgnoreCase) && conditions.Count > 0)
			{
				var frame = conditions.Pop();
				frame.TrueTails.AddRange(currentTails);
				currentTails = frame.TrueTails;
				continue;
			}
			if (line.Equals("fork", StringComparison.OrdinalIgnoreCase))
			{
				forks.Push(new ForkFrame([.. currentTails]));
				continue;
			}
			if (line.Equals("fork again", StringComparison.OrdinalIgnoreCase) && forks.Count > 0)
			{
				var frame = forks.Pop();
				frame.BranchTails.AddRange(currentTails);
				forks.Push(frame);
				currentTails = [.. frame.OriginTails];
				continue;
			}
			if (line.Equals("end fork", StringComparison.OrdinalIgnoreCase) && forks.Count > 0)
			{
				var frame = forks.Pop();
				frame.BranchTails.AddRange(currentTails);
				currentTails = frame.BranchTails;
			}
		}

		if (activityText.Length > 0)
			throw new RaidSchemaException("PlantUML activity text is missing its terminating semicolon.");
		if (conditions.Count > 0)
			throw new RaidSchemaException("PlantUML activity diagram contains an unterminated if block.");
		if (forks.Count > 0)
			throw new RaidSchemaException("PlantUML activity diagram contains an unterminated fork block.");

		DeterministicDiagramCanvas.Ensure(manifest);
		return RaidDiagramModel.FromManifest(manifest);
	}

	private static RaidDiagramModel ImportClass(
		IReadOnlyList<string> lines,
		string diagramId,
		string source)
	{
		var manifest = CreateManifest(diagramId, ReadTitle(lines) ?? diagramId, DiagramKind.Class, source);
		var elementsByName = new Dictionary<string, DiagramElement>(StringComparer.Ordinal);
		var relationshipNumber = 0;
		DiagramElement? openClass = null;
		var memberNumber = 0;

		DiagramElement GetOrAdd(string rawName, string kind = DiagramElementKinds.Class)
		{
			var name = Unquote(rawName);
			if (elementsByName.TryGetValue(name, out var existing))
				return existing;
			var element = new DiagramElement
			{
				Id = UniqueElementId(name, elementsByName.Values.Select(item => item.Id)),
				Kind = kind,
				DisplayName = name
			};
			elementsByName[name] = element;
			manifest.Projection.Elements.Add(element);
			return element;
		}

		foreach (var sourceLine in lines)
		{
			var line = sourceLine.Trim();
			if (line.Length == 0 || line.StartsWith("'") || line.StartsWith("@")
				|| line.StartsWith("title ", StringComparison.OrdinalIgnoreCase)
				|| line.StartsWith("skinparam ", StringComparison.OrdinalIgnoreCase))
				continue;

			if (openClass is not null)
			{
				if (line.StartsWith('}'))
				{
					openClass = null;
					continue;
				}
				var member = line.TrimEnd(';').Trim();
				if (member.Length > 0)
				{
					var memberKind = member.Contains('(') ? "method" : "attribute";
					openClass.RelevantFacts[$"plantUml.{memberKind}.{++memberNumber:D4}"] =
						ModelFactValue.String(member);
				}
				continue;
			}

			var declaration = ClassPattern.Match(line);
			if (declaration.Success)
			{
				var displayName = Unquote(declaration.Groups["name"].Value);
				var lookupName = declaration.Groups["alias"].Success
					? declaration.Groups["alias"].Value
					: displayName;
				var kind = declaration.Groups["kind"].Value.Equals("interface", StringComparison.OrdinalIgnoreCase)
					? DiagramElementKinds.Interface
					: DiagramElementKinds.Class;
				var element = GetOrAdd(lookupName, kind);
				element.DisplayName = displayName;
				element.Kind = kind;
				if (declaration.Groups["abstract"].Success)
					element.RelevantFacts["abstract"] = ModelFactValue.Boolean(true);
				if (line.Contains('{') && !line.Contains('}'))
				{
					openClass = element;
					memberNumber = 0;
				}
				continue;
			}

			var relationshipMatch = RelationshipPattern.Match(line);
			if (!relationshipMatch.Success)
				continue;
			var left = GetOrAdd(relationshipMatch.Groups["left"].Value);
			var right = GetOrAdd(relationshipMatch.Groups["right"].Value);
			var arrow = relationshipMatch.Groups["arrow"].Value;
			manifest.Projection.Relationships.Add(new DiagramRelationship
			{
				Id = $"relationship-{++relationshipNumber:D4}",
				Kind = arrow switch
				{
					"<|--" => DiagramRelationshipKinds.Generalization,
					"*--" => DiagramRelationshipKinds.Containment,
					"o--" => DiagramRelationshipKinds.Aggregation,
					_ => DiagramRelationshipKinds.Association
				},
				SourceId = left.Id,
				TargetId = right.Id,
				Label = EmptyToNull(relationshipMatch.Groups["label"].Value)
			});
		}

		if (openClass is not null)
			throw new RaidSchemaException($"PlantUML class '{openClass.DisplayName}' is missing its closing brace.");
		if (manifest.Projection.Elements.Count == 0)
			throw new RaidSchemaException("No supported PlantUML class declarations were found.");

		DeterministicDiagramCanvas.Ensure(manifest);
		return RaidDiagramModel.FromManifest(manifest);
	}

	private static DiagramManifest CreateManifest(
		string diagramId,
		string title,
		DiagramKind kind,
		string source)
	{
		var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
		return new DiagramManifest
		{
			Diagram = new DiagramIdentity { Id = diagramId, Title = title, Kind = kind },
			Model = new DiagramModelIdentity
			{
				ProviderScheme = "plantuml",
				ModelId = diagramId,
				CapturedRevision = $"sha256:{hash}"
			}
		};
	}

	private static IReadOnlyList<string> NormalizeLines(string source)
		=> source.Replace("\r\n", "\n", StringComparison.Ordinal)
			.Replace('\r', '\n')
			.Split('\n');

	private static string? ReadDiagramName(IEnumerable<string> lines)
	{
		foreach (var line in lines)
		{
			var match = StartUmlPattern.Match(line.Trim());
			if (match.Success && match.Groups["name"].Success)
				return match.Groups["name"].Value.Trim();
		}
		return null;
	}

	private static string? ReadTitle(IEnumerable<string> lines)
		=> lines.Select(line => line.Trim())
			.FirstOrDefault(line => line.StartsWith("title ", StringComparison.OrdinalIgnoreCase))?[6..].Trim();

	private static bool LooksLikeClassDiagram(IEnumerable<string> lines)
		=> lines.Any(line => ClassPattern.IsMatch(line.Trim()) || RelationshipPattern.IsMatch(line.Trim()));

	private static string CleanActivity(string value)
		=> value.Trim().TrimStart(':').TrimEnd(';').Trim();

	private static string ValueOr(string value, string fallback)
		=> string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

	private static string? EmptyToNull(string value)
		=> string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private static string Unquote(string value)
	{
		var trimmed = value.Trim();
		return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"'
			? trimmed[1..^1]
			: trimmed;
	}

	private static string UniqueElementId(string name, IEnumerable<string> existingIds)
	{
		var baseId = Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
		if (baseId.Length == 0)
			baseId = "element";
		var used = existingIds.ToHashSet(StringComparer.Ordinal);
		var candidate = baseId;
		for (var suffix = 2; used.Contains(candidate); suffix++)
			candidate = $"{baseId}-{suffix}";
		return candidate;
	}

	private sealed record FlowTail(string ElementId, string? Guard);

	private sealed class ConditionalFrame(string decisionId, string trueGuard)
	{
		public string DecisionId { get; } = decisionId;
		public string TrueGuard { get; } = trueGuard;
		public string FalseGuard { get; set; } = "no";
		public List<FlowTail> TrueTails { get; } = [];
	}

	private sealed class ForkFrame(List<FlowTail> originTails)
	{
		public List<FlowTail> OriginTails { get; } = originTails;
		public List<FlowTail> BranchTails { get; } = [];
	}
}
