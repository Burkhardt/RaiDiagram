namespace RaiDiagram;

/// <summary>Detects fatal diagnostic documents returned in place of a PlantUML SVG.</summary>
public static class PlantUmlSvgDiagnostics
{
	private static readonly string[] ErrorMarkers =
	[
		"Syntax Error?",
		"An error has occured",
		"An error has occurred",
		"No diagram found"
	];

	private static readonly string[] WarningMarkers =
	[
		"This version of PlantUML is",
		"consider upgrading from https://plantuml.com/download"
	];

	public static bool IsErrorDocument(string svg)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(svg);
		return ErrorMarkers.Any(marker => svg.Contains(marker, StringComparison.OrdinalIgnoreCase));
	}

	public static bool ContainsVersionWarning(string svg)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(svg);
		return WarningMarkers.Any(marker => svg.Contains(marker, StringComparison.OrdinalIgnoreCase));
	}

	public static void ThrowIfErrorDocument(string svg)
	{
		if (IsErrorDocument(svg))
			throw new DiagramRenderingException(
				"PlantUML returned a diagnostic error document instead of a rendered SVG.");
	}
}
