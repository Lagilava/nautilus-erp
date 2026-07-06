// WebApplicationFactory bootstraps the host via HostFactoryResolver, which relies on
// process-wide static state. Running multiple factories in parallel races that state and
// intermittently throws "entry point exited without ever building an IHost". Integration
// tests are I/O-light here, so serialising them is a fine trade for determinism.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
