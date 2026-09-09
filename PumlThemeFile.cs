using System.Text.RegularExpressions;
using OsLib;
using RaiImage;

namespace RaiDiagram;

/// <summary>
/// A local PlantUML theme using PlantUML's required
/// <c>puml-theme-&lt;name&gt;.puml</c> filename convention.
/// </summary>
public sealed partial class PumlThemeFile : ItemTreeTextFile
{
	public PumlThemeFile(ItemTreePath itemPath, string themeName)
		: base(
			itemPath ?? throw new ArgumentNullException(nameof(itemPath)),
			FileStem(themeName),
			string.Empty,
			"puml")
	{
		ThemeName = ValidateThemeName(themeName);
	}

	public PumlThemeFile(
		RaiPath subscriberRoot,
		string itemId,
		string themeName,
		PathConventionType convention)
		: base(
			subscriberRoot ?? throw new ArgumentNullException(nameof(subscriberRoot)),
			itemId,
			FileStem(themeName),
			string.Empty,
			"puml",
			convention)
	{
		ThemeName = ValidateThemeName(themeName);
	}

	public PumlThemeFile(RaiPath themeRoot, string themeName)
		: base(themeRoot ?? throw new ArgumentNullException(nameof(themeRoot)), FileStem(themeName), string.Empty, "puml")
	{
		ThemeName = ValidateThemeName(themeName);
	}

	public string ThemeName { get; }

	public static string FileNameFor(string themeName) => $"{FileStem(themeName)}.puml";

	public PumlThemeFile Write(PumlStyleSheet styleSheet, bool handwritten = false)
	{
		ArgumentNullException.ThrowIfNull(styleSheet);
		DeleteAll();
		if (handwritten)
			Append("!option handwritten true");
		Append(styleSheet.ToPlantUml()).Save();
		return this;
	}

	private static string FileStem(string themeName)
		=> $"puml-theme-{ValidateThemeName(themeName)}";

	private static string ValidateThemeName(string themeName)
	{
		if (string.IsNullOrWhiteSpace(themeName) || !SafeThemeName().IsMatch(themeName))
			throw new RaidSchemaException(
				$"Theme name '{themeName}' is not a safe PlantUML theme identifier.");
		return themeName;
	}

	[GeneratedRegex("^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)]
	private static partial Regex SafeThemeName();
}
