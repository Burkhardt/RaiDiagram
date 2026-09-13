using OsLib;
using RaiDiagram.Builders;
using RaiImage;

namespace RaiDiagram.Tests;

public sealed class DiagramBuilderTests
{
	[Fact]
	public void OneUseCaseBuilder_ProducesTypedManifestAndMixedPlantUml()
	{
		var builder = CreateUseCaseBuilder();
		var manifest = builder.BuildManifest();
		var source = new PlantUmlDiagramCompiler().Compile(manifest).Source;

		Assert.Equal("SignContract", builder.BaseItemId);
		Assert.Equal("SignContract", builder.ItemId);
		Assert.Equal("UCD", builder.NameExt);
		Assert.Equal("SignContract_UCD", manifest.Diagram.Id);
		Assert.Equal(DiagramKind.UseCase, manifest.Diagram.Kind);
		Assert.All(
			manifest.Projection.Elements.Where(item => item.DisplayName is "BandLeader" or "Owner"),
			item => Assert.Equal(DiagramElementKinds.Role, item.Kind));
		Assert.Equal("allowmixing", Lines(source)[1]);
		Assert.Contains("frame \"Contract Management\"", source);
		Assert.Contains("actor \"BandLeader\"", source);
		Assert.Contains("«executes» [1..1]", source);
		Assert.Contains("«defines role» [0..1]", source);
		Assert.Contains("«references What» [0..1]", source);
		Assert.Contains("«DependsOn»", source);
		Assert.Contains("note right of", source);
	}

	[Theory]
	[InlineData("2026-08-19T04:44:46.7202080Z")]
	[InlineData("2026-08-19T04:44:46.7202080+00:00")]
	[InlineData("r42")]
	public void OneUseCaseBuilder_PreservesCapturedRevisionVerbatim(string capturedRevision)
	{
		var model = Model();
		model.CapturedRevision = capturedRevision;

		var manifest = new OneUseCaseDiagramBuilder("Probe", model)
			.SetMainUseCase("Probe")
			.BuildManifest();

		Assert.Equal(capturedRevision, manifest.Model.CapturedRevision);
	}

	[Fact]
	public void OneUseCaseBuilder_ObjectReferenceSupportsExplicitStereotypeAndPreservesDefault()
	{
		var explicitManifest = new OneUseCaseDiagramBuilder("ProduceWorkspace", Model())
			.SetMainUseCase("Produce workspace")
			.AddObjectReference("Workspace", "What", "0..*", "produces")
			.BuildManifest();
		var defaultManifest = new OneUseCaseDiagramBuilder("ReferenceWorkspace", Model())
			.SetMainUseCase("Reference workspace")
			.AddObjectReference("Workspace", "What", "0..*")
			.BuildManifest();

		var explicitRelationship = Assert.Single(explicitManifest.Projection.Relationships);
		var defaultRelationship = Assert.Single(defaultManifest.Projection.Relationships);
		Assert.Equal("«produces»", explicitRelationship.Label);
		Assert.Equal("«references What»", defaultRelationship.Label);
		Assert.Equal("0..*", explicitRelationship.Cardinality);
		Assert.Equal("0..*", defaultRelationship.Cardinality);

		var explicitSource = new PlantUmlDiagramCompiler().Compile(explicitManifest).Source;
		var defaultSource = new PlantUmlDiagramCompiler().Compile(defaultManifest).Source;
		Assert.Contains("«produces» [0..*]", explicitSource);
		Assert.DoesNotContain("«references produces»", explicitSource);
		Assert.Contains("«references What» [0..*]", defaultSource);
	}

	[Fact]
	public void RoleFillerBuilder_ProducesRfdAndOdIdentifiersAndMixedShapes()
	{
		var rfd = new RoleFillerDiagramBuilder("cvhzhi", Model())
			.SetInstance("cvhzhi", "Show", new Dictionary<string, string> { ["Status"] = "Draft" })
			.AddWhoFiller("Owner", "Adele")
			.AddWhatFiller("Contract", "Contract42", "Contract")
			.AddWhereFiller("Venue", "MusicHall")
			.BuildManifest();
		var od = new RoleFillerDiagramBuilder("cvhzhi", Model(), DiagramArchetype.Object)
			.SetInstance("cvhzhi", "Show")
			.BuildManifest();
		var source = new PlantUmlDiagramCompiler().Compile(rfd).Source;

		Assert.Equal("cvhzhi_RFD", rfd.Diagram.Id);
		Assert.Equal("cvhzhi_OD", od.Diagram.Id);
		Assert.Equal("allowmixing", Lines(source)[1]);
		Assert.Contains("object \"<u>cvhzhi</u> : Show\"", source);
		Assert.Contains("Status = Draft", source);
		Assert.Contains("actor \"Adele\"", source);
		Assert.Contains(" : Owner", source);
	}

	[Fact]
	public void ClassBuilder_EmitsKlOneRolesMethodsAndInstances()
	{
		var manifest = new ClassDiagramBuilder("Show", Model())
			.SetClass(
				"Show",
				attributes: ["Name : string"],
				roles: [new KlOneRoleDef("Venue", "Place", "1..1", "TBD")],
				methods: ["Schedule()"])
			.AddInstance("Samstag26", "Show")
			.BuildManifest();
		var source = new PlantUmlDiagramCompiler().Compile(manifest).Source;

		Assert.Equal("Show_CD", manifest.Diagram.Id);
		Assert.Equal("allowmixing", Lines(source)[1]);
		Assert.Contains("Name : string", source);
		Assert.Contains("Venue : Place [1..1] = TBD", source);
		Assert.Contains("Schedule()", source);
		Assert.Contains(" <|.. ", source);
		Assert.Contains("«instanceOf»", source);
	}

	[Fact]
	public void ClassBuilder_SetSuperClassEmitsTypedGeneralizationAlongsideInstance()
	{
		var manifest = new ClassDiagramBuilder("Agent", Model())
			.SetClass("Agent", attributes: ["Name : string"])
			.SetSuperClass("Actor")
			.AddInstance("7010", "Agent")
			.BuildManifest();
		var source = new PlantUmlDiagramCompiler().Compile(manifest).Source;

		var actor = Assert.Single(manifest.Projection.Elements, item => item.DisplayName == "Actor");
		var agent = Assert.Single(manifest.Projection.Elements, item => item.DisplayName == "Agent");
		Assert.Equal(DiagramElementKinds.Class, actor.Kind);
		Assert.Equal(DiagramElementKinds.Class, agent.Kind);
		var generalization = Assert.Single(manifest.Projection.Relationships,
			item => item.Kind == DiagramRelationshipKinds.Generalization);
		Assert.Equal(actor.Id, generalization.SourceId);
		Assert.Equal(agent.Id, generalization.TargetId);
		Assert.Contains("class \"Actor\"", source);
		Assert.Contains("class \"Agent\"", source);
		Assert.Contains(" <|-- ", source);
		Assert.Contains(" <|.. ", source);
		Assert.Contains("«instanceOf»", source);
	}

	[Fact]
	public void ClassBuilder_SetSuperClassSupportsStereotype()
	{
		var manifest = new ClassDiagramBuilder("Person", Model())
			.SetClass("Person")
			.SetSuperClass("Actor", "subClassOf")
			.BuildManifest();
		var source = new PlantUmlDiagramCompiler().Compile(manifest).Source;

		var relationship = Assert.Single(manifest.Projection.Relationships);
		Assert.Equal("«subClassOf»", relationship.Label);
		Assert.Contains(" <|-- ", source);
		Assert.Contains(" : «subClassOf»", source);
	}

	[Fact]
	public void ClassBuilder_SetSuperClassRejectsInvalidOrderNameAndSecondDefinition()
	{
		Assert.Throws<RaidSchemaException>(() =>
			new ClassDiagramBuilder("Agent", Model()).SetSuperClass("Actor"));
		Assert.Throws<ArgumentException>(() =>
			new ClassDiagramBuilder("Agent", Model()).SetClass("Agent").SetSuperClass(" "));

		var builder = new ClassDiagramBuilder("Agent", Model())
			.SetClass("Agent")
			.SetSuperClass("Actor");
		Assert.Throws<RaidSchemaException>(() => builder.SetSuperClass("Entity"));
	}

	[Fact]
	public void ActivityBuilder_EmitsSwimlanesDecisionAndControlFlow()
	{
		var manifest = CreateActivityBuilder().BuildManifest();
		var source = new PlantUmlDiagramCompiler().Compile(manifest).Source;

		Assert.Equal("ActWorkspaceGenesis_AD", manifest.Diagram.Id);
		Assert.Contains("|Band|", source);
		Assert.Contains("|Venue|", source);
		Assert.Contains(":Request rehearsal;", source);
		Assert.Contains("if (Venue available?) then (true)", source);
		Assert.Contains("else (false)", source);
		Assert.Contains("endif", source);
		Assert.Contains("|Band|\nstart", source);
		Assert.Contains("stop", source);
	}

	[Fact]
	public void SequenceBuilder_EmitsTypedParticipantsOrderedMessagesDividerAndNote()
	{
		var manifest = CreateSequenceBuilder().BuildManifest();
		var source = new PlantUmlDiagramCompiler().Compile(manifest).Source;

		Assert.Equal("ConfirmAvailability_SD", manifest.Diagram.Id);
		Assert.Contains("actor \"Band Manager\"", source);
		Assert.Contains("boundary \"AIA\"", source);
		Assert.Contains(" ->> ", source);
		Assert.Contains(" --> ", source);
		Assert.Contains("== Confirmation ==", source);
		Assert.Contains("note right of", source);
		Assert.Contains("Check the proposed date", source);
	}

	[Fact]
	public void ArtifactSet_WritesRaidPumlAndClientSvgToOneCumulative8x2Home()
	{
		var root = Os.TempDir / "RAIkeep" / "raidiagram-tests" /
			nameof(ArtifactSet_WritesRaidPumlAndClientSvgToOneCumulative8x2Home);
		Cleanup(root);
		try
		{
			var manifest = CreateUseCaseBuilder().BuildManifest();
			var artifacts = new DiagramArtifactSet(root / "ImageTree", "AfricaStage", "SignContract", "UCD")
				.Write(manifest);
			artifacts.SaveClientSvg(manifest, "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>Sign contract</text></svg>");

			var expected = root / "ImageTree" / "AfricaStage" / "SignCont" / "SignContra";
			Assert.Equal(expected.FullPath, artifacts.RaidManifest.SubdirRoot.FullPath);
			Assert.Equal("SignContract", artifacts.ItemId);
			Assert.Equal("UCD", artifacts.NameExt);
			Assert.Equal("SignContract_UCD", artifacts.DiagramId);
			Assert.Equal("SignContract_UCD.raid", artifacts.RaidManifest.NameWithExtension);
			Assert.Equal("SignContract_UCD.puml", artifacts.PlantUmlSource.NameWithExtension);
			Assert.Equal("SignContract_UCD.svg", artifacts.Svg.NameWithExtension);
			Assert.True(artifacts.RaidManifest.Exists());
			Assert.True(artifacts.PlantUmlSource.Exists());
			Assert.True(artifacts.Svg.Exists());
			Assert.False((root / "ImageTree" / "AfricaStage" / "UseCase").Exists());
			Assert.False((root / "ImageTree" / "AfricaStage" / "Activity").Exists());
			var provenance = SvgProvenanceMetadata.Read(new TextFile(artifacts.Svg.FullName).ReadAllText());
			Assert.Equal(manifest.Diagram.Id, provenance.RaidId);
			Assert.Equal(DiagramSemanticHasher.Compute(manifest), provenance.SemanticHash);
		}
		finally
		{
			Cleanup(root);
		}
	}

	[Fact]
	public void SvgDiagnostics_DetectsSyntaxErrorsAndOutdatedVersionWarnings()
	{
		const string diagnostic = """
			<svg xmlns="http://www.w3.org/2000/svg">
			<text>This version of PlantUML is 236 days old, so you should consider upgrading from https://plantuml.com/download</text>
			<text>Syntax Error? (Assumed diagram type: component)</text>
			</svg>
			""";

		Assert.True(PlantUmlSvgDiagnostics.IsErrorDocument(diagnostic));
		Assert.True(PlantUmlSvgDiagnostics.ContainsVersionWarning(diagnostic));
		Assert.Throws<DiagramRenderingException>(() => PlantUmlSvgDiagnostics.ThrowIfErrorDocument(diagnostic));
	}

	[Fact]
	public void Builder_RejectsBaseItemIdThatAlreadyContainsAnArchetypeSeam()
	{
		Assert.Throws<RaidSchemaException>(() => new OneUseCaseDiagramBuilder("SignContract_UCD", Model()));
	}

	[Fact]
	public void NumberedBuilderAndArtifacts_UseItemIdNumberNameExtOrderInOneItemTreeHome()
	{
		var root = Os.TempDir / "RAIkeep" / "raidiagram-tests" /
			nameof(NumberedBuilderAndArtifacts_UseItemIdNumberNameExtOrderInOneItemTreeHome);
		Cleanup(root);
		try
		{
			var builder = new OneUseCaseDiagramBuilder("SignContract", Model(), itemNumber: 2)
				.SetMainUseCase("Sign contract");
			var manifest = builder.BuildManifest();
			var artifacts = new DiagramArtifactSet(
				root / "ImageTree",
				"AfricaStage",
				builder.BaseItemId,
				builder.NameExt,
				builder.ItemNumber).Write(manifest);

			Assert.Equal("SignContract_02_UCD", manifest.Diagram.Id);
			Assert.Equal("SignContract", artifacts.ItemId);
			Assert.Equal(2, artifacts.ItemNumber);
			Assert.Equal("UCD", artifacts.NameExt);
			Assert.Equal("SignContract_02_UCD.raid", artifacts.RaidManifest.NameWithExtension);
			Assert.Equal("SignContract_02_UCD.puml", artifacts.PlantUmlSource.NameWithExtension);
			Assert.Equal("SignContract_02_UCD.svg", artifacts.Svg.NameWithExtension);
			Assert.EndsWith("/SignCont/SignContra/", artifacts.Svg.SubdirRoot.FullPath, StringComparison.Ordinal);
		}
		finally
		{
			Cleanup(root);
		}
	}

	internal static OneUseCaseDiagramBuilder CreateUseCaseBuilder() =>
		new OneUseCaseDiagramBuilder("SignContract", Model())
			.SetMainUseCase("Sign contract", "Contract Management")
			.AddInitiatingRole("BandLeader")
			.AddDefinedRole("Owner")
			.AddObjectReference("Contract")
			.AddDependency("DraftContract")
			.SetNarrativeNote("The agreement is signed after availability is confirmed.");

	internal static ActivityDiagramBuilder CreateActivityBuilder() =>
		new ActivityDiagramBuilder("ActWorkspaceGenesis", Model())
			.SetActivity("Workspace genesis", "Create and validate a workspace")
			.AddSwimlane("Band")
			.AddSwimlane("Venue")
			.AddStep("request", "Band", "Request rehearsal")
			.AddDecision("availability", "Venue", "Venue available?", "confirm", "decline")
			.AddStep("confirm", "Venue", "Confirm rehearsal")
			.AddStep("decline", "Venue", "Decline rehearsal")
			.AddTransition("request", "availability")
			.SetInitialStep("request")
			.AddTerminalStep("confirm")
			.AddTerminalStep("decline");

	internal static SequenceDiagramBuilder CreateSequenceBuilder() =>
		new SequenceDiagramBuilder("ConfirmAvailability", Model())
			.SetSequenceTitle("Confirm availability")
			.AddParticipant("Band Manager", "actor", "band")
			.AddParticipant("AIA", "boundary")
			.AddMessage("band", "AIA", "Request availability", isAsync: true, returnMessage: "Request accepted")
			.AddDivider("Confirmation")
			.AddSelfMessage("AIA", "Evaluate schedule")
			.AddNote("AIA", "Check the proposed date");

	internal static DiagramModelIdentity Model() => new()
	{
		ProviderScheme = "test-model",
		ModelId = "AIA",
		CapturedRevision = "r1"
	};

	private static string[] Lines(string value) =>
		value.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries);

	private static void Cleanup(RaiPath root)
	{
		if (root.Exists())
			root.rmdir(depth: 8, deleteFiles: true);
	}
}
