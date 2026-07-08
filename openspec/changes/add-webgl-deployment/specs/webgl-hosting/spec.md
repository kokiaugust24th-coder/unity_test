## ADDED Requirements

### Requirement: Cloudflare Pages Publishing
The WebGL build output directory SHALL be deployable directly to Cloudflare Pages as a static site, and Cloudflare Pages SHALL be documented as the default free hosting target (unlimited bandwidth, git-based deploys, supports custom response headers for future extensibility).

#### Scenario: Deploying to Cloudflare Pages
- **WHEN** the `Builds/WebGL` output directory is deployed to a Cloudflare Pages project (via dashboard upload or `wrangler pages deploy`)
- **THEN** the game SHALL load and run in-browser without any additional server configuration

### Requirement: Alternative Static Hosting Guide
Documentation SHALL describe how to deploy the same WebGL build to at least one alternative static host (itch.io as the zero-account-setup, gaming-audience alternative; GitHub Pages as a git-native alternative), including the compression header caveats and how Decompression Fallback removes the need for server-side configuration on hosts without custom header support.

#### Scenario: Deploying to itch.io
- **WHEN** the WebGL build is zipped and uploaded to itch.io as an HTML5 project
- **THEN** the build SHALL still load successfully due to Decompression Fallback, without itch.io-specific server configuration

#### Scenario: Deploying to GitHub Pages
- **WHEN** the WebGL build folder is published via GitHub Pages without custom server headers configured
- **THEN** the build SHALL still load successfully due to Decompression Fallback

### Requirement: Pre-publish Verification Checklist
Before publishing, the WebGL build SHALL be verified by serving it locally and loading it in at least one desktop browser to confirm the initial world content loads and the player can move without a crash.

#### Scenario: Local smoke test before publish
- **WHEN** the WebGL build is served from a local static server and opened in a browser
- **THEN** the initial world cell(s) SHALL load and the player SHALL be able to move within the frame budget without a hard crash or unrecoverable error
