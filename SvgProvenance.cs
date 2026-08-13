using System.Xml.Linq;

namespace RaiDiagram;

public sealed class SvgProvenance
{
	public required string RaidId { get; init; }
	public required string SemanticHash { get; init; }
	public required string SchemaVersion { get; init; }
	public string? ManifestUri { get; init; }
	public string? ModelRevision { get; init; }
}

public static class SvgProvenanceMetadata
{
	public static string Embed(string svg, SvgProvenance provenance)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(svg);
		ArgumentNullException.ThrowIfNull(provenance);
		Validate(provenance);

		try
		{
			var document = XDocument.Parse(svg, LoadOptions.PreserveWhitespace);
			var root = document.Root ?? throw new SvgProvenanceException("The SVG document has no root element.");
			if (root.Name.LocalName != "svg")
				throw new SvgProvenanceException("The rendered document root is not SVG.");

			root.SetAttributeValue("data-raid-id", provenance.RaidId);
			root.SetAttributeValue("data-raid-semantic-hash", provenance.SemanticHash);
			root.SetAttributeValue("data-raid-schema-version", provenance.SchemaVersion);
			root.SetAttributeValue("data-raid-manifest-uri", provenance.ManifestUri);
			root.SetAttributeValue("data-raid-model-revision", provenance.ModelRevision);
			return document.ToString(SaveOptions.DisableFormatting);
		}
		catch (SvgProvenanceException)
		{
			throw;
		}
		catch (Exception exception)
		{
			throw new SvgProvenanceException("The rendered SVG could not receive .raid provenance metadata.", exception);
		}
	}

	public static SvgProvenance Read(string svg)
	{
		try
		{
			var root = XDocument.Parse(svg).Root
				?? throw new SvgProvenanceException("The SVG document has no root element.");
			var provenance = new SvgProvenance
			{
				RaidId = Required(root, "data-raid-id"),
				SemanticHash = Required(root, "data-raid-semantic-hash"),
				SchemaVersion = Required(root, "data-raid-schema-version"),
				ManifestUri = (string?)root.Attribute("data-raid-manifest-uri"),
				ModelRevision = (string?)root.Attribute("data-raid-model-revision")
			};
			Validate(provenance);
			return provenance;
		}
		catch (SvgProvenanceException)
		{
			throw;
		}
		catch (Exception exception)
		{
			throw new SvgProvenanceException("The SVG provenance metadata is malformed.", exception);
		}
	}

	private static string Required(XElement root, string name)
		=> (string?)root.Attribute(name)
			?? throw new SvgProvenanceException($"The SVG is missing required '{name}' metadata.");

	private static void Validate(SvgProvenance provenance)
	{
		if (string.IsNullOrWhiteSpace(provenance.RaidId))
			throw new SvgProvenanceException("SVG provenance requires a .raid id.");
		if (string.IsNullOrWhiteSpace(provenance.SchemaVersion))
			throw new SvgProvenanceException("SVG provenance requires a schema version.");
		if (provenance.SemanticHash.Length != 64
			|| provenance.SemanticHash.Any(character => !Uri.IsHexDigit(character)))
			throw new SvgProvenanceException("SVG provenance requires a lowercase or uppercase SHA-256 semantic hash.");
		if (provenance.ManifestUri is { Length: > 0 }
			&& Uri.TryCreate(provenance.ManifestUri, UriKind.Absolute, out var uri)
			&& uri.IsFile)
			throw new SvgProvenanceException("SVG provenance must not expose an absolute local file URI.");
	}
}
