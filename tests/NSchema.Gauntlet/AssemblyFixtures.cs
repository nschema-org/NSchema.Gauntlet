using NSchema.Gauntlet.Services;

// One fleet for the whole assembly rather than per collection:
// an engine is expensive to start and every test class wants the same one,
// so sharing it here is what leaves collections free to run in parallel.
[assembly: AssemblyFixture(typeof(EngineFleet))]
