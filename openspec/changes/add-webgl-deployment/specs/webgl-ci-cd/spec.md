## ADDED Requirements

### Requirement: CI Test and Build Verification
On every pull request and push targeting the main branch, CI SHALL run Unity EditMode tests and produce a WebGL build, and SHALL fail the check if either the tests or the build fail.

#### Scenario: Pull request opened
- **WHEN** a pull request is opened or updated targeting the main branch
- **THEN** CI SHALL run EditMode tests and a WebGL build, and SHALL report a failing check if either step fails

### Requirement: Automated Cloudflare Pages Deployment on Merge
On push/merge to the main branch (not on pull requests), CI SHALL automatically build the WebGL player and publish `Builds/WebGL` to the configured Cloudflare Pages project, without manual intervention, and only after tests and the build succeed.

#### Scenario: Merge to main
- **WHEN** a commit is merged or pushed directly to the main branch and CI tests/build succeed
- **THEN** CI SHALL build the WebGL output and publish it to Cloudflare Pages automatically

#### Scenario: Failing tests block deployment
- **WHEN** EditMode tests fail on the main branch
- **THEN** CI SHALL NOT publish a new build to Cloudflare Pages

### Requirement: CI Secrets Management
Unity license credentials and the Cloudflare API token SHALL be stored as CI secrets (not committed to the repository) and SHALL NOT be printed in CI logs.

#### Scenario: CI job execution
- **WHEN** a CI job activates the Unity license or invokes the Cloudflare Pages deploy action with an API token
- **THEN** the credentials SHALL be sourced from CI secrets and SHALL NOT appear in plaintext in the CI log output

### Requirement: Build Caching
CI SHALL cache the Unity `Library/` folder between runs, keyed on `Assets/` and `Packages/` contents, to reduce Unity import and build time on repeated runs.

#### Scenario: Repeated CI runs without dependency changes
- **WHEN** CI runs again without changes to `Assets/` or `Packages/` affecting the cache key
- **THEN** the cached `Library/` folder SHALL be restored and reused instead of a full re-import
