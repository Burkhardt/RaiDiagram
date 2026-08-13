using OsLib;
using RaiImage;

namespace RaiDiagram.Tests;

public sealed class PlantUmlRendererTests : IDisposable
{
	private readonly RaiPath? originalPath = PlantUml.PlantUmlPath;
	private readonly string originalCommand = PlantUml.CommandName;
	private readonly string originalJavaCommand = PlantUml.JavaCommand;

	public void Dispose()
	{
		PlantUml.PlantUmlPath = originalPath;
		PlantUml.CommandName = originalCommand;
		PlantUml.JavaCommand = originalJavaCommand;
	}

	[Fact]
	public async Task RenderAsync_WritesRaiImageArtifactsAndVerifiedSvgProvenance()
	{
		var root = Os.TempDir / "RAIkeep" / "raidiagram-tests" / nameof(RenderAsync_WritesRaiImageArtifactsAndVerifiedSvgProvenance);
		Cleanup(root);
		try
		{
			var tools = root / "tools";
			tools.mkdir();
			var script = CreateFakePlantUml(tools);
			PlantUml.PlantUmlPath = tools;
			PlantUml.CommandName = new RaiFile(script).NameWithExtension;
			var model = TestDiagrams.CreateUseCaseModel();

			var result = await new PlantUmlDiagramRenderer().RenderAsync(
				model,
				new DiagramDestination
				{
					ImageTreeRoot = root / "images",
					Subscriber = "ExampleConsumer",
					ItemId = "ScheduleRehearsal"
				},
				new DiagramRenderOptions { ManifestUri = "raid:ScheduleRehearsal" },
				TestContext.Current.CancellationToken);

			Assert.True(result.PlantUmlSource.Exists());
			Assert.True(result.Svg.Exists());
			Assert.Equal(model.SemanticHash, result.SemanticHash);
			var svg = new TextFile(result.Svg.FullName).ReadAllText();
			var provenance = SvgProvenanceMetadata.Read(svg);
			Assert.Equal("ScheduleRehearsal", provenance.RaidId);
			Assert.Equal(model.SemanticHash, provenance.SemanticHash);
			Assert.Equal("raid:ScheduleRehearsal", provenance.ManifestUri);
			Assert.DoesNotContain(root.FullPath, svg, StringComparison.Ordinal);
		}
		finally
		{
			Cleanup(root);
		}
	}

	private static string CreateFakePlantUml(RaiPath tools)
	{
		if (OperatingSystem.IsWindows())
		{
			return RaiSystem.CreateScript(tools, "fake-plantuml.cmd", """
				@echo off
				setlocal EnableExtensions EnableDelayedExpansion
				set "last="
				:loop
				if "%~1"=="" goto after
				set "last=%~1"
				shift
				goto loop
				:after
				set "svg=%last:.puml=.svg%"
				> "%svg%" echo ^<svg xmlns="http://www.w3.org/2000/svg"^>^<text^>generated^</text^>^</svg^>
				exit /b 0
				""").FullName;
		}

		return RaiSystem.CreateScript(tools, "fake-plantuml.sh", """
			#!/bin/sh
			last=""
			for arg in "$@"; do
			  last="$arg"
			done
			svg="${last%.puml}.svg"
			printf '%s' '<svg xmlns="http://www.w3.org/2000/svg"><text>generated</text></svg>' > "$svg"
			exit 0
			""").FullName;
	}

	private static void Cleanup(RaiPath root)
	{
		if (root.Exists())
			root.rmdir(depth: 7, deleteFiles: true);
	}
}
