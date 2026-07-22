\# Codex Task: Implement the General MAVFTP Subsystem



\## Objective



Implement a reusable, production-quality MAVFTP client for MissionPlanner Next Generation.



This task covers the \*\*general MAVFTP protocol subsystem only\*\*.



Do not implement ArduPilot packed-parameter decoding in this task. Fast parameter download will be implemented later as a consumer of the MAVFTP subsystem.



The resulting subsystem must support future consumers such as:



\* Fast parameter download

\* DataFlash log download

\* Mission-file transfer

\* Rally-point files

\* Geofence files

\* Terrain files

\* Lua scripts

\* General ArduPilot filesystem access



\---



\# Required Preparation



Before changing code, read:



1\. `AGENTS.md`

2\. `src/ai.md`

3\. `docs/DESIGN\_CONCEPTS.md`

4\. `docs/ARCHITECTURE\_DECISION\_RECORDS.md`

5\. `docs/VEHICLE\_CONNECTION.md`

6\. `docs/PARAMETERS.md`

7\. `.editorconfig`



Inspect the existing solution before proposing namespaces, projects, interfaces, or dependencies.



Follow the existing naming, dependency-injection, test, logging, cancellation, and result-type conventions.



Do not introduce a third-party MAVFTP library.



Use the existing MAVLink connection, encoding, decoding, message-pump, channel, and dependency-injection infrastructure where appropriate.



\---



\# Authoritative Protocol References



Use the current official specifications as the source of truth:



\* MAVLink File Transfer Protocol service specification

\* MAVLink `FILE\_TRANSFER\_PROTOCOL` message definition

\* ArduPilot MAVFTP developer documentation

\* ArduPilot `GCS\_FTP` implementation when interoperability details are ambiguous



Do not infer protocol behavior solely from Mission Planner, QGroundControl, MAVSDK, forum posts, or old code examples.



Other implementations may be inspected for interoperability insight, but the official MAVLink protocol and current ArduPilot behavior take precedence.



Document every intentional deviation or compatibility workaround.



\---



\# Architectural Boundaries



The MAVFTP wire protocol belongs in the MAVLink infrastructure layer.



It must not leak MAVLink packets or raw `FILE\_TRANSFER\_PROTOCOL` payloads into the domain, application, or UI layers.



Use boundaries equivalent to:



```text

Application consumer

&#x20;       ↓

IMavFtpClient

&#x20;       ↓

MAVFTP transfer/session coordinator

&#x20;       ↓

MAVFTP packet codec

&#x20;       ↓

Existing IMavLinkConnection

&#x20;       ↓

Transport

```



Do not place MAVFTP protocol types in `MissionPlanner.Core` unless the existing project structure clearly defines another appropriate protocol project.



Do not add MAVFTP behavior to the parameter subsystem.



Do not couple the generic client to ArduPilot packed parameters.



Do not update UI code in this task.



\---



\# Initial Repository Analysis



Before implementation:



1\. Locate the existing `FILE\_TRANSFER\_PROTOCOL` message record, decoder, and encoder support.

2\. Determine how outbound MAVLink messages are currently encoded and sent.

3\. Determine how inbound messages are distributed and filtered.

4\. Determine how concurrent request-response services are coordinated.

5\. Identify the established result, error, clock, retry, logging, and cancellation patterns.

6\. Identify test utilities or simulated vehicles that can be extended for MAVFTP.



Write a concise implementation plan before modifying code.



Prefer integration with existing abstractions over creating parallel infrastructure.



\---



\# Proposed Public API



Adapt names and locations to existing project conventions, but provide equivalent capabilities.



A suitable starting interface is:



```csharp

public interface IMavFtpClient

{

&#x20;   Task<MavFtpFileInfo> GetFileInfoAsync(

&#x20;       VehicleId vehicleId,

&#x20;       string remotePath,

&#x20;       CancellationToken cancellationToken = default);



&#x20;   Task<IReadOnlyList<MavFtpDirectoryEntry>> ListDirectoryAsync(

&#x20;       VehicleId vehicleId,

&#x20;       string remotePath,

&#x20;       CancellationToken cancellationToken = default);



&#x20;   Task<byte\[]> DownloadFileAsync(

&#x20;       VehicleId vehicleId,

&#x20;       string remotePath,

&#x20;       IProgress<MavFtpProgress>? progress = null,

&#x20;       CancellationToken cancellationToken = default);



&#x20;   Task DownloadFileAsync(

&#x20;       VehicleId vehicleId,

&#x20;       string remotePath,

&#x20;       Stream destination,

&#x20;       IProgress<MavFtpProgress>? progress = null,

&#x20;       CancellationToken cancellationToken = default);



&#x20;   Task UploadFileAsync(

&#x20;       VehicleId vehicleId,

&#x20;       string remotePath,

&#x20;       Stream source,

&#x20;       IProgress<MavFtpProgress>? progress = null,

&#x20;       CancellationToken cancellationToken = default);



&#x20;   Task RemoveFileAsync(

&#x20;       VehicleId vehicleId,

&#x20;       string remotePath,

&#x20;       CancellationToken cancellationToken = default);



&#x20;   Task CreateDirectoryAsync(

&#x20;       VehicleId vehicleId,

&#x20;       string remotePath,

&#x20;       CancellationToken cancellationToken = default);



&#x20;   Task RemoveDirectoryAsync(

&#x20;       VehicleId vehicleId,

&#x20;       string remotePath,

&#x20;       CancellationToken cancellationToken = default);



&#x20;   Task RenameAsync(

&#x20;       VehicleId vehicleId,

&#x20;       string sourcePath,

&#x20;       string destinationPath,

&#x20;       CancellationToken cancellationToken = default);



&#x20;   Task ResetSessionsAsync(

&#x20;       VehicleId vehicleId,

&#x20;       CancellationToken cancellationToken = default);

}

```



The first implementation does not have to expose every operation publicly if some operations cannot yet be tested safely.



At minimum, the implemented vertical slice must support:



\* Reset sessions

\* Open file for reading

\* Read file

\* Burst-read file

\* Terminate session

\* List directory

\* Download to a stream

\* Correct ACK/NAK handling

\* Retry and timeout handling

\* Cancellation

\* Progress reporting



Upload support may be implemented in the same task only after the download path is complete and well tested.



Do not return `byte\[]` as the only download API. Large files must be streamable without buffering the entire file in memory.



\---



\# Internal Protocol Model



Create explicit protocol types equivalent to:



```text

MavFtpPacket

MavFtpOpcode

MavFtpNakError

MavFtpSession

MavFtpRequest

MavFtpResponse

MavFtpDirectoryEntry

MavFtpProgress

MavFtpException or typed result/error model

```



Use the repository's preferred records/classes and error-handling conventions.



Do not spread numeric opcodes or error codes across the implementation.



Centralize all protocol constants.



The packet codec must explicitly handle:



\* Sequence number

\* Session identifier

\* Opcode

\* Payload size

\* Requested opcode

\* Burst completion flag

\* Padding/alignment fields

\* File offset

\* Data payload



Use little-endian encoding where required by the protocol.



Use `Span<byte>`, `ReadOnlySpan<byte>`, `BinaryPrimitives`, and pooled buffers where appropriate.



Do not use unsafe code unless already justified by the project and supported by measurements.



\---



\# MAVLink Message Integration



Use the MAVLink `FILE\_TRANSFER\_PROTOCOL` message.



Verify:



\* Target network

\* Target system

\* Target component

\* Payload length

\* FTP payload layout

\* MAVLink v1/v2 compatibility expectations

\* Source system/component correlation



Responses must be matched to the correct:



\* Vehicle

\* Component

\* MAVFTP sequence

\* Request opcode

\* Session



Do not accept unrelated FTP traffic.



Do not assume there is only one vehicle connected.



Do not assume autopilot component ID is always `1` unless the existing vehicle model explicitly resolves it that way.



\---



\# Sequence Numbers



Implement sequence handling as a first-class concern.



Requirements:



\* Sequence values wrap correctly.

\* A response must correlate with the originating request.

\* Duplicate responses must not complete a later request.

\* Delayed responses must not corrupt a later operation.

\* Retries must follow protocol-compatible sequence behavior.

\* Sequence allocation must be concurrency-safe.



Add tests around wraparound and delayed duplicate responses.



\---



\# Session Management



Represent active sessions explicitly.



Each file operation must correctly manage the session lifecycle:



```text

Reset stale sessions when appropriate

&#x20;       ↓

Open file

&#x20;       ↓

Read or write

&#x20;       ↓

Terminate session

```



Requirements:



\* Always attempt session cleanup after success.

\* Attempt cleanup after failure when a session was created.

\* Cancellation must not leave local session state active.

\* Remote cleanup failure must be logged but must not hide the original error.

\* Session identifiers are scoped correctly per remote endpoint.

\* Unknown-session responses are handled explicitly.

\* No session state is stored globally without vehicle/component scoping.



Use `try/finally` or an equivalent reliable lifecycle mechanism.



\---



\# Concurrency



MAVFTP is a request-response protocol running over a shared MAVLink stream.



Prevent competing operations from consuming each other's responses.



Initially, serialize MAVFTP operations per vehicle/component unless the protocol and implementation can prove safe concurrent sessions.



A suitable design is a keyed asynchronous lock:



```text

VehicleId + target component

&#x20;       ↓

single active MAVFTP operation

```



Do not use one global lock for every connected vehicle.



Do not hold locks while performing unrelated UI or domain work.



Document the concurrency policy.



\---



\# Request/Response Coordination



Do not implement response waiting by repeatedly reading directly from a shared general-purpose MAVLink stream.



Introduce or reuse a dispatcher that routes incoming FTP responses by correlation criteria.



A suitable internal structure is:



```text

Incoming FILE\_TRANSFER\_PROTOCOL message

&#x20;       ↓

Validate source and payload

&#x20;       ↓

Decode MavFtpPacket

&#x20;       ↓

Match pending request/session/sequence

&#x20;       ↓

Complete corresponding waiter

```



The coordinator must handle:



\* Timeout

\* Cancellation

\* Duplicate response

\* Late response

\* Malformed payload

\* ACK

\* NAK

\* Responses for another vehicle

\* Responses for another component

\* Unsolicited FTP messages



Do not silently discard malformed packets without diagnostic logging.



Avoid unbounded collections of pending requests or stale responses.



\---



\# Retry Policy



Implement bounded retries for transient failures.



Retry candidates may include:



\* Request timeout

\* Lost ACK

\* Lost read response

\* Retryable NAK conditions where the protocol allows retry

\* Burst-read gaps

\* Session reset recovery



Do not blindly retry:



\* Invalid path

\* File not found

\* Permission denied

\* Invalid data size

\* Unsupported operation

\* Invalid session after the recovery policy has been exhausted



Use repository-standard time abstractions if available so retry tests do not require long real delays.



Retry delays should be configurable through options rather than hard-coded throughout the client.



A possible options type:



```csharp

public sealed class MavFtpOptions

{

&#x20;   public TimeSpan RequestTimeout { get; init; }



&#x20;   public int MaximumRequestAttempts { get; init; }



&#x20;   public int MaximumSessionRecoveryAttempts { get; init; }



&#x20;   public int ReadChunkSize { get; init; }



&#x20;   public bool PreferBurstRead { get; init; }

}

```



Use conservative default read sizes suitable for radio links.



Do not optimize solely for localhost or USB.



Validate configured values.



\---



\# Download Behavior



Implement streaming downloads.



Expected flow:



```text

OpenFileRO

&#x20;   ↓

Determine file size when available

&#x20;   ↓

BurstReadFile, or ReadFile fallback

&#x20;   ↓

Write received blocks at their offsets

&#x20;   ↓

Recover missing blocks

&#x20;   ↓

Verify completion

&#x20;   ↓

TerminateSession

```



Requirements:



\* Support empty files.

\* Support files smaller than one payload.

\* Support files spanning many packets.

\* Support exact payload-boundary sizes.

\* Support short final blocks.

\* Correctly handle end-of-file signaling.

\* Correctly handle burst completion.

\* Correctly handle packet loss within a burst.

\* Avoid duplicate writes when duplicate blocks arrive.

\* Detect impossible offsets and malformed sizes.

\* Do not report success until all expected bytes are present.

\* Respect stream capabilities.

\* Do not close a caller-owned destination stream.

\* Do not require the destination stream to be seekable unless documented.



When the output stream is non-seekable, maintain ordered delivery or use a bounded reordering mechanism.



Do not allocate a full-file buffer for stream-based downloads.



\---



\# Burst Read



Burst read is important for throughput but must not be the only supported path.



Implement:



1\. Burst-read preferred mode

2\. Normal `ReadFile` fallback

3\. Recovery of missing ranges using regular reads where appropriate



Do not assume all autopilots or links behave perfectly with maximum payload sizes.



Burst handling must tolerate:



\* Multiple data packets for one request

\* Burst completion marker

\* Short burst

\* Lost packet

\* Duplicate packet

\* Delayed packet

\* End-of-file NAK/indication

\* Timeout before burst completion



Document the chosen recovery algorithm.



\---



\# Directory Listing



Implement directory listing parsing as a separate codec concern.



Requirements:



\* Parse file entries

\* Parse directory entries

\* Preserve names accurately

\* Preserve file size when supplied

\* Handle multiple result pages/offsets

\* Stop correctly at end of listing

\* Reject malformed entries safely

\* Avoid path concatenation bugs

\* Normalize separators only where protocol-safe



Do not use host filesystem APIs to normalize remote paths in a way that changes MAVFTP path semantics.



\---



\# Error Model



Map MAVFTP NAK/error values into a typed application-facing error model.



Errors should retain useful diagnostic context:



\* Vehicle

\* Target component

\* Operation

\* Remote path

\* Opcode

\* Requested opcode

\* Sequence

\* Session

\* Offset

\* MAVFTP error code

\* Retry count



Do not expose raw exceptions for expected remote errors such as file-not-found.



Unexpected malformed protocol data may use a protocol exception or equivalent typed failure.



Do not log routine file-not-found probes as fatal errors.



\---



\# Cancellation and Cleanup



Every asynchronous public operation must accept `CancellationToken`.



Requirements:



\* Cancellation must interrupt waiting and retries promptly.

\* Cancellation must not be converted into timeout.

\* Preserve `OperationCanceledException` semantics used by the project.

\* Dispose registrations and pending-request entries.

\* Attempt remote session termination when practical.

\* Never block synchronously on asynchronous operations.

\* Do not use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.



\---



\# Logging



Use structured logging.



Useful events include:



\* Operation started/completed

\* Session opened/terminated

\* Retry attempt

\* Timeout

\* Fallback from burst read to normal read

\* Recovering missing range

\* Remote NAK

\* Malformed response

\* Cleanup failure

\* Transfer throughput summary



Do not log every packet at `Information`.



Packet-level diagnostics should be `Trace` or `Debug`.



Avoid expensive formatting in hot paths.



Do not log file content.



\---



\# Progress Reporting



Provide progress independently of the UI.



A suitable immutable model is:



```csharp

public sealed record MavFtpProgress(

&#x20;   string RemotePath,

&#x20;   long BytesTransferred,

&#x20;   long? TotalBytes,

&#x20;   double? BytesPerSecond);

```



Progress reporting must:



\* Be optional

\* Not affect protocol correctness

\* Not run under critical locks

\* Avoid reporting on every tiny packet when that creates excessive UI work

\* Report final completion

\* Handle unknown total length



Do not reference Avalonia types.



\---



\# Dependency Injection



Register the subsystem through the existing service-registration pattern.



Possible registrations:



```text

IMavFtpClient

IMavFtpPacketCodec

IMavFtpResponseDispatcher

MavFtpOptions

```



Use the project's existing options/configuration conventions.



Do not use a service locator.



Do not construct dependencies with `new` inside high-level services unless they are immutable value objects or protocol packets.



\---



\# Testing Strategy



Create comprehensive automated tests.



Use unit tests for packet encoding/decoding and deterministic protocol state-machine tests.



Extend or create a fake MAVFTP vehicle/server for integration-style tests.



Do not require physical flight-controller hardware for the normal test suite.



\## Packet codec tests



Cover:



\* Every implemented opcode

\* ACK decoding

\* NAK decoding

\* Maximum payload

\* Empty payload

\* Offset encoding

\* Session encoding

\* Sequence encoding

\* Sequence wraparound

\* Malformed size

\* Truncated payload

\* Invalid opcode

\* Little-endian fields



\## Download tests



Cover:



\* Empty file

\* Small file

\* Multi-block file

\* Exact block boundary

\* Short final block

\* Burst download

\* Normal-read fallback

\* Lost response followed by retry

\* Lost block inside burst

\* Duplicate block

\* Out-of-order block

\* Delayed response from previous request

\* Timeout

\* Cancellation

\* File not found

\* Invalid session and recovery

\* Cleanup after success

\* Cleanup after failure

\* Concurrent operations for two different vehicles

\* Serialized operations for the same vehicle

\* Destination stream failure



\## Directory tests



Cover:



\* Empty directory

\* Files and directories

\* Multi-page listing

\* Long names within protocol limits

\* Malformed entry

\* End-of-directory behavior



\## Dispatcher tests



Cover:



\* Correct vehicle correlation

\* Correct component correlation

\* Correct sequence correlation

\* Wrong sequence ignored

\* Wrong vehicle ignored

\* Late duplicate ignored

\* Pending request removed after timeout

\* Pending request removed after cancellation



\## Performance-oriented tests



Add a test or benchmark that demonstrates a large file can be downloaded without allocating a buffer equal to the complete file size.



Do not create brittle wall-clock performance assertions in unit tests.



\---



\# Simulator/Fake Server



Implement a deterministic fake server capable of:



\* Maintaining files in memory

\* Opening read sessions

\* Serving normal reads

\* Serving burst reads

\* Listing directories

\* Terminating sessions

\* Resetting sessions

\* Returning configured NAKs

\* Dropping selected packets

\* Duplicating selected packets

\* Reordering selected packets

\* Delaying selected packets



Keep the fake protocol behavior close enough to ArduPilot for meaningful interoperability tests.



Do not place test-only behavior in production classes.



\---



\# Documentation



Create:



```text

docs/MAVFTP.md

```



Document:



\* Purpose

\* Architectural placement

\* Public API

\* Request/response pipeline

\* Session lifecycle

\* Concurrency policy

\* Retry policy

\* Burst-read behavior

\* Error mapping

\* Cancellation behavior

\* Testing strategy

\* Known limitations

\* Planned consumers, including packed parameter download



Update:



```text

docs/README.md

docs/FEATURES.md

```



Update `docs/ARCHITECTURE\_DECISION\_RECORDS.md` only when the implementation introduces a genuine architectural decision.



Do not duplicate detailed code documentation in `AGENTS.md` or `src/ai.md`.



\---



\# Scope Exclusions



Do not implement these in this task:



\* ArduPilot `@PARAM/param.pck` decoding

\* Parameter registry population

\* Parameter UI changes

\* Log viewer UI

\* Mission-file interpretation

\* Lua editor

\* Firmware upload

\* Terrain interpretation

\* Generic local file-browser UI

\* Broad refactoring of unrelated MAVLink code

\* Replacement of the existing parameter protocol



Do not remove or alter the existing classic MAVLink parameter implementation.



\---



\# Implementation Phases



Implement in reviewable phases.



\## Phase 1 — Protocol foundation



\* FTP opcodes and error codes

\* Packet model

\* Packet encoder/decoder

\* `FILE\_TRANSFER\_PROTOCOL` integration

\* Unit tests



\## Phase 2 — Coordination



\* Response dispatcher

\* Request correlation

\* Timeout

\* Cancellation

\* Retry policy

\* Per-vehicle/component serialization

\* Tests



\## Phase 3 — Basic download



\* Reset sessions

\* Open read-only

\* Normal file reads

\* End-of-file handling

\* Terminate session

\* Streaming destination

\* Progress

\* Tests



\## Phase 4 — Burst download



\* Burst read

\* Missing-block detection

\* Recovery reads

\* Fallback

\* Tests



\## Phase 5 — Directory listing



\* Directory entry codec

\* Pagination

\* Public API

\* Tests



\## Phase 6 — Integration and documentation



\* Dependency injection

\* Fake MAVFTP server

\* End-to-end tests

\* `docs/MAVFTP.md`

\* `FEATURES.md`

\* Build and test verification



Do not begin upload support until these phases are complete and stable.



\---



\# Definition of Done



The task is complete when:



1\. The solution builds with no new warnings.

2\. All existing tests still pass.

3\. New MAVFTP tests pass.

4\. A file can be downloaded through the existing MAVLink connection into a caller-provided stream.

5\. Burst read works and has a tested normal-read fallback.

6\. Timeout, retry, cancellation, and remote NAKs are handled deterministically.

7\. Concurrent operations cannot steal each other's responses.

8\. Operations are isolated by vehicle/component.

9\. Session cleanup occurs on success, failure, timeout, and cancellation where practical.

10\. Large-file download does not require a full-file memory allocation.

11\. No MAVLink packet type leaks into Core, Application, or UI APIs.

12\. The classic parameter service remains unchanged and functional.

13\. `docs/MAVFTP.md`, `docs/README.md`, and `docs/FEATURES.md` accurately describe the implementation.

14\. The final response includes:



&#x20;   \* Files changed

&#x20;   \* Architectural decisions

&#x20;   \* Protocol operations implemented

&#x20;   \* Tests added

&#x20;   \* Build/test results

&#x20;   \* Known limitations

&#x20;   \* Recommended next task



\---



\# Verification Commands



Determine the exact solution path from the repository, then run the equivalent of:



```powershell

dotnet restore

dotnet build --no-restore

dotnet test --no-build

```



Run targeted MAVFTP tests during development and the complete test suite before finishing.



Do not claim successful verification unless the commands were actually executed.



Report any test that requires real ArduPilot hardware separately from the normal automated suite.



\---



\# Final Constraint



Prioritize protocol correctness, cancellation safety, clean correlation, and deterministic tests over maximum throughput.



Do not optimize prematurely.



However, do not create an architecture that forces one-request-per-packet behavior or full-file buffering, because burst transfer and large-file streaming are core requirements of this subsystem.



