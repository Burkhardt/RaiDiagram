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
	private RaidDiagramModel(DiagramModel model, IReadOnlyList<PlantUmlDiagnostic>? diagnostics = null)
	{
		Manifest = model.Manifest;
		SemanticHash = model.SemanticHash;
		Diagnostics = diagnostics ?? [];
	}

	public DiagramManifest Manifest { get; }
	public string SemanticHash { get; }
	public IReadOnlyList<PlantUmlDiagnostic> Diagnostics { get; }

	public static RaidDiagramModel FromManifest(DiagramManifest manifest, IReadOnlyList<PlantUmlDiagnostic>? diagnostics = null)
		=> new(DiagramModel.FromManifest(manifest), diagnostics);
}

/// <summary>Imports the phase-one PlantUML activity and class syntax defined by CR036.</summary>
public sealed class PlantUmlModelImporter : IModelImporter
{
	private static readonly Regex StartUmlPattern = new(
		"^@startuml(?:\\s+(?<name>[^\\s]+))?$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
	private static readonly Regex IfPattern = new(
		"^if\\s*\\((?<condition>.*)\\)\\s*then(?:\\s*\\((?<guard>.*)\\))?$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
	private static readonly Regex ElsePattern = new(
		"^else(?:\\s*\\((?<guard>.*)\\))?$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

	public string FormatName => "PlantUML";

	public bool CanImport(string filePath)
		=> !string.IsNullOrWhiteSpace(filePath)
			&& filePath.EndsWith(".puml", StringComparison.OrdinalIgnoreCase);

	public RaidDiagramModel Import(TextReader reader)
		=> Import(reader, "<input>");

	public RaidDiagramModel Import(TextReader reader, string sourceName)
	{
		ArgumentNullException.ThrowIfNull(reader);
		var source = reader.ReadToEnd();
		if (string.IsNullOrWhiteSpace(source))
			throw new PlantUmlImportException(new("PUML003", sourceName, 1, 1, "A PlantUML source cannot be empty."));
		var lines = NormalizeLines(source);
		var significant = lines.Select((text, index) => (Text: text.Trim(), Line: index + 1))
			.Where(item => item.Text.Length > 0 && !item.Text.StartsWith("'", StringComparison.Ordinal)).ToArray();
		if (significant.Length == 0)
			throw new PlantUmlImportException(new("PUML003", sourceName, 1, 1, "No standalone diagram was supplied."));
		if (!significant.Any(item => item.Text.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase)))
			throw new PlantUmlImportException(new("PUML003", sourceName, significant[0].Line, 1, "This is not a standalone diagram; an @startuml / @enduml envelope is required."));
		if (!StartUmlPattern.IsMatch(significant[0].Text) || significant[^1].Text != "@enduml"
			|| significant.Count(item => item.Text.StartsWith("@startuml", StringComparison.Ordinal)) != 1
			|| significant.Count(item => item.Text == "@enduml") != 1)
			throw new PlantUmlImportException(new("PUML001", sourceName, significant[0].Line, 1,
				"Expected exactly one @startuml / @enduml diagram envelope."));
		if (PlantUmlRoundTripMetadata.TryRead(source, out var roundTripManifest))
			return RaidDiagramModel.FromManifest(roundTripManifest);

		var name = ReadDiagramName(lines) ?? "ImportedDiagram";
		var activity = significant.Any(item => item.Text == "start")
			&& !significant.Any(item => System.Text.RegularExpressions.Regex.IsMatch(item.Text, "^(object|class|interface|node|component|actor|usecase) "));
		if (activity)
		{
			ValidateActivitySyntax(lines, sourceName);
			return ImportActivity(lines, name, source);
		}
		var manifest = CreateManifest(name, ReadTitle(lines) ?? name, DiagramKind.Mixed, source);
		new StructuralPlantUmlParser(manifest, sourceName).Parse(lines);
		DeterministicDiagramCanvas.Ensure(manifest);
		return RaidDiagramModel.FromManifest(manifest, manifest.Presentation.LayoutHints
			.Where(h => h.Key.StartsWith("plantuml.presentation.", StringComparison.Ordinal))
			.Select(h => new PlantUmlDiagnostic("PUML101", sourceName, int.Parse(h.Key[(h.Key.LastIndexOf('.') + 1)..]), 1,
				"Presentation directive retained but not applied by the built-in canvas renderer: " + h.Value)).ToArray());
	}

	private static void ValidateActivitySyntax(IReadOnlyList<string> lines, string sourceName)
	{
		var blocks = new Stack<(string Kind, bool HasElse)>();
		var note = false;
		var action = false;
		for (var i = 0; i < lines.Count; i++)
		{
			var text = lines[i].Trim();
			if (text.Length == 0 || text.StartsWith("'", StringComparison.Ordinal)) continue;
			if (note) { if (text == "end note") note = false; continue; }
			if (text.StartsWith("note ", StringComparison.Ordinal)) { note = true; continue; }
			if (action || text.StartsWith(':')) { action = !text.EndsWith(';'); continue; }
			if (IfPattern.IsMatch(text)) { blocks.Push(("if", false)); continue; }
			if (text == "fork") { blocks.Push(("fork", false)); continue; }
			if (text is "endif" or "end fork" or "fork again" || ElsePattern.IsMatch(text))
			{
				var expected = text.Contains("fork", StringComparison.Ordinal) ? "fork" : "if";
				if (!blocks.TryPeek(out var block) || block.Kind != expected || (ElsePattern.IsMatch(text) && block.HasElse))
					throw new PlantUmlImportException(new("PUML001", sourceName, i + 1, 1, "Unmatched or repeated activity branch delimiter."));
				if (text is "endif" or "end fork") blocks.Pop();
				else if (ElsePattern.IsMatch(text)) { blocks.Pop(); blocks.Push(("if", true)); }
				continue;
			}
			if (text.StartsWith('@') || text.StartsWith("title ", StringComparison.Ordinal)
				|| (text.StartsWith("skinparam ", StringComparison.Ordinal) && !text.Contains('{'))
				|| (text.StartsWith('|') && text.EndsWith('|'))
				|| text is "start" or "stop" or "detach" or "endif" or "fork" or "fork again" or "end fork"
				|| IfPattern.IsMatch(text) || ElsePattern.IsMatch(text)) continue;
			throw new PlantUmlImportException(new(text.StartsWith('!') ? "PUML005" : "PUML002", sourceName, i + 1, 1,
				"Unsupported activity statement; no partial model was imported."));
		}
		if (note || action || blocks.Count > 0) throw new PlantUmlImportException(new("PUML001", sourceName, lines.Count, 1,
			"Unterminated activity or note."));
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

	private static string CleanActivity(string value)
		=> value.Trim().TrimStart(':').TrimEnd(';').Trim();

	private static string ValueOr(string value, string fallback)
		=> string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

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
