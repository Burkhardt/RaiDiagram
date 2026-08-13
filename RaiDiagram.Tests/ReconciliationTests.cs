using System.Runtime.CompilerServices;

namespace RaiDiagram.Tests;

public class ReconciliationTests
{
	[Fact]
	public async Task ReferenceReconciliation_ReportsCurrentChangedAndMissingElements()
	{
		var model = TestDiagrams.CreateUseCaseModel();
		var snapshots = model.Manifest.Projection.Elements
			.Where(item => item.Source is not null)
			.Select(TestDiagrams.Snapshot)
			.ToArray();
		var provider = new FakeProvider { Resolved = snapshots };
		var reconciler = new DiagramReconciler();

		var current = await reconciler.ReconcileAsync(model.Manifest, provider, cancellationToken: TestContext.Current.CancellationToken);
		Assert.Equal(DiagramModelState.Current, current.State);

		provider.Resolved = snapshots.Select(snapshot => snapshot.Reference.Id == "role/band-manager"
			? new ModelElementSnapshot
			{
				Reference = snapshot.Reference,
				Kind = snapshot.Kind,
				DisplayName = "Promoter",
				RelevantFacts = snapshot.RelevantFacts,
				Relationships = snapshot.Relationships
			}
			: snapshot).ToArray();
		var changed = await reconciler.ReconcileAsync(model.Manifest, provider, cancellationToken: TestContext.Current.CancellationToken);
		Assert.Contains(changed.Changes, item => item.Kind == DiagramModelChangeKind.Changed);

		provider.Resolved = provider.Resolved.Where(item => item.Reference.Id != "usecase/schedule").ToArray();
		var missing = await reconciler.ReconcileAsync(model.Manifest, provider, cancellationToken: TestContext.Current.CancellationToken);
		Assert.Contains(missing.Changes, item => item.Kind == DiagramModelChangeKind.Removed);
	}

	[Fact]
	public async Task ProjectionReconciliation_ReportsNewlyAndNoLongerInScope()
	{
		var model = TestDiagrams.CreateUseCaseModel();
		var role = model.Manifest.Projection.Elements.Single(item => item.Kind == DiagramElementKinds.Role);
		role.SelectedBy.Add("active-roles");
		model.Manifest.Projection.SelectionRules.Add(new DiagramSelectionRule
		{
			Id = "active-roles",
			Query = "roles where active"
		});
		var resolved = model.Manifest.Projection.Elements
			.Where(item => item.Source is not null)
			.Select(TestDiagrams.Snapshot)
			.ToArray();
		var provider = new FakeProvider
		{
			Resolved = resolved,
			Queried =
			[
				new ModelElementSnapshot
				{
					Reference = TestDiagrams.Reference("role/promoter", DiagramElementKinds.Role),
					Kind = DiagramElementKinds.Role,
					DisplayName = "Promoter"
				}
			]
		};

		var diff = await new DiagramReconciler().ReconcileAsync(
			model.Manifest,
			provider,
			DiagramReconciliationMode.Projection,
			TestContext.Current.CancellationToken);

		Assert.Contains(diff.Changes, item => item.Kind == DiagramModelChangeKind.NewlyInScope);
		Assert.Contains(diff.Changes, item => item.Kind == DiagramModelChangeKind.NoLongerInScope);
		Assert.DoesNotContain("theme", diff.ToAgentText(), StringComparison.OrdinalIgnoreCase);
		Assert.Contains("Presentation-only details were excluded", diff.ToAgentText());
	}

	[Fact]
	public async Task MissingProvider_IsStructuredOfflineState()
	{
		var manifest = TestDiagrams.CreateUseCaseModel().Manifest;
		var registry = new DiagramModelProviderRegistry([]);

		var diff = await new DiagramReconciler().ReconcileAsync(
			manifest,
			registry,
			cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(DiagramModelState.ProviderUnavailable, diff.State);
		Assert.Empty(diff.Changes);
	}

	[Fact]
	public async Task ProviderFailure_IsAnIndeterminateStructuredDiff()
	{
		var manifest = TestDiagrams.CreateUseCaseModel().Manifest;
		var provider = new FakeProvider { ThrowWhileResolving = true };

		var diff = await new DiagramReconciler().ReconcileAsync(
			manifest,
			provider,
			cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(DiagramModelState.Indeterminate, diff.State);
		Assert.Contains(diff.Changes, item => item.Kind == DiagramModelChangeKind.Inaccessible);
	}

	private sealed class FakeProvider : IDiagramModelProvider
	{
		public string Scheme => "test-model";
		public IReadOnlyCollection<ModelElementSnapshot> Resolved { get; set; } = [];
		public IReadOnlyCollection<ModelElementSnapshot> Queried { get; set; } = [];
		public bool ThrowWhileResolving { get; set; }

		public ValueTask<ModelRevision> GetRevisionAsync(
			DiagramModelIdentity model,
			CancellationToken cancellationToken = default)
			=> ValueTask.FromResult(new ModelRevision("r2"));

		public async IAsyncEnumerable<ModelElementSnapshot> ResolveAsync(
			DiagramModelIdentity model,
			IReadOnlyCollection<ModelElementReference> references,
			[EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			if (ThrowWhileResolving)
				throw new InvalidOperationException("model temporarily unavailable");
			foreach (var snapshot in Resolved)
			{
				cancellationToken.ThrowIfCancellationRequested();
				yield return snapshot;
				await Task.Yield();
			}
		}

		public async IAsyncEnumerable<ModelElementSnapshot> QueryAsync(
			DiagramModelIdentity model,
			DiagramSelectionRule selectionRule,
			[EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			foreach (var snapshot in Queried)
			{
				cancellationToken.ThrowIfCancellationRequested();
				yield return snapshot;
				await Task.Yield();
			}
		}
	}
}
