Release CI

Runtime tests run on pushes to main and pull requests without private references.
Publishing a GitHub release (including a prerelease) builds the exact tagged
commit, runs nine standalone suites, compiles Gunmaggedon.Runtime.dll, creates
an install ZIP, generates GitHub build-provenance attestations and attaches
both files to that release. Merely pushing a tag does not publish a release.

Actions > Release runtime > Run workflow builds and attests artifacts without
creating a release or changing its attachments. Use this to validate setup.

One-time credential setup by the repository owner:
1. GitHub Settings > Developer settings > Personal access tokens > Fine-grained
   tokens > Generate new token. Name: Gunmaggedon CI reference download.
2. Resource owner: taxidriver563. Choose Only select repositories, then select
   Gunmaggedon-Build-References. Repository permissions: Contents = Read-only.
   Choose an expiration and renew the secret before it expires. Do not grant
   write access or select the public mod repository.
3. In taxidriver563/Gunmaggedon > Settings > Secrets and variables > Actions,
   create repository secret GUNMAGGEDON_REFERENCES_TOKEN with the token value.
   Do not paste the value in chat or commit it to either repository.
4. Run Release runtime manually. Check the download, compile, attestation and
   artifact steps. Future published releases then build automatically.

Private references are stored in the private build-reference repository. The
workflow pins their Git commit and verifies their ZIP digest before extraction.
Reference assemblies never enter the public artifact paths. Fork/PR test jobs
never receive this secret. All third-party Actions are pinned to commit SHAs.

The .nobp asset pack still needs its normal Unity/Blueprinter build and separate
release upload. CI compiles only the runtime helper; its install ZIP states the
matching pack version. Historical dist packages remain historical snapshots.

Local SDK checks:
  pwsh ./ci/test.ps1
  pwsh ./ci/export-references.ps1 -GameDirectory <game-folder> -OutputZip <private-zip>
  pwsh ./ci/build-runtime.ps1 -ReferencesDirectory <extracted-private-refs>

Use the SDK pinned by global.json. Exported ZIPs and intermediate builds belong
in ignored GunmaggedonRuntime/build; public packages go in ignored artifacts/.
Refreshing game references requires a new private commit and updated URL/digest
in .github/workflows/release.yml. No public game DLL distribution is required.

Verify release provenance using GitHub CLI:
  gh attestation verify Gunmaggedon.Runtime.dll --repo taxidriver563/Gunmaggedon
  gh attestation verify Gunmaggedon.Runtime.zip --repo taxidriver563/Gunmaggedon
Provenance links the binary to a source revision and build workflow. It does
not certify gameplay behavior, prevent modification, or obfuscate public code.

Validation before publication: nine standalone SDK suites, full runtime compile,
PowerShell parser checks and actionlint. Hosted download/attestation validation
requires the owner to add the private-reference token and run the workflow.
