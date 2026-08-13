namespace RaiDiagram.Tests;

public class SvgProvenanceTests
{
	[Fact]
	public void EmbedAndRead_RoundTripsRequiredMetadata()
	{
		var hash = new string('a', 64);
		var svg = SvgProvenanceMetadata.Embed(
			"<svg xmlns=\"http://www.w3.org/2000/svg\"><text>diagram</text></svg>",
			new SvgProvenance
			{
				RaidId = "ScheduleRehearsal",
				SemanticHash = hash,
				SchemaVersion = "1.0",
				ManifestUri = "raid:ScheduleRehearsal",
				ModelRevision = "r1"
			});

		var result = SvgProvenanceMetadata.Read(svg);

		Assert.Equal("ScheduleRehearsal", result.RaidId);
		Assert.Equal(hash, result.SemanticHash);
		Assert.Equal("1.0", result.SchemaVersion);
		Assert.Equal("raid:ScheduleRehearsal", result.ManifestUri);
		Assert.DoesNotContain("/Users/", svg, StringComparison.Ordinal);
	}

	[Fact]
	public void Embed_RejectsAbsoluteLocalManifestUri()
	{
		Assert.Throws<SvgProvenanceException>(() => SvgProvenanceMetadata.Embed(
			"<svg xmlns=\"http://www.w3.org/2000/svg\" />",
			new SvgProvenance
			{
				RaidId = "ScheduleRehearsal",
				SemanticHash = new string('b', 64),
				SchemaVersion = "1.0",
				ManifestUri = "file:///Users/example/ScheduleRehearsal.raid"
			}));
	}

	[Fact]
	public void Read_RejectsMissingMetadata()
	{
		Assert.Throws<SvgProvenanceException>(() =>
			SvgProvenanceMetadata.Read("<svg xmlns=\"http://www.w3.org/2000/svg\" />"));
	}
}
