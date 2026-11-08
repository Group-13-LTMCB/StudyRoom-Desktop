using Xunit;

// Disable parallel test execution in UI tests to avoid WPF BAML loading and Dispatcher conflicts across STA threads
[assembly: CollectionBehavior(DisableTestParallelization = true)]
