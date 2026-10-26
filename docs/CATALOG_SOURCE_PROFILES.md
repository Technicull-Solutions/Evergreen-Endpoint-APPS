# Configure an environment's catalog source

The portable **Evergreen Endpoint | ADMIN** client can retrieve and validate a Gitea
software catalog using an environment-owned source profile. Every organization supplies
its own server and repository. The application has no built-in organization server,
repository token, or automatic connection on startup.

The implemented flow is:

```text
Gitea repository -> explicit Admin fetch -> validated library draft
                -> review/export -> administrator deployment -> employee Apps
```

## Configure in Admin

1. Open **Catalog source** and enter the environment name, HTTPS Gitea server base URL,
   repository owner, repository name, branch/tag/commit, and repository-relative file path.
   The server field is the server root or configured Gitea subpath, not a repository browser URL.
2. **Export source profile** saves these settings as `catalog-source.json`. Reopen it on
   another administrator workstation with **Open source profile**. Profiles contain private
   environment metadata, so keep them in your private workspace or configuration repository.
3. For a private repository, enter a token with repository read access in the masked
   **Repository access token (session only)** field. It is sent in the HTTP Authorization
   header, never in a URL. Leave the field blank for anonymous access. The token is not saved
   or exported; changing the server/owner/repository, opening a profile, or closing Admin
   clears the field. **Clear token** clears it immediately. Enter credentials in the local
   application, not in chat, Git-tracked files, screenshots, or issue reports.
4. Select **Fetch and validate catalog**. This reads the raw file through Gitea's API.
   No repository writes occur. A non-empty library draft requires confirmation before
   replacement. Errors and cancellation leave the existing draft intact.
5. Review **Software library**, then **Export JSON** to a workspace file. Have your
   administrator or deployment system copy that reviewed file to the protected location
   documented in [Admin usage](ADMIN_CLIENT.md). Reload **Apps** on the employee device.

Use a reviewed full commit ID for repeatable deployment. A branch such as `main` is
mutable. The displayed SHA-256 identifies the original downloaded bytes; it does not
establish publisher approval or a catalog signature. Formatting/export may change the
bytes, so a formatted export can have a different hash.

## Profile contract

The ZIP includes `catalog-source.example.json` with placeholders only:

```json
{
  "schemaVersion": 1,
  "provider": "gitea",
  "name": "Example environment",
  "serverUrl": "https://gitea.example.test",
  "owner": "Example",
  "repository": "endpoint-config",
  "revision": "main",
  "filePath": "software-catalog.json"
}
```

This is an **Admin workspace document**, separate from the version 1 software catalog
and the machine package-manager configuration. Apps does not read this profile. It
continues reading the protected local catalog and enforcing existing manager/source policy.
Opening or exporting a profile never applies machine configuration.

Only schema version 1 and provider `gitea` are accepted. Unknown properties, including
credential fields, are rejected. HTTPS is required; URL credentials, query strings,
fragments, and traversal/ambiguous path segments are rejected. A Gitea base subpath is
supported. Server certificates use normal operating-system trust validation.

Requests have a 30-second deadline, do not follow redirects or use cookies/default
Windows credentials, and limit catalog bodies to 4 MiB while reading, even without a
Content-Length header. HTML sign-in pages, authentication failures, oversized bodies,
and invalid catalog documents are rejected before replacement. Profiles are limited to
64 KiB. HTTP response bodies and authentication headers are not written to logs.

API references: [Gitea raw-file API](https://docs.gitea.com/api/1.25/operations/repo-get-raw-file/)
and [Gitea token authentication](https://docs.gitea.com/development/api-usage/).

## Boundaries and next increments

This increment supports administrator-initiated retrieval and local review/export. It
does not push catalog changes to Gitea, persist repository credentials, automatically
refresh employee devices, deploy protected files, or sign catalogs. Publishing changes
still uses the organization's reviewed Git workflow. Authentication proves repository
access; structural validation is not package availability or publisher-trust validation.

Direct endpoint retrieval needs a separately defined machine-owned delivery policy,
credential provisioning/rotation, refresh/rollback behavior, and effective-policy
precedence. Do not deploy an administrator's repository token to employee settings as
a substitute for that design.
