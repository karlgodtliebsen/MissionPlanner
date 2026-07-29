using Xunit;

// The MAUI test bootstrap replaces the process-wide Application.Current. Test
// classes therefore cannot safely initialize independent applications in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
