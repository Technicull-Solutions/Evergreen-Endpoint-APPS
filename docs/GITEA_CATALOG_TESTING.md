# Test the software catalog with self-hosted Gitea

This pilot uses Gitea to version catalog/configuration documents and the portable Admin
client to author and validate them. The Apps client reads a protected local deployment
of the selected catalog revision. It does not yet connect to Gitea, store Gitea credentials,
poll a remote catalog, or publish Admin edits automatically. Gitea is the configuration
repository here; WinGet/PowerShell remain the actual package sources.

The portable Admin's **Catalog source** page can now fetch a private Gitea catalog into a
validated draft using environment-specific settings and a session-only repository read
token. See [catalog source profiles](CATALOG_SOURCE_PROFILES.md). That workflow replaces
the manual download step; reviewed local deployment to Apps remains the same.

## 1. Create a catalog repository

Create a repository such as `endpoint-config` on your Gitea instance. Use your normal
HTTPS/SSH Git authentication for private repositories; keep credentials out of JSON,
repository URLs, release notes, and employee configuration. The administrator/deployment
identity needs access to Gitea; employees do not need a repository token for this pilot.
Use a server certificate trusted by the test devices, rather than disabling TLS checks.

Suggested layout:

```text
endpoint-config/
  software-catalog.json
  configuration/package-managers.json    # optional, separate machine configuration
  README.md
```

Seed `software-catalog.json` from the included `SoftwareCatalog.example.json`,
or create a new Apps library in Admin. The sample lists 7-Zip and Visual Studio Code
from `WinGet` / `winget`; displaying a catalog does not install either application.
An empty `Version` means no catalog version pin. Package IDs and configured source names
must match the target device's managers. See [Admin usage](ADMIN_CLIENT.md).

## 2. Author, validate, and commit

1. Clone your Gitea repository using your normal Git client.
2. Extract the portable Admin ZIP into a writable folder and launch
   `Evergreen Endpoint Admin.exe`.
3. In **Software library**, open `software-catalog.json` from that checkout.
4. Edit the library or JSON, then **Validate and format** and **Export JSON** back to
   that file. Validation checks document structure, not package availability or trust.
5. Review the Git diff and commit/push through your normal Gitea workflow. Record the
   full commit ID selected for deployment. Avoid editing files directly on a live device
   and losing the corresponding repository history.

For a second device, clone/fetch the repository and check out the recorded commit before
opening the file in Admin. Alternatively, download the file at that revision from Gitea's
web UI. Its raw-file API is `GET /api/v1/repos/{owner}/{repo}/raw/{filepath}?ref={commit}`;
use your instance's API documentation for its version and private-repository authentication.
Do not use a repository browser HTML page as catalog JSON.
[Gitea raw-file reference](https://docs.gitea.com/enterprise/api/operations/repo-get-raw-file/).

## 3. Deploy to the Apps test device

First preserve any existing deployed catalog for rollback. Have your administrator or
deployment system create this directory with standard-user read access and administrator
write access:

```text
%ProgramData%\Technicull Solutions\Evergreen Endpoint\Package Manager
```

Validate the reviewed revision in Admin, then copy **SoftwareCatalog.json** into that
directory using your administrator/deployment identity. Do not grant employees write
access to the deployed catalog. Ordinary employee installation rights and manager/source
policy still apply: the catalog is a list of apps, not an authorization grant.

Open **Apps** in the employee client and reload it. If entries are hidden, inspect the
unavailable-app filter and verify that WinGet and its `winget` source are available and
permitted. Editing a catalog's Source field does not register a new manager repository.

The optional manager JSON belongs at the separate path documented in the bundled
Policies guide. Deploy it separately after reviewing its effective-policy precedence;
do not copy the software catalog over machine manager configuration.

## 4. Record the pilot results

- Verify the Apps version/build and the Admin executable's file properties against the
  selected release; retain release checksums and source commit.
- The employee library appears under **Apps**, with the expected catalog name and entries.
- Search/filter behavior works and unavailable entries explain their state.
- On a disposable test device, choose an approved app and exercise install, update,
  failure/log viewing, and uninstall through the normal authorization flow.
- Change the catalog in Admin, commit it to Gitea, deploy that revision, and reload Apps.
- Restore the previous reviewed revision and confirm rollback of the displayed library.
  Catalog rollback does not automatically uninstall apps or roll back installed versions.
- Keep private URLs, tokens, and identifying device logs out of public GitHub reports.

## Unsigned release scope

These are explicitly unsigned Windows x64 test builds. The portable Admin does not require
installation, but Windows or organization application-control policy can still block its
executable. This release does not alter those protections. Elevated policy writes remain
unavailable without the existing signed-binary trust requirements. See
[release trust](WINDOWS_RELEASE_TRUST.md) for the distinction between installer warnings,
application-control blocks, and malware detections. ARM64 runtime coverage is not included.
