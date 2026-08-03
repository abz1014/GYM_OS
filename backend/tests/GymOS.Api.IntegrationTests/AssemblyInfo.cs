using Xunit;

// Every test class spins up its own GymOsWebApplicationFactory against the same "gymos_test"
// database and resets its schema on construction (EnsureDeleted + EnsureCreated). Running test
// classes in parallel races two factories against that reset and fails with
// "database gymos_test does not exist".
[assembly: CollectionBehavior(DisableTestParallelization = true)]
