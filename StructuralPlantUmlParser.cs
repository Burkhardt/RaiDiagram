using System.Text.RegularExpressions;

namespace RaiDiagram;

/// <summary>Strict structural import. Unsupported statements never become implicit classes.</summary>
internal sealed class StructuralPlantUmlParser
{
	private const string Identifier = "[A-Za-z_][A-Za-z0-9_.-]*";
	private const string Name = "(?:\"(?:[^\"\\\\]|\\\\.)*\"|" + Identifier + ")";
	private static readonly Regex Declaration = new(
		"^(?<abstract>abstract\\s+)?(?<kind>class|interface|enum|object|actor|usecase|node|cloud|database|component|artifact|folder|frame|package|rectangle)\\s+(?<name>" + Name + ")(?:\\s+as\\s+(?<alias>" + Identifier + "))?(?<tail>.*)$",
		RegexOptions.CultureInvariant);
	private static readonly Regex Link = new(
		"^(?<left>" + Name + ")\\s*(?<arrow><\\|--|--\\|>|\\.\\.\\|>|<--|-->|\\.\\.>|<\\.\\.|\\*--|o--|--)\\s*(?<right>" + Name + ")(?:\\s*:\\s*(?<label>.*))?$",
		RegexOptions.CultureInvariant);
	private readonly DiagramManifest manifest;
	private readonly string source;
	private readonly Dictionary<string, DiagramElement> aliases = new(StringComparer.Ordinal);
	private readonly Stack<DiagramElement> containers = new();
	private readonly List<(Match Match, int Line)> links = [];
	private DiagramElement? body;
	private int memberIndex;

	internal StructuralPlantUmlParser(DiagramManifest manifest, string source)
	{
		this.manifest = manifest;
		this.source = source;
	}

	internal void Parse(IReadOnlyList<string> lines)
	{
		for (var index = 0; index < lines.Count; index++)
		{
			var line = lines[index].Trim();
			var number = index + 1;
			if (line.Length == 0 || line.StartsWith("'", StringComparison.Ordinal)) continue;
			
			if (body is not null)
			{
				if (line == "@enduml") Fail("PUML001", number, "A declaration is missing its closing brace.");
				if (line == "}") { body = null; continue; }
				AddMember(body, line, number);
				continue;
			}
			if (line.StartsWith('@') || line.StartsWith("title ", StringComparison.Ordinal) || line == "allowmixing") continue;
			if (line == "}")
			{
				if (!containers.TryPop(out _)) Fail("PUML001", number, "Unexpected closing brace.");
				continue;
			}
			if (line.StartsWith("note ", StringComparison.Ordinal) || line == "note")
			{
				var attachment = Regex.Match(line, "^note (?:left|right|top|bottom) of (?<alias>" + Name + ")$");
				if (!attachment.Success && line != "note") Fail("PUML002", number, "Unsupported note syntax; use a multiline note attached to an alias.");
				var attachedId = attachment.Success ? Unquote(attachment.Groups["alias"].Value) : null;
				if (attachedId is not null && !aliases.ContainsKey(attachedId)) Fail("PUML004", number, "Note refers to an undeclared alias.");
				var start = number;
				var text = new List<string>();
				while (++index < lines.Count && lines[index].Trim() != "end note") text.Add(lines[index]);
				if (index == lines.Count) Fail("PUML001", start, "Note is missing 'end note'.");
				manifest.Annotations.Add(new DiagramAnnotation { Id = $"note-{start}", ElementId = attachedId, Text = string.Join('\n', text) });
				continue;
			}
			if (line.StartsWith('!')) Fail("PUML005", number, "Preprocessor directives require supported expansion before import.");
			if (line.StartsWith("skinparam ", StringComparison.Ordinal) || line.StartsWith("hide ", StringComparison.Ordinal)
				|| line.StartsWith("show ", StringComparison.Ordinal) || line.EndsWith(" direction", StringComparison.Ordinal))
			{
				if (line.Contains('{')) Fail("PUML002", number, "Style blocks are not yet supported by structural import.");
				manifest.Presentation.LayoutHints[$"plantuml.presentation.{number:D6}"] = line;
				continue;
			}
			CheckBalancedQuotes(line, number);
			var declaration = Declaration.Match(line);
			if (declaration.Success) { AddDeclaration(declaration, number); continue; }
			var link = Link.Match(line);
			if (link.Success) { links.Add((link, number)); continue; }
			Fail(line.Contains('{') || line.Contains('}') ? "PUML001" : "PUML002", number, "Unsupported or malformed structural statement; no partial model was imported.");
		}
		if (body is not null || containers.Count > 0) Fail("PUML001", lines.Count, "A declaration is missing its closing brace.");
		if (aliases.Count == 0) Fail("PUML003", 1, "No model declarations were found.");
		foreach (var (link, line) in links) AddLink(link, line);
		var kinds = aliases.Values.Select(e => e.Kind).ToHashSet(StringComparer.Ordinal);
		manifest.Diagram.Kind = kinds.Any(DiagramManifest.IsDeploymentContainer) ? DiagramKind.Deployment
			: kinds.Contains(DiagramElementKinds.Object) ? (kinds.Overlaps([DiagramElementKinds.Class, DiagramElementKinds.Interface]) ? DiagramKind.Mixed : DiagramKind.Object)
			: kinds.Contains(DiagramElementKinds.UseCase) ? DiagramKind.UseCase : DiagramKind.Class;
		if (manifest.Diagram.Kind == DiagramKind.Deployment || aliases.Values.Any(e => e.ObjectProperties.Count > 0) || manifest.Projection.Relationships.Any(r => r.Directed))
			manifest.SchemaVersion = DiagramManifest.ExtendedSchemaVersion;
	}

	private void AddDeclaration(Match match, int line)
	{
		var token = match.Groups["kind"].Value;
		var label = Unquote(match.Groups["name"].Value);
		var alias = match.Groups["alias"].Success ? match.Groups["alias"].Value : label;
		if (aliases.ContainsKey(alias)) Fail("PUML004", line, $"Duplicate alias '{alias}'. Use distinct aliases.");
		var tail = match.Groups["tail"].Value.Trim();
		if (tail.StartsWith("<<", StringComparison.Ordinal)) Fail("PUML002", line, "Declaration stereotypes are not supported by this importer yet.");
		var isContainer = token is "node" or "cloud" or "database" or "component" or "artifact" or "folder" or "frame" or "package" or "rectangle";
		var element = new DiagramElement
		{
			Id = alias, DisplayName = label, ParentId = containers.TryPeek(out var parent) ? parent.Id : null,
			Kind = token switch
			{
				"class" => DiagramElementKinds.Class, "interface" => DiagramElementKinds.Interface,
				"enum" => DiagramElementKinds.Enumeration, "object" => DiagramElementKinds.Object,
				"actor" => DiagramElementKinds.Role, "usecase" => DiagramElementKinds.UseCase,
				"node" => DiagramElementKinds.Node, "cloud" => DiagramElementKinds.Cloud,
				"database" => DiagramElementKinds.Database, "component" => DiagramElementKinds.Component,
				"artifact" => DiagramElementKinds.Artifact, "folder" => DiagramElementKinds.Folder,
				"frame" => DiagramElementKinds.BoundaryFrame, _ => DiagramElementKinds.Frame
			}
		};
		if (match.Groups["abstract"].Success) element.RelevantFacts["abstract"] = ModelFactValue.Boolean(true);
		aliases.Add(alias, element);
		manifest.Projection.Elements.Add(element);
		if (tail.Length == 0) return;
		if (!tail.StartsWith('{')) Fail("PUML002", line, "Unsupported declaration suffix.");
		var contents = tail[1..].Trim();
		memberIndex = 0;
		if (contents.EndsWith('}'))
		{
			contents = contents[..^1].Trim();
			if (contents.Length > 0)
			{
				if (isContainer) Fail("PUML002", line, "Inline container declarations must be written on separate lines.");
				AddMember(element, contents, line);
			}
			return;
		}
		if (contents.Length > 0) Fail("PUML001", line, "Inline body is missing its closing brace.");
		if (isContainer) containers.Push(element); else body = element;
	}

	private void AddMember(DiagramElement element, string text, int line)
	{
		if (element.Kind == DiagramElementKinds.Object)
		{
			var equals = text.IndexOf('=');
			if (equals <= 0) Fail("PUML001", line, "Object slots require 'name = value'.");
			var name = text[..equals].Trim();
			var value = text[(equals + 1)..].Trim();
			if (element.ObjectProperties.Any(p => p.Name == name)) Fail("PUML004", line, $"Duplicate object slot '{name}'.");
			CheckBalancedValue(value, line);
			element.ObjectProperties.Add(new DiagramObjectProperty { Name = name, Value = value });
		}
		else if (element.Kind is DiagramElementKinds.Class or DiagramElementKinds.Interface or DiagramElementKinds.Enumeration)
		{
			var kind = text.Contains('(') ? "method" : "attribute";
			element.RelevantFacts[$"raidiagram.class.{kind}.{++memberIndex:D4}"] = ModelFactValue.String(text);
		}
		else Fail("PUML002", line, "This declaration does not support members.");
	}

	private void CheckBalancedValue(string value, int line)
	{
		var stack = new Stack<char>();
		var quote = '\0';
		for (var i = 0; i < value.Length; i++)
		{
			var c = value[i];
			if (quote != '\0')
			{
				if (c == '\\') { i++; continue; }
				if (c == quote) quote = '\0';
				continue;
			}
			if (c is '\'' or '"' && (i == 0 || !char.IsLetterOrDigit(value[i - 1]))) { quote = c; continue; }
			if (c is '{' or '[' or '(') stack.Push(c);
			if (c is '}' or ']' or ')')
			{
				if (!stack.TryPop(out var open) || (open, c) is not (('{', '}') or ('[', ']') or ('(', ')')))
					Fail("PUML001", line, "Unbalanced slot value delimiters.");
			}
		}
		if (quote != '\0' || stack.Count > 0) Fail("PUML001", line, "Unclosed slot value delimiter or quote.");
	}

	private void AddLink(Match match, int line)
	{
		var left = Unquote(match.Groups["left"].Value);
		var right = Unquote(match.Groups["right"].Value);
		if (!aliases.ContainsKey(left) || !aliases.ContainsKey(right)) Fail("PUML004", line, "Relationship refers to an undeclared alias.");
		var arrow = match.Groups["arrow"].Value;
		var label = match.Groups["label"].Success ? match.Groups["label"].Value : null;
		var kind = arrow switch
		{
			"<|--" or "--|>" => DiagramRelationshipKinds.Generalization,
			"..|>" => DiagramRelationshipKinds.Realization,
			"..>" or "<.." => DiagramRelationshipKinds.Dependency,
			"*--" => DiagramRelationshipKinds.Containment,
			"o--" => DiagramRelationshipKinds.Aggregation,
			_ => DiagramRelationshipKinds.Association
		};
		if (label?.StartsWith("<<", StringComparison.Ordinal) == true)
		{
			if (label == "<<include>>") kind = DiagramRelationshipKinds.Include;
			else if (label == "<<extend>>") kind = DiagramRelationshipKinds.Extend;
			else if (label is not ("<<TCP>>" or "<<http>>" or "<<DB Connect>>"))
				Fail("PUML002", line, "Unknown relationship stereotype.");
		}
		// Generalization uses parent -> child; realization uses implementing class -> interface.
		if (arrow is "<--" or "<.." or "--|>") (left, right) = (right, left);
		manifest.Projection.Relationships.Add(new DiagramRelationship
		{
			Id = $"relationship-{manifest.Projection.Relationships.Count + 1:D6}", Kind = kind,
			Directed = kind == DiagramRelationshipKinds.Association && arrow is "-->" or "<--",
			SourceId = left, TargetId = right, Label = label
		});
	}

	private void CheckBalancedQuotes(string text, int line)
	{
		var quoted = false;
		for (var i = 0; i < text.Length; i++)
		{
			if (text[i] == '\\') { i++; continue; }
			if (text[i] == '"') quoted = !quoted;
		}
		if (quoted) Fail("PUML001", line, "Unclosed quoted name.");
	}

	private static string Unquote(string value) => value.StartsWith('"') ? value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal) : value;
	private void Fail(string code, int line, string message) => throw new PlantUmlImportException(new(code, source, line, 1, message));
}
