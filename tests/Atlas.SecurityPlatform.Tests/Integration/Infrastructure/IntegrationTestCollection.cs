using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration.Infrastructure;

[CollectionDefinition("Integration", DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<AtlasWebApplicationFactory>
{
}
