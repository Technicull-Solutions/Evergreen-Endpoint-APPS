# Evergreen Endpoint Package Manager versioning

Use Semantic Versioning for public releases and a separate globally increasing build
number for Windows identity and traceability. Stamp deliberately through
`scripts/set-version.ps1`; ordinary builds do not silently change either value.

| Value | Current source value | Purpose |
|---|---:|---|
| Version | `0.2.1-rc.4` | Next release candidate; not a published stable release. |
| Build | `8` | Increasing packaged build, including candidates. |
| Windows version | `0.2.1.8` | Numeric base version plus build; components are at most 65535. |

The sidebar, tray tooltip, About page, update metadata, and build-freshness calculation must read these centralized values. Display text must never contain copied version literals.

The build number drives the comparison with `main`:

| Difference from main | Indicator | Meaning |
|---:|---|---|
| N | Green | Up to date |
| N-1 to N-2 | Yellow | Slightly behind |
| N-3 to N-4 | Orange | Behind |
| N-5 or older | Red | Critically behind |

`0.2.0` test assets already exist. Candidates progress through `0.2.1-rc.1`,
`0.2.1-rc.2`, `0.2.1-rc.3`, `0.2.1-rc.4`, and onward as needed, then `0.2.1` after signed MVP acceptance. Increase the build at
each packaging milestone. Use patch versions for fixes, minor versions for features,
and major versions for breaking contracts; document breaking changes explicitly during
0.x development. Do not replace assets with different bytes under an existing identity.

Candidate tags retain the test workflow identity (`v0.2.1-rc.4-build.8`). Reserve `v0.2.1`
for the signed stable release. Artifact filenames may include build and architecture.

```powershell
./scripts/set-version.ps1 -Version 0.2.1-rc.4 -BuildNumber 8
```

Stamping synchronizes CoreData, assembly/file versions, installer and application manifests,
and update metadata. Prereleases stamp Beta; stable releases stamp Current. Changed
identities clear old installer entries. Stamping does not upload files or publish a release:
final signed asset URLs and hashes must be verified before advertising an update.

The updater compares SemVer first (`rc.1 < rc.2 < stable`), then the build for equal
versions. Run `./testing/automation/test-version-stamping.ps1` to test the isolated
stamping path. See [signed MVP gates](SIGNED_MVP_RELEASE.md).

The fork began from UniGetUI `3.3.7`. Its exact upstream tag and commit are recorded in `UPSTREAM.json`; upstream provenance does not participate in Evergreen version ordering.

The sidebar compares only `scripts/BuildNumber` on this repository’s main branch. Updater channel metadata belongs to release selection and must not overwrite that comparison. `scripts/set-version.ps1` stamps the owned product metadata alongside the application; packaging rejects version/build drift.

The sidebar is a development freshness indicator, not a release or security support rating.
