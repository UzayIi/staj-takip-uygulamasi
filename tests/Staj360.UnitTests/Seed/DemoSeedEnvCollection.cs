namespace Staj360.UnitTests.Seed;

/// <summary>
/// Demo seeder testleri process-wide Environment değişkenlerini kullanır;
/// paralel çalışınca birbirini bozar. Aynı collection içinde serileştirilir.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DemoSeedEnvCollection : ICollectionFixture<object>
{
    public const string Name = "DemoSeedEnv";
}
