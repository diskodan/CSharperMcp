global using FluentAssertions;
global using Moq;
global using NUnit.Framework;

// Configure NUnit to use InstancePerTestCase lifecycle
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
