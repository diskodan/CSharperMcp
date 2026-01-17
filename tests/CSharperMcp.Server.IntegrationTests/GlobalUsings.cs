global using NUnit.Framework;
global using FluentAssertions;
global using Moq;

// Configure NUnit to use InstancePerTestCase lifecycle
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
