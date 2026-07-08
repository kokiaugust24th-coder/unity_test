## ADDED Requirements

### Requirement: WebGL Build Target Configuration
The project SHALL provide a WebGL build configuration using the IL2CPP scripting backend with Decompression Fallback enabled, so the build loads correctly regardless of whether the hosting server sets compression `Content-Encoding` headers.

#### Scenario: Hosting without compression headers
- **WHEN** the WebGL build is served from a static host that does not set `Content-Encoding` for compressed files
- **THEN** the build SHALL still load and run correctly via the Decompression Fallback mechanism

### Requirement: WebGL Memory Configuration
The WebGL build SHALL configure an explicit initial memory size with Memory Growth enabled and a defined maximum heap limit, so that streaming open-world content does not crash the browser tab with an out-of-memory error.

#### Scenario: Streaming multiple cells
- **WHEN** multiple world cells with HLOD and terrain tiles stream in during gameplay
- **THEN** memory usage SHALL stay within the configured WebGL heap limit without an out-of-memory crash

### Requirement: WebGL Threading Compatibility
Existing Jobs/Burst hot paths used by world-streaming SHALL execute correctly on WebGL without WebGL Threading Support (Web Worker + SharedArrayBuffer) enabled, so the build works on hosts that cannot set COOP/COEP headers.

#### Scenario: Streaming distance calculation without special headers
- **WHEN** the WebGL build is hosted on a static host without COOP/COEP headers
- **THEN** the streaming Jobs system SHALL run correctly in single-threaded mode without errors or missing functionality

### Requirement: WebGL Addressables Build Path Isolation
Addressable content build/load paths SHALL resolve to a WebGL-specific location distinct from other build targets, so cell, HLOD, and terrain-tile Addressable content loads correctly via UnityWebRequest in a browser without colliding with Standalone build output. This SHALL be achieved via the existing `[BuildTarget]`-tokenized profile paths (no duplicate profile required) as long as the active build target is WebGL when Addressables content is built.

#### Scenario: Loading a cell bundle in-browser
- **WHEN** the WebGL client requests an Addressable cell bundle at runtime
- **THEN** the bundle SHALL be fetched over HTTP from the WebGL-resolved path and load successfully in the browser sandbox

#### Scenario: Building Addressables content for WebGL
- **WHEN** the automated build script builds Addressables content with the active build target set to WebGL
- **THEN** the resulting bundles SHALL be written to the `[BuildTarget]`-resolved WebGL path and bundled into the WebGL player's StreamingAssets, separate from any other target's build output

### Requirement: Automated WebGL Build Script
The project SHALL provide an Editor build script with a batch-mode entry point (`-executeMethod`) that produces a complete WebGL build without manual Editor interaction, so builds are reproducible outside the Editor UI.

#### Scenario: Batch-mode build invocation
- **WHEN** Unity is invoked with `-batchmode -quit -buildTarget WebGL -executeMethod <BuildScript.Method>`
- **THEN** a playable WebGL build SHALL be produced in the configured output directory
