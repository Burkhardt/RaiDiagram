using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OsLib;
using RaiImage;

namespace RaiDiagram;

public enum PumlStyleProperty
{
	FontName,
	FontColor,
	FontSize,
	FontStyle,
	BackgroundColor,
	HyperLinkColor,
	LineColor,
	LineThickness,
	LineStyle,
	Padding,
	Margin,
	RoundCorner,
	MinimumWidth,
	WordWrap,
	HorizontalAlignment
}

/// <summary>A deterministic, programmable PlantUML CSS-like style sheet.</summary>
public sealed partial class PumlStyleSheet
{
	private readonly SortedDictionary<string, SortedDictionary<string, string>> rules =
		new(StringComparer.Ordinal);

	public PumlStyleSheet Set(string selector, PumlStyleProperty property, string value)
		=> Set(selector, property.ToString(), value);

	public PumlStyleSheet Set(string selector, string propertyName, string value)
	{
		var normalizedSelector = ValidateSelector(selector);
		var normalizedProperty = ValidateProperty(propertyName);
		var normalizedValue = ValidateValue(value);
		if (!rules.TryGetValue(normalizedSelector, out var properties))
		{
			properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
			rules.Add(normalizedSelector, properties);
		}
		properties[normalizedProperty] = normalizedValue;
		return this;
	}

	public PumlStyleSheet Merge(PumlStyleSheet? overlay)
	{
		var merged = Clone();
		if (overlay is null)
			return merged;
		foreach (var rule in overlay.rules)
			foreach (var property in rule.Value)
				merged.Set(rule.Key, property.Key, property.Value);
		return merged;
	}

	public PumlStyleSheet Clone()
	{
		var clone = new PumlStyleSheet();
		foreach (var rule in rules)
			foreach (var property in rule.Value)
				clone.Set(rule.Key, property.Key, property.Value);
		return clone;
	}

	public string ToPlantUml()
	{
		if (rules.Count == 0)
			return string.Empty;
		var tree = new StyleNode();
		foreach (var rule in rules)
		{
			var node = tree;
			foreach (var selectorPart in rule.Key.Split(' '))
			{
				if (!node.Children.TryGetValue(selectorPart, out var child))
				{
					child = new StyleNode();
					node.Children.Add(selectorPart, child);
				}
				node = child;
			}
			foreach (var property in rule.Value)
				node.Properties[property.Key] = property.Value;
		}
		var source = new StringBuilder();
		source.AppendLine("<style>");
		AppendNodes(source, tree, 0);
		source.AppendLine("</style>");
		return source.ToString();
	}

	private static void AppendNodes(StringBuilder source, StyleNode parent, int depth)
	{
		foreach (var child in parent.Children)
		{
			var indent = new string(' ', depth * 2);
			source.Append(indent).Append(child.Key).AppendLine(" {");
			foreach (var property in child.Value.Properties)
				source.Append(indent).Append("  ").Append(property.Key).Append(' ').AppendLine(property.Value);
			AppendNodes(source, child.Value, depth + 1);
			source.Append(indent).AppendLine("}");
		}
	}

	private sealed class StyleNode
	{
		public SortedDictionary<string, StyleNode> Children { get; } = new(StringComparer.Ordinal);
		public SortedDictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);
	}

	private static string ValidateSelector(string selector)
	{
		if (string.IsNullOrWhiteSpace(selector))
			throw new RaidSchemaException("A PlantUML style selector is required.");
		var parts = selector.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length == 0 || parts.Any(part => !SafeSelectorPart().IsMatch(part)))
			throw new RaidSchemaException($"PlantUML style selector '{selector}' is not safe.");
		return string.Join(' ', parts);
	}

	private static string ValidateProperty(string propertyName)
	{
		if (string.IsNullOrWhiteSpace(propertyName) || !SafeProperty().IsMatch(propertyName))
			throw new RaidSchemaException($"PlantUML style property '{propertyName}' is not safe.");
		return propertyName;
	}

	private static string ValidateValue(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['\r', '\n', '{', '}', '<', '>', ';']) >= 0)
			throw new RaidSchemaException("A PlantUML style value is empty or contains control syntax.");
		return value.Trim();
	}

	[GeneratedRegex("^\\.?[A-Za-z][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
	private static partial Regex SafeSelectorPart();

	[GeneratedRegex("^[A-Za-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
	private static partial Regex SafeProperty();
}

/// <summary>Common and diagram-kind-specific style layers.</summary>
public sealed class PumlStyleCatalog
{
	private readonly Dictionary<DiagramKind, PumlStyleSheet> byDiagramKind = [];
	private readonly List<PumlStyleFile> commonFiles = [];
	private readonly Dictionary<DiagramKind, List<PumlStyleFile>> filesByDiagramKind = [];

	public PumlStyleSheet Common { get; } = new();

	public PumlStyleSheet For(DiagramKind kind)
	{
		if (!byDiagramKind.TryGetValue(kind, out var sheet))
		{
			sheet = new PumlStyleSheet();
			byDiagramKind.Add(kind, sheet);
		}
		return sheet;
	}

	public PumlStyleSheet Resolve(DiagramKind kind)
		=> Common.Merge(byDiagramKind.TryGetValue(kind, out var sheet) ? sheet : null);

	public PumlStyleCatalog Add(PumlStyleFile styleFile)
	{
		ArgumentNullException.ThrowIfNull(styleFile);
		commonFiles.Add(styleFile);
		return this;
	}

	public PumlStyleCatalog Add(DiagramKind kind, PumlStyleFile styleFile)
	{
		ArgumentNullException.ThrowIfNull(styleFile);
		if (!filesByDiagramKind.TryGetValue(kind, out var files))
		{
			files = [];
			filesByDiagramKind.Add(kind, files);
		}
		files.Add(styleFile);
		return this;
	}

	public IReadOnlyList<PumlStyleSource> ResolveFiles(DiagramKind kind)
	{
		var files = commonFiles.Concat(
			filesByDiagramKind.TryGetValue(kind, out var kindFiles) ? kindFiles : []);
		return files.Select(file =>
		{
			if (!file.Exists())
				throw new RaiPathNotFoundException(
					$"The PlantUML style file does not exist: {file.FullName}",
					file.FullName);
			return new PumlStyleSource(file.NameWithExtension, file.ReadAllText());
		}).ToArray();
	}
}

public sealed record PumlStyleSource(
	string Name,
	string Content,
	string? Subscriber = null,
	string? ContentHash = null);

/// <summary>The complete content supplied to PlantUML through <c>-config</c>.</summary>
public sealed partial class PumlRenderConfiguration
{
	public DiagramKind DiagramKind { get; init; }
	public string? ProfileId { get; init; }
	public string? Theme { get; init; }
	public RaiPath? LocalThemeRoot { get; init; }
	public bool Handwritten { get; init; }
	public IReadOnlyList<PumlStyleSource> StyleSources { get; init; } = [];
	public PumlStyleSheet Styles { get; init; } = new();

	public string ToPlantUml()
	{
		var source = new StringBuilder();
		source.Append("' RaiDiagram resolved config for ").AppendLine(DiagramKind.ToString());
		if (!string.IsNullOrWhiteSpace(ProfileId))
		{
			if (ProfileId.IndexOfAny(['\r', '\n']) >= 0)
				throw new RaidSchemaException("A PlantUML style profile id cannot contain a line break.");
			source.Append("' style-profile: ").AppendLine(ProfileId);
		}
		if (!string.IsNullOrWhiteSpace(Theme))
		{
			if (!SafeThemeName().IsMatch(Theme))
				throw new RaidSchemaException($"Theme name '{Theme}' is not a safe PlantUML theme identifier.");
			source.Append("!theme ").Append(Theme);
			if (LocalThemeRoot is not null)
			{
				if (!LocalThemeRoot.Exists())
					throw new RaiPathNotFoundException(
						$"The approved PlantUML theme root does not exist: {LocalThemeRoot.FullPath}",
						LocalThemeRoot.FullPath);
				var themeFile = new PumlThemeFile(LocalThemeRoot, Theme);
				if (!themeFile.Exists())
					throw new DiagramRenderingException(
						$"PlantUML theme '{Theme}' was not found in the approved theme root.");
				source.Append(" from ").Append(EscapePreprocessorPath(LocalThemeRoot.FullPath));
			}
			source.AppendLine();
		}
		if (Handwritten)
			source.AppendLine("!option handwritten true");
		foreach (var styleSource in StyleSources)
		{
			if (string.IsNullOrWhiteSpace(styleSource.Name)
				|| styleSource.Name.IndexOfAny(['\r', '\n']) >= 0
				|| styleSource.Subscriber?.IndexOfAny(['\r', '\n']) >= 0
				|| (styleSource.ContentHash is { Length: > 0 }
					&& (styleSource.ContentHash.Length != 64
						|| styleSource.ContentHash.Any(character => !Uri.IsHexDigit(character))))
				|| string.IsNullOrWhiteSpace(styleSource.Content))
				throw new RaidSchemaException("A resolved PlantUML style source requires a safe name and content.");
			source.Append("' style-source: ").Append(styleSource.Name);
			if (!string.IsNullOrWhiteSpace(styleSource.Subscriber))
				source.Append(" subscriber=").Append(styleSource.Subscriber);
			if (!string.IsNullOrWhiteSpace(styleSource.ContentHash))
				source.Append(" sha256=").Append(styleSource.ContentHash);
			source.AppendLine();
			source.AppendLine(styleSource.Content.TrimEnd());
		}
		source.Append(Styles.ToPlantUml());
		return source.ToString();
	}

	public string ContentHash => Sha256Hex(ToPlantUml());

	public string ComputeRenderHash(string plantUmlSource)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(plantUmlSource);
		return Sha256Hex(ToPlantUml() + "\n--RAIDIAGRAM-PUML--\n" + plantUmlSource);
	}

	internal static string Sha256Hex(string value)
		=> Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

	private static string EscapePreprocessorPath(string path)
	{
		if (path.IndexOfAny(['\r', '\n', '\'', '"']) >= 0)
			throw new DiagramRenderingException("The approved PlantUML theme root contains unsupported quoting characters.");
		return path;
	}

	[GeneratedRegex("^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)]
	private static partial Regex SafeThemeName();
}

public interface IDiagramStyleProvider
{
	PumlRenderConfiguration Resolve(DiagramManifest manifest, PlantUmlCompileOptions options);

	ResolvedDiagramStyle Resolve(DiagramStyleResolutionContext context)
		=> new()
		{
			Configuration = Resolve(context.Manifest, context.Options),
			Layers = []
		};
}

/// <summary>
/// Resolves global and diagram-kind-specific style layers, then applies the
/// backwards-compatible presentation intent held by the manifest.
/// </summary>
public sealed class DiagramStyleProvider : IDiagramStyleProvider
{
	private readonly PumlStyleCatalog catalog;
	private readonly string? profileId;
	private readonly string? defaultTheme;
	private readonly bool defaultHandwritten;
	private readonly IDiagramStyleRepository? repository;

	public DiagramStyleProvider(
		PumlStyleCatalog? catalog = null,
		string? profileId = null,
		string? defaultTheme = null,
		bool defaultHandwritten = false,
		IDiagramStyleRepository? repository = null)
	{
		this.catalog = catalog ?? new PumlStyleCatalog();
		this.profileId = profileId;
		this.defaultTheme = defaultTheme;
		this.defaultHandwritten = defaultHandwritten;
		this.repository = repository;
	}

	public PumlRenderConfiguration Resolve(DiagramManifest manifest, PlantUmlCompileOptions options)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		ArgumentNullException.ThrowIfNull(options);
		return Resolve(new DiagramStyleResolutionContext
		{
			Manifest = manifest,
			Options = options,
			ProfileId = profileId
		}).Configuration;
	}

	public ResolvedDiagramStyle Resolve(DiagramStyleResolutionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(context.Manifest);
		ArgumentNullException.ThrowIfNull(context.Options);
		context.Manifest.Validate();
		var effectiveProfile = context.ProfileId ?? profileId;
		var effectiveContext = new DiagramStyleResolutionContext
		{
			Manifest = context.Manifest,
			Options = context.Options,
			Locations = context.Locations,
			ProfileId = effectiveProfile,
			RequireSubscriberProfile = context.RequireSubscriberProfile
		};
		var layers = repository?.ResolveLayers(effectiveContext) ?? [];
		var sources = new List<PumlStyleSource>();
		var themeName = context.Manifest.Presentation.Theme ?? defaultTheme;
		var localThemeRoot = context.Options.LocalThemeRoot;
		if (repository is not null && localThemeRoot is null && !string.IsNullOrWhiteSpace(themeName))
		{
			var themeLayer = repository.ResolveTheme(effectiveContext, themeName);
			if (themeLayer is not null)
			{
				layers = [themeLayer, .. layers];
				sources.Add(ToSource(themeLayer));
				themeName = null;
			}
		}
		if (repository is not null
			&& effectiveContext.RequireSubscriberProfile
			&& !string.IsNullOrWhiteSpace(effectiveProfile)
			&& layers.Count == 0)
			throw new RaiPathNotFoundException(
				$"The explicitly selected subscriber diagram style profile '{effectiveProfile}' was not found.",
				effectiveProfile);
		sources.AddRange(catalog.ResolveFiles(context.Manifest.Diagram.Kind));
		sources.AddRange(layers.Where(layer => layer.Scope != "theme").Select(ToSource));

		var styles = catalog.Resolve(context.Manifest.Diagram.Kind);
		if (!string.IsNullOrWhiteSpace(context.Manifest.Presentation.FontName))
			styles.Set("root", PumlStyleProperty.FontName, context.Manifest.Presentation.FontName);

		return new ResolvedDiagramStyle
		{
			Configuration = new PumlRenderConfiguration
			{
				DiagramKind = context.Manifest.Diagram.Kind,
				ProfileId = effectiveProfile,
				Theme = themeName,
				LocalThemeRoot = localThemeRoot,
				Handwritten = context.Manifest.Presentation.Handwritten || defaultHandwritten,
				StyleSources = sources,
				Styles = styles
			},
			Layers = layers.OrderBy(layer => layer.Precedence).ToArray()
		};
	}

	private static PumlStyleSource ToSource(DiagramStyleLayer layer)
		=> new(layer.SourceName, layer.Content, layer.Location.Subscriber, layer.ContentHash);
}

public sealed class PumlStyleFile : ImageTreeTextFile
{
	private PumlStyleFile(
		RaiPath subscriberRoot,
		string itemId,
		string nameExt,
		PathConventionType convention)
		: base(subscriberRoot, itemId, nameExt, "puml", convention)
	{
	}

	public PumlStyleFile(RaiPath styleRoot, string name)
		: base(styleRoot ?? throw new ArgumentNullException(nameof(styleRoot)), name, string.Empty, "puml")
	{
	}

	public static PumlStyleFile FromImageTree(
		RaiPath imageTreeRoot,
		string subscriber,
		string itemId,
		PathConventionType convention = PathConventionType.ItemIdTree8x2)
	{
		ArgumentNullException.ThrowIfNull(imageTreeRoot);
		if (string.IsNullOrWhiteSpace(subscriber) || subscriber.Contains('/') || subscriber.Contains('\\'))
			throw new ArgumentException("The style subscriber must be a plain ImageTree path segment.", nameof(subscriber));
		return FromSubscriber(imageTreeRoot / new RaiRelPath(subscriber), itemId, convention);
	}

	public static PumlStyleFile FromSubscriber(
		RaiPath subscriberRoot,
		string itemId,
		PathConventionType convention = PathConventionType.ItemIdTree8x2)
	{
		ArgumentNullException.ThrowIfNull(subscriberRoot);
		return new PumlStyleFile(subscriberRoot, itemId, string.Empty, convention);
	}

	public static PumlStyleFile FromSubscriberProfile(
		RaiPath subscriberRoot,
		string itemId,
		string nameExt,
		PathConventionType convention = PathConventionType.ItemIdTree8x2)
		=> new(subscriberRoot ?? throw new ArgumentNullException(nameof(subscriberRoot)), itemId, nameExt, convention);

	public PumlStyleFile Write(PumlStyleSheet styleSheet)
	{
		ArgumentNullException.ThrowIfNull(styleSheet);
		DeleteAll().Append(styleSheet.ToPlantUml()).Save();
		return this;
	}
}

public sealed class PumlConfigFile : ImageTreeTextFile
{
	public PumlConfigFile(
		RaiPath subscriberRoot,
		string itemId,
		PathConventionType convention)
		: base(subscriberRoot ?? throw new ArgumentNullException(nameof(subscriberRoot)), itemId, "config", "puml", convention)
	{
	}

	public PumlConfigFile(RaiPath configRoot, string name)
		: base(configRoot ?? throw new ArgumentNullException(nameof(configRoot)), name, "config", "puml")
	{
	}

	public static PumlConfigFile FromImageTree(
		RaiPath imageTreeRoot,
		string subscriber,
		string itemId,
		PathConventionType convention = PathConventionType.ItemIdTree8x2)
	{
		ArgumentNullException.ThrowIfNull(imageTreeRoot);
		if (string.IsNullOrWhiteSpace(subscriber) || subscriber.Contains('/') || subscriber.Contains('\\'))
			throw new ArgumentException("The config subscriber must be a plain ImageTree path segment.", nameof(subscriber));
		return FromImageTree(imageTreeRoot / new RaiRelPath(subscriber), itemId, convention);
	}

	public static PumlConfigFile FromImageTree(
		RaiPath subscriberRoot,
		string itemId,
		PathConventionType convention = PathConventionType.ItemIdTree8x2)
	{
		ArgumentNullException.ThrowIfNull(subscriberRoot);
		return new PumlConfigFile(subscriberRoot, itemId, convention);
	}

	public PumlConfigFile Write(PumlRenderConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		DeleteAll().Append(configuration.ToPlantUml()).Save();
		return this;
	}
}
