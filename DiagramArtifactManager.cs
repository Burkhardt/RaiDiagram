using OsLib;

namespace RaiDiagram;

public enum DiagramArtifactFormat
{
	Raid,
	PlantUml,
	Svg,
	All
}

public sealed record DiagramDerivedArtifacts(
	string Raid,
	string PlantUml,
	string Svg,
	string SemanticHash);

public sealed record DiagramRefreshResult(
	string DiagramId,
	bool PlantUmlWritten,
	bool SvgWritten,
	string SemanticHash)
{
	public bool Changed => PlantUmlWritten || SvgWritten;
}

/// <summary>
/// Derives, exports, and refreshes diagram siblings from the authoritative .raid manifest.
/// All writes go directly through RaiFile at their final pathname.
/// </summary>
public sealed class DiagramArtifactManager
{
	private readonly PlantUmlDiagramCompiler compiler;

	public DiagramArtifactManager(PlantUmlDiagramCompiler? compiler = null)
	{
		this.compiler = compiler ?? new PlantUmlDiagramCompiler();
	}

	public DiagramDerivedArtifacts Derive(
		DiagramManifest manifest,
		AimSvgProfile svgProfile = AimSvgProfile.Hydratable)
	{
		ArgumentNullException.ThrowIfNull(manifest);
		manifest.Validate();
		var renderManifest = DiagramModel.FromManifest(manifest).Manifest;
		DeterministicDiagramCanvas.Ensure(renderManifest);
		return new DiagramDerivedArtifacts(
			RaidJson5.Serialize(manifest),
			compiler.Compile(renderManifest).Source,
			AimSvg.Emit(renderManifest, svgProfile),
			DiagramSemanticHasher.Compute(manifest));
	}

	public DiagramRefreshResult Refresh(
		DiagramArtifactSet artifacts,
		AimSvgProfile svgProfile = AimSvgProfile.Hydratable)
	{
		ArgumentNullException.ThrowIfNull(artifacts);
		if (!artifacts.RaidManifest.Exists())
			throw new RaiPathNotFoundException(
				$"The authoritative diagram manifest does not exist: {artifacts.RaidManifest.FullName}",
				artifacts.RaidManifest.FullName);

		var manifest = artifacts.RaidManifest.LoadManifest();
		EnsureIdentity(artifacts, manifest);
		var derived = Derive(manifest, svgProfile);
		var pumlWritten = WriteIfChanged(
			new TextFile(artifacts.PlantUmlSource.FullName),
			derived.PlantUml);
		var svgWritten = WriteIfChanged(new TextFile(artifacts.Svg.FullName), derived.Svg);
		return new DiagramRefreshResult(
			artifacts.DiagramId,
			pumlWritten,
			svgWritten,
			derived.SemanticHash);
	}

	public IReadOnlyList<RaiFile> Export(
		DiagramArtifactSet artifacts,
		RaiPath destination,
		DiagramArtifactFormat format,
		AimSvgProfile svgProfile = AimSvgProfile.Hydratable)
	{
		ArgumentNullException.ThrowIfNull(artifacts);
		ArgumentNullException.ThrowIfNull(destination);
		if (!artifacts.RaidManifest.Exists())
			throw new RaiPathNotFoundException(
				$"The authoritative diagram manifest does not exist: {artifacts.RaidManifest.FullName}",
				artifacts.RaidManifest.FullName);

		var manifest = artifacts.RaidManifest.LoadManifest();
		EnsureIdentity(artifacts, manifest);
		return Write(destination, artifacts.DiagramId, manifest, format, svgProfile);
	}

	public IReadOnlyList<RaiFile> Write(
		RaiPath destination,
		string diagramId,
		DiagramManifest manifest,
		DiagramArtifactFormat format = DiagramArtifactFormat.All,
		AimSvgProfile svgProfile = AimSvgProfile.Hydratable)
	{
		ArgumentNullException.ThrowIfNull(destination);
		ArgumentException.ThrowIfNullOrWhiteSpace(diagramId);
		ArgumentNullException.ThrowIfNull(manifest);
		if (!string.Equals(diagramId, manifest.Diagram.Id, StringComparison.Ordinal))
			throw new RaidSchemaException(
				$"Output identity '{diagramId}' does not match manifest diagram id '{manifest.Diagram.Id}'.");
		var derived = Derive(manifest, svgProfile);
		var files = new List<RaiFile>();
		if (format is DiagramArtifactFormat.Raid or DiagramArtifactFormat.All)
			files.Add(Write(new TextFile(destination, diagramId, "raid"), derived.Raid));
		if (format is DiagramArtifactFormat.PlantUml or DiagramArtifactFormat.All)
			files.Add(Write(new TextFile(destination, diagramId, "puml"), derived.PlantUml));
		if (format is DiagramArtifactFormat.Svg or DiagramArtifactFormat.All)
			files.Add(Write(new TextFile(destination, diagramId, "svg"), derived.Svg));
		return files;
	}

	private static bool WriteIfChanged(TextFile file, string expected)
	{
		var persisted = PersistedContent(expected);
		if (file.Exists() && string.Equals(file.ReadAllText(), persisted, StringComparison.Ordinal))
			return false;
		Write(file, expected);
		return true;
	}

	private static TextFile Write(TextFile file, string contents)
	{
		var normalized = contents.Replace("\r\n", "\n", StringComparison.Ordinal)
			.Replace('\r', '\n')
			.TrimEnd('\n');
		file.Lines = normalized.Split('\n').ToList();
		file.Changed = true;
		file.Save();
		return file;
	}

	private static string PersistedContent(string contents)
		=> contents.Replace("\r\n", "\n", StringComparison.Ordinal)
			.Replace('\r', '\n')
			.TrimEnd('\n') + "\n";

	private static void EnsureIdentity(DiagramArtifactSet artifacts, DiagramManifest manifest)
	{
		if (!string.Equals(artifacts.DiagramId, manifest.Diagram.Id, StringComparison.Ordinal))
			throw new RaidSchemaException(
				$"Artifact identity '{artifacts.DiagramId}' does not match manifest diagram id '{manifest.Diagram.Id}'.");
	}
}
